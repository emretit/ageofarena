import { WebSocketServer, WebSocket } from "ws";
import { createServer } from "http";

const port = Number(process.env.PORT ?? 2567);
const httpServer = createServer((req, res) => {
  // Health check
  res.writeHead(200, { "Content-Type": "text/plain", "Access-Control-Allow-Origin": "*" });
  res.end("Age of Arena — game server OK");
});
const wss = new WebSocketServer({ server: httpServer });

// ── Oda ve oyuncu modeli ──────────────────────────────────────────────────────

interface Player {
  ws: WebSocket;
  id: string;
  name: string;
  team: number;
  ready: boolean;
  roomCode: string;
}

interface Room {
  code: string;
  host: string;          // player id
  players: Map<string, Player>;
  started: boolean;
  seed: number;
  inputBuffer: Map<number, InputFrame[]>;
  checksums: Map<string, { tick: number; hash: number }>;
}

interface InputFrame {
  playerId: string;
  tick: number;
  commands: any[];
}

const rooms = new Map<string, Room>();
const playerMap = new Map<WebSocket, Player>();

function send(ws: WebSocket, type: string, payload: any) {
  if (ws.readyState === WebSocket.OPEN)
    ws.send(JSON.stringify({ type, ...payload }));
}

function broadcast(room: Room, type: string, payload: any, except?: string) {
  room.players.forEach((p) => {
    if (p.id !== except) send(p.ws, type, payload);
  });
}

function makeRoomCode(): string {
  return Math.random().toString(36).substring(2, 7).toUpperCase();
}

// ── Bağlantı yönetimi ──────────────────────────────────────────────────────────

wss.on("connection", (ws) => {
  const playerId = Math.random().toString(36).substring(2, 10);
  console.log(`[+] bağlandı: ${playerId}`);

  ws.on("message", (raw) => {
    let msg: any;
    try { msg = JSON.parse(raw.toString()); } catch { return; }

    const { type } = msg;

    // ── create_room ───────────────────────────────────────────────────────────
    if (type === "create_room") {
      const code = makeRoomCode();
      const player: Player = {
        ws, id: playerId,
        name: msg.playerName ?? "Oyuncu1",
        team: 0, ready: false,
        roomCode: code,
      };
      const room: Room = {
        code, host: playerId,
        players: new Map([[playerId, player]]),
        started: false,
        seed: Math.floor(Math.random() * 2147483647),
        inputBuffer: new Map(),
        checksums: new Map(),
      };
      rooms.set(code, room);
      playerMap.set(ws, player);
      send(ws, "room_created", { roomCode: code, team: 0, playerId });
      console.log(`[Oda] oluşturuldu: ${code} host=${player.name}`);
    }

    // ── join_room ─────────────────────────────────────────────────────────────
    else if (type === "join_room") {
      const room = rooms.get(msg.roomCode);
      if (!room) { send(ws, "error", { message: "Oda bulunamadı" }); return; }
      if (room.started) { send(ws, "error", { message: "Oyun başladı" }); return; }
      if (room.players.size >= 4) { send(ws, "error", { message: "Oda dolu" }); return; }

      const team = room.players.size;
      const player: Player = {
        ws, id: playerId,
        name: msg.playerName ?? `Oyuncu${team + 1}`,
        team, ready: false,
        roomCode: msg.roomCode,
      };
      room.players.set(playerId, player);
      playerMap.set(ws, player);

      const playerList = [...room.players.values()].map(p => ({ name: p.name, team: p.team, ready: p.ready }));
      send(ws, "room_joined", { roomCode: msg.roomCode, team, playerId, playerList });
      broadcast(room, "player_joined", { name: player.name, team, playerList }, playerId);
      console.log(`[Oda] ${msg.roomCode} katıldı: ${player.name} (team=${team})`);
    }

    // ── ready ─────────────────────────────────────────────────────────────────
    else if (type === "ready") {
      const player = playerMap.get(ws);
      if (!player) return;
      const room = rooms.get(player.roomCode);
      if (!room) return;
      room.players.get(playerId)!.ready = true;

      const playerList = [...room.players.values()].map(p => ({ name: p.name, team: p.team, ready: p.ready }));
      broadcast(room, "player_ready", { playerId, playerList });

      const allReady = [...room.players.values()].every(p => p.ready);
      if (allReady && room.players.size >= 2 && !room.started) {
        room.started = true;
        const players = [...room.players.values()].map(p => ({ id: p.id, name: p.name, team: p.team }));
        broadcast(room, "game_start", { seed: room.seed, players });
        console.log(`[Oda] ${room.code} BAŞLADI seed=${room.seed}`);
      }
    }

    // ── input ─────────────────────────────────────────────────────────────────
    else if (type === "input") {
      const player = playerMap.get(ws);
      if (!player) return;
      const room = rooms.get(player.roomCode);
      if (!room || !room.started) return;

      const tick: number = msg.tick;
      if (!room.inputBuffer.has(tick)) room.inputBuffer.set(tick, []);
      room.inputBuffer.get(tick)!.push({ playerId, tick, commands: msg.commands ?? [] });

      const activePlayers = [...room.players.values()].length;
      if (room.inputBuffer.get(tick)!.length >= activePlayers) {
        broadcast(room, "tick_inputs", { tick, inputs: room.inputBuffer.get(tick)! });
        room.inputBuffer.delete(tick);
      }
    }

    // ── checksum ──────────────────────────────────────────────────────────────
    else if (type === "checksum") {
      const player = playerMap.get(ws);
      if (!player) return;
      const room = rooms.get(player.roomCode);
      if (!room) return;
      room.checksums.set(playerId, { tick: msg.tick, hash: msg.hash });

      if (room.checksums.size === room.players.size) {
        const hashes = [...room.checksums.values()].map(c => c.hash);
        const allSame = hashes.every(h => h === hashes[0]);
        if (!allSame) {
          broadcast(room, "desync", { tick: msg.tick });
          console.warn(`[Oda] ${room.code} DESYNC tick=${msg.tick}`);
        }
        room.checksums.clear();
      }
    }

    // ── chat ──────────────────────────────────────────────────────────────────
    else if (type === "chat") {
      const player = playerMap.get(ws);
      if (!player) return;
      const room = rooms.get(player.roomCode);
      if (!room) return;
      broadcast(room, "chat", { name: player.name, message: msg.message });
    }
  });

  ws.on("close", () => {
    const player = playerMap.get(ws);
    if (!player) return;
    playerMap.delete(ws);

    const room = rooms.get(player.roomCode);
    if (!room) return;
    room.players.delete(playerId);
    broadcast(room, "player_left", { playerId, name: player.name, team: player.team });
    console.log(`[-] ayrıldı: ${player.name}`);

    if (room.players.size === 0) {
      rooms.delete(room.code);
      console.log(`[Oda] ${room.code} silindi (boş)`);
    }
  });
});

httpServer.listen(port, () => {
  console.log(`[AgeOfArena] Sunucu ayakta → ws://localhost:${port}`);
});
