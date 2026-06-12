import { WebSocketServer, WebSocket } from 'ws';
import { createServer } from 'http';
import { randomInt } from 'crypto';
import { Room, type RoomPlayer } from './Room';
import { PROTOCOL_VERSION } from '../../shared/protocol';
import type { ClientMsg } from '../../shared/protocol';
import { isRateLimited, cleanupSocket, touchRoom, removeRoom, startRoomGc } from './Limits';

const port = Number(process.env.PORT ?? 2567);
const httpServer = createServer((req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/plain', 'Access-Control-Allow-Origin': '*' });
  res.end('Age of Arena — game server OK');
});
const wss = new WebSocketServer({ server: httpServer });

const rooms = new Map<string, Room>();
const socketToPlayer = new Map<WebSocket, { playerId: string; roomCode: string }>();

function makeCode(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let code = '';
  for (let i = 0; i < 5; i++) code += chars[randomInt(chars.length)];
  return code;
}

function send(ws: WebSocket, payload: object): void {
  if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(payload));
}

function versionOk(v: unknown): boolean {
  if (!Array.isArray(v) || v.length < 2) return false;
  return v[0] === PROTOCOL_VERSION[0] && v[1] === PROTOCOL_VERSION[1];
}

wss.on('connection', (ws) => {
  const playerId = randomInt(0x7fffffff).toString(36);
  console.log(`[+] connected: ${playerId}`);

  ws.on('message', (raw) => {
    if (isRateLimited(ws)) { send(ws, { type: 'error', code: 'RATE_LIMITED', message: 'Too many messages' }); return; }
    const str = raw.toString();
    if (Buffer.byteLength(str, 'utf8') > 8192) { return; } // drop oversized

    let msg: ClientMsg;
    try { msg = JSON.parse(str); } catch { return; }

    const { type } = msg;

    if (type === 'create') {
      if (!versionOk(msg.version)) {
        send(ws, { type: 'error', code: 'VERSION_MISMATCH', message: `Expected protocol ${PROTOCOL_VERSION.join('.')}` });
        return;
      }
      let code = makeCode();
      while (rooms.has(code)) code = makeCode();
      const seed = randomInt(0x7fffffff);
      const player: RoomPlayer = { ws, id: playerId, name: msg.playerName ?? 'Player1', team: 0, ready: false };
      const room = new Room(code, playerId, seed);
      room.addPlayer(player);
      rooms.set(code, room);
      touchRoom(code);
      socketToPlayer.set(ws, { playerId, roomCode: code });
      send(ws, { type: 'room_created', roomCode: code, team: 0, playerId });
      console.log(`[Room] created: ${code} host=${player.name}`);
    }

    else if (type === 'join') {
      if (!versionOk(msg.version)) {
        send(ws, { type: 'error', code: 'VERSION_MISMATCH', message: `Expected protocol ${PROTOCOL_VERSION.join('.')}` });
        return;
      }
      const room = rooms.get(msg.roomCode);
      if (!room) { send(ws, { type: 'error', code: 'ROOM_NOT_FOUND', message: 'Room not found' }); return; }
      if (room.started) { send(ws, { type: 'error', code: 'ALREADY_STARTED', message: 'Game already started' }); return; }
      if (room.playerCount >= 4) { send(ws, { type: 'error', code: 'ROOM_FULL', message: 'Room full' }); return; }

      const team = room.playerCount;
      const player: RoomPlayer = { ws, id: playerId, name: msg.playerName ?? `Player${team + 1}`, team, ready: false };
      room.addPlayer(player);
      socketToPlayer.set(ws, { playerId, roomCode: msg.roomCode });

      send(ws, { type: 'room_joined', roomCode: msg.roomCode, team, playerId, players: room.playerList() });
      room.broadcast({ type: 'player_joined', name: player.name, team, players: room.playerList() }, playerId);
      console.log(`[Room] ${msg.roomCode} joined: ${player.name} team=${team}`);
    }

    else if (type === 'ready') {
      const ctx = socketToPlayer.get(ws);
      if (!ctx) return;
      const room = rooms.get(ctx.roomCode);
      if (!room || room.started) return;
      const p = room.getPlayer(ctx.playerId);
      if (!p) return;
      p.ready = true;
      // Host's mapType preference is stored on the room
      if (ctx.playerId === room.hostId && typeof msg.mapType === 'number') {
        room.mapType = msg.mapType;
      }
      room.broadcast({ type: 'player_ready', playerId: ctx.playerId, players: room.playerList() });
      if (room.allReady()) room.startGame(room.mapType);
    }

    else if (type === 'turn_input') {
      const ctx = socketToPlayer.get(ws);
      if (!ctx) return;
      const room = rooms.get(ctx.roomCode);
      if (!room || !room.started) return;
      touchRoom(ctx.roomCode);
      room.receiveTurnInput(ctx.playerId, msg.turn, msg.commands ?? []);
    }

    else if (type === 'checksum') {
      const ctx = socketToPlayer.get(ws);
      if (!ctx) return;
      const room = rooms.get(ctx.roomCode);
      if (!room) return;
      room.receiveChecksum(ctx.playerId, msg.turn, msg.hash);
    }

    else if (type === 'chat') {
      const ctx = socketToPlayer.get(ws);
      if (!ctx) return;
      const room = rooms.get(ctx.roomCode);
      if (!room) return;
      const p = room.getPlayer(ctx.playerId);
      if (!p) return;
      room.broadcast({ type: 'chat', name: p.name, message: String(msg.message ?? '').slice(0, 256) });
    }
  });

  ws.on('close', () => {
    cleanupSocket(ws);
    const ctx = socketToPlayer.get(ws);
    if (!ctx) return;
    socketToPlayer.delete(ws);
    const room = rooms.get(ctx.roomCode);
    if (!room) return;
    const p = room.removePlayer(ctx.playerId);
    if (p) {
      room.broadcast({ type: 'player_left', playerId: ctx.playerId, name: p.name, team: p.team });
      console.log(`[-] disconnected: ${p.name}`);
    }
    if (room.playerCount === 0) {
      rooms.delete(ctx.roomCode);
      removeRoom(ctx.roomCode);
      console.log(`[Room] ${ctx.roomCode} deleted (empty)`);
    }
  });

  ws.on('error', (err) => console.error(`[WS] error: ${err.message}`));
});

httpServer.listen(port, () => {
  console.log(`[AgeOfArena] Server running → ws://localhost:${port}`);
});

// Room GC — evict idle rooms after 30 min
startRoomGc((code) => {
  const room = rooms.get(code);
  if (room) {
    room.broadcast({ type: 'error', code: 'ROOM_EXPIRED', message: 'Room expired (idle)' });
    rooms.delete(code);
    console.log(`[Room] ${code} expired (TTL)`);
  }
});
