/**
 * Age of Arena — Lockstep Network Protocol v1
 * JSON-over-WebSocket. Client↔Server messages.
 *
 * Version triple: [schema, major, minor].
 * Server rejects join if schema or major differ.
 */

export const PROTOCOL_VERSION: [number, number, number] = [1, 0, 0];

// ── Serialisable command payload ─────────────────────────────────────────────

/** A command stripped of tick/seq stamps for wire transport. */
export interface WireCommand {
  kind: string;
  teamId: number;
  [key: string]: unknown;
}

// ── Client → Server ──────────────────────────────────────────────────────────

export interface CreateMsg {
  type: 'create';
  playerName: string;
  version: [number, number, number];
}

export interface JoinMsg {
  type: 'join';
  roomCode: string;
  playerName: string;
  version: [number, number, number];
  /** Join as a view-only spectator — receives turns but cannot issue commands. */
  spectate?: boolean;
}

export interface ReadyMsg {
  type: 'ready';
  /** Host may include preferred map type; non-host sends are ignored. */
  mapType?: number;
}

export interface TurnInputMsg {
  type: 'turn_input';
  turn: number;
  /** Commands issued by this player during that turn window. */
  commands: WireCommand[];
}

export interface ChecksumMsg {
  type: 'checksum';
  turn: number;
  hash: number;
}

export interface ChatMsg {
  type: 'chat';
  message: string;
}

export type ClientMsg = CreateMsg | JoinMsg | ReadyMsg | TurnInputMsg | ChecksumMsg | ChatMsg;

// ── Server → Client ──────────────────────────────────────────────────────────

export interface PlayerInfo {
  id: string;
  name: string;
  team: number;
  ready: boolean;
}

export interface RoomCreatedMsg {
  type: 'room_created';
  roomCode: string;
  team: number;
  playerId: string;
}

export interface RoomJoinedMsg {
  type: 'room_joined';
  roomCode: string;
  team: number;
  playerId: string;
  players: PlayerInfo[];
  /** True when the joiner is a spectator (view-only, no team). */
  spectator?: boolean;
}

export interface PlayerJoinedMsg {
  type: 'player_joined';
  name: string;
  team: number;
  players: PlayerInfo[];
}

export interface PlayerReadyMsg {
  type: 'player_ready';
  playerId: string;
  players: PlayerInfo[];
}

export interface PlayerLeftMsg {
  type: 'player_left';
  playerId: string;
  name: string;
  team: number;
}

/** Sent to all clients when game starts. */
export interface GameStartMsg {
  type: 'game_start';
  seed: number;
  players: PlayerInfo[];
  mapType: number;
}

/** Confirmed turn: all players' inputs for a given turn number. */
export interface TurnMsg {
  type: 'turn';
  turn: number;
  inputs: Array<{ playerId: string; commands: WireCommand[] }>;
}

/** Server is still waiting for some players to submit turn T. */
export interface StallMsg {
  type: 'stall';
  turn: number;
  waitingFor: string[]; // player ids
}

export interface DesyncMsg {
  type: 'desync';
  turn: number;
}

export interface ChatServerMsg {
  type: 'chat';
  name: string;
  message: string;
}

export interface ErrorMsg {
  type: 'error';
  message: string;
  code: string;
}

export interface PingMsg { type: 'ping'; }
export interface PongMsg { type: 'pong'; ts: number; }

export type ServerMsg =
  | RoomCreatedMsg
  | RoomJoinedMsg
  | PlayerJoinedMsg
  | PlayerReadyMsg
  | PlayerLeftMsg
  | GameStartMsg
  | TurnMsg
  | StallMsg
  | DesyncMsg
  | ChatServerMsg
  | ErrorMsg
  | PingMsg
  | PongMsg;
