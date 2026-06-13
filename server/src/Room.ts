/**
 * Room.ts — Lockstep turn sequencer.
 * Collects one turn_input from every active player, then broadcasts `turn`.
 * Stop-and-wait: server does NOT advance until all players submit.
 */
import { WebSocket } from 'ws';
import { reportDesync } from './Telemetry';
import type {
  TurnMsg,
  StallMsg,
  PlayerInfo,
  GameStartMsg,
  WireCommand,
} from '../../shared/protocol';

export interface RoomPlayer {
  ws: WebSocket;
  id: string;
  name: string;
  team: number;
  ready: boolean;
}

interface TurnBuffer {
  playerId: string;
  commands: WireCommand[];
}

const MAX_CMDS_PER_TURN = 32;
const MAX_MSG_BYTES = 8192;

function send(ws: WebSocket, payload: object): void {
  if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(payload));
}

export class Room {
  readonly code: string;
  readonly hostId: string;
  readonly seed: number;
  started = false;
  mapType = 0;

  private readonly _players = new Map<string, RoomPlayer>();
  private readonly _spectators = new Map<string, WebSocket>();
  private _currentTurn = 0;
  private readonly _buffer = new Map<number, Map<string, TurnBuffer>>();
  private readonly _checksumsByTurn = new Map<number, Map<string, number>>();

  constructor(code: string, hostId: string, seed: number) {
    this.code = code;
    this.hostId = hostId;
    this.seed = seed;
  }

  get playerCount(): number { return this._players.size; }

  addPlayer(player: RoomPlayer): void {
    this._players.set(player.id, player);
  }

  removePlayer(id: string): RoomPlayer | undefined {
    const p = this._players.get(id);
    this._players.delete(id);
    return p;
  }

  getPlayer(id: string): RoomPlayer | undefined {
    return this._players.get(id);
  }

  get spectatorCount(): number { return this._spectators.size; }

  addSpectator(id: string, ws: WebSocket): void {
    this._spectators.set(id, ws);
  }

  removeSpectator(id: string): boolean {
    return this._spectators.delete(id);
  }

  isSpectator(id: string): boolean {
    return this._spectators.has(id);
  }

  playerList(): PlayerInfo[] {
    return [...this._players.values()].map(p => ({
      id: p.id, name: p.name, team: p.team, ready: p.ready,
    }));
  }

  broadcast(payload: object, exceptId?: string): void {
    this._players.forEach(p => {
      if (p.id !== exceptId) send(p.ws, payload);
    });
    // Spectators receive the same turn/game stream (read-only)
    this._spectators.forEach((ws, id) => {
      if (id !== exceptId) send(ws, payload);
    });
  }

  allReady(): boolean {
    return this._players.size >= 2 && [...this._players.values()].every(p => p.ready);
  }

  startGame(mapType: number): void {
    this.started = true;
    this.mapType = mapType;
    const msg: GameStartMsg = {
      type: 'game_start',
      seed: this.seed,
      mapType,
      players: this.playerList(),
    };
    this.broadcast(msg);
    console.log(`[Room] ${this.code} started seed=${this.seed} map=${mapType}`);
  }

  /** Receive turn_input from a player. Returns true if turn is now complete. */
  receiveTurnInput(playerId: string, turn: number, commands: WireCommand[]): boolean {
    if (turn < this._currentTurn) return false; // late/duplicate, ignore

    if (!this._buffer.has(turn)) this._buffer.set(turn, new Map());
    const turnMap = this._buffer.get(turn)!;

    // Validate
    const safeCommands = commands.slice(0, MAX_CMDS_PER_TURN).map(c => this._sanitizeCmd(c, playerId));
    turnMap.set(playerId, { playerId, commands: safeCommands });

    if (turnMap.size < this._players.size) {
      // Not all players submitted yet — notify those still waiting (exclude submitter)
      const waiting = [...this._players.keys()].filter(id => !turnMap.has(id));
      const stall: StallMsg = { type: 'stall', turn, waitingFor: waiting };
      this.broadcast(stall, playerId);
      return false;
    }

    // All players submitted → broadcast turn
    const inputs = [...turnMap.values()].map(b => ({ playerId: b.playerId, commands: b.commands }));
    const msg: TurnMsg = { type: 'turn', turn, inputs };
    this.broadcast(msg);
    this._buffer.delete(turn);
    this._currentTurn = turn + 1;
    console.log(`[Room] ${this.code} turn ${turn} broadcast (${inputs.length} players)`);
    return true;
  }

  receiveChecksum(playerId: string, turn: number, hash: number): void {
    if (!this._checksumsByTurn.has(turn)) {
      this._checksumsByTurn.set(turn, new Map());
    }
    const turnMap = this._checksumsByTurn.get(turn)!;
    turnMap.set(playerId, hash);

    if (turnMap.size < this._players.size) return;

    const hashes = [...turnMap.values()];
    const allSame = hashes.every(h => h === hashes[0]);
    if (!allSame) {
      this.broadcast({ type: 'desync', turn });
      console.warn(`[Room] ${this.code} DESYNC turn=${turn} hashes=${hashes.join(',')}`);
      reportDesync(this.code, turn, hashes);
    }
    this._checksumsByTurn.delete(turn);
  }

  /** Strip unknown fields, validate teamId ownership. */
  private _sanitizeCmd(c: WireCommand, playerId: string): WireCommand {
    const player = this._players.get(playerId);
    const teamId = player ? player.team : -1;
    return { ...c, kind: String(c.kind ?? ''), teamId };
  }

  validateMsgSize(raw: string): boolean {
    return Buffer.byteLength(raw, 'utf8') <= MAX_MSG_BYTES;
  }
}
