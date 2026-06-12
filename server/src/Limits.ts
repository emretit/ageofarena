/**
 * Limits.ts — rate limiting and room TTL for the game server.
 * Per-IP message rate, per-socket burst cap, room idle TTL.
 */
import { WebSocket } from 'ws';

const MSG_WINDOW_MS  = 1000;   // per-second window
const MSG_LIMIT      = 60;     // max messages per second per socket
const BURST_LIMIT    = 10;     // burst buffer per window
const ROOM_TTL_MS    = 30 * 60 * 1000;  // 30 minutes idle → room GC

interface RateState {
  count: number;
  windowStart: number;
}

const rates = new Map<WebSocket, RateState>();

/** Returns true if this socket has exceeded its rate limit. Tracks state internally. */
export function isRateLimited(ws: WebSocket): boolean {
  const now = Date.now();
  let state = rates.get(ws);
  if (!state) {
    state = { count: 0, windowStart: now };
    rates.set(ws, state);
  }

  if (now - state.windowStart >= MSG_WINDOW_MS) {
    state.count = 0;
    state.windowStart = now;
  }

  state.count++;
  return state.count > MSG_LIMIT + BURST_LIMIT;
}

/** Clean up tracking state when socket closes. */
export function cleanupSocket(ws: WebSocket): void {
  rates.delete(ws);
}

// ── Room TTL ─────────────────────────────────────────────────────────────────

type RoomGcCallback = (roomCode: string) => void;

const roomActivity = new Map<string, number>(); // roomCode → last activity ms

export function touchRoom(roomCode: string): void {
  roomActivity.set(roomCode, Date.now());
}

export function removeRoom(roomCode: string): void {
  roomActivity.delete(roomCode);
}

/** Start periodic GC. Calls onExpired for each idle room. Returns cleanup fn. */
export function startRoomGc(onExpired: RoomGcCallback): () => void {
  const interval = setInterval(() => {
    const now = Date.now();
    for (const [code, last] of roomActivity.entries()) {
      if (now - last > ROOM_TTL_MS) {
        onExpired(code);
        roomActivity.delete(code);
      }
    }
  }, 60_000); // check every minute

  return () => clearInterval(interval);
}
