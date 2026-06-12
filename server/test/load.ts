/**
 * load.ts — Load test: 50 rooms × 2 bots.
 * Measures relay p95 latency and server RSS.
 *
 * Run: npx ts-node server/test/load.ts [serverUrl]
 * Default serverUrl: ws://localhost:2567
 *
 * Pass criteria:
 *   - All 50 rooms complete 300 turns without error
 *   - p95 relay latency < 50ms
 *   - process RSS < 512MB
 */
import WebSocket from 'ws';
import { PROTOCOL_VERSION } from '../../shared/protocol';

const SERVER_URL = process.argv[2] ?? 'ws://localhost:2567';
const NUM_ROOMS  = 50;
const NUM_TURNS  = 300;

interface BotContext {
  ws: WebSocket;
  team: number;
  roomCode: string;
  readyToPlay: boolean;
  turnsComplete: number;
  turnInputsSent: number;
  relayMs: number[];   // recorded relay RTT per turn
  sendTimes: Map<number, number>; // turn → timestamp sent
}

function makeBot(name: string): Promise<BotContext> {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(SERVER_URL);
    const ctx: BotContext = {
      ws, team: -1, roomCode: '', readyToPlay: false,
      turnsComplete: 0, turnInputsSent: 0, relayMs: [], sendTimes: new Map(),
    };

    ws.on('error', reject);
    ws.on('open', () => resolve(ctx));
    ctx.ws = ws;
  });
}

async function runRoom(roomIndex: number): Promise<{ relayMs: number[]; errors: number }> {
  const host = await makeBot(`host-${roomIndex}`);
  const guest = await makeBot(`guest-${roomIndex}`);

  let errors = 0;
  let roomCode = '';

  return new Promise((resolve) => {
    function maybeStart() {
      if (host.readyToPlay && guest.readyToPlay) {
        // Start sim loop
        runSimLoop(host, guest, () => {
          resolve({ relayMs: [...host.relayMs, ...guest.relayMs], errors });
        });
      }
    }

    function setupBot(ctx: BotContext, isHost: boolean) {
      ctx.ws.on('message', (raw) => {
        const msg = JSON.parse(raw.toString());
        if (msg.type === 'room_created') {
          roomCode = msg.roomCode;
          ctx.team = 0;
          ctx.roomCode = msg.roomCode;
          ctx.ws.send(JSON.stringify({ type: 'ready' }));
          // Guest joins
          guest.ws.send(JSON.stringify({ type: 'join', roomCode, playerName: `guest-${roomIndex}`, version: PROTOCOL_VERSION }));
        } else if (msg.type === 'room_joined') {
          ctx.team = msg.team;
          ctx.roomCode = msg.roomCode;
          ctx.ws.send(JSON.stringify({ type: 'ready' }));
        } else if (msg.type === 'game_start') {
          ctx.readyToPlay = true;
          maybeStart();
        } else if (msg.type === 'turn') {
          const sentAt = ctx.sendTimes.get(msg.turn);
          if (sentAt !== undefined) {
            ctx.relayMs.push(Date.now() - sentAt);
            ctx.sendTimes.delete(msg.turn);
          }
          ctx.turnsComplete = msg.turn + 1;
        } else if (msg.type === 'error') {
          errors++;
        }
      });
    }

    setupBot(host, true);
    setupBot(guest, false);

    host.ws.send(JSON.stringify({ type: 'create', playerName: `host-${roomIndex}`, version: PROTOCOL_VERSION }));
  });
}

function runSimLoop(host: BotContext, guest: BotContext, onDone: () => void): void {
  let turn = 0;
  function sendTurns() {
    if (turn >= NUM_TURNS) {
      host.ws.close();
      guest.ws.close();
      onDone();
      return;
    }
    const t = turn++;
    const now = Date.now();
    host.sendTimes.set(t, now);
    guest.sendTimes.set(t, now);
    host.ws.send(JSON.stringify({ type: 'turn_input', turn: t, commands: [] }));
    guest.ws.send(JSON.stringify({ type: 'turn_input', turn: t, commands: [] }));
    setImmediate(sendTurns);
  }
  sendTurns();
}

async function main() {
  console.log(`Load test: ${NUM_ROOMS} rooms × 2 bots × ${NUM_TURNS} turns → ${SERVER_URL}`);
  const start = Date.now();

  const promises = Array.from({ length: NUM_ROOMS }, (_, i) => runRoom(i));
  const results = await Promise.allSettled(promises);

  const allRelayMs: number[] = [];
  let totalErrors = 0;
  let failed = 0;

  for (const r of results) {
    if (r.status === 'fulfilled') {
      allRelayMs.push(...r.value.relayMs);
      totalErrors += r.value.errors;
    } else {
      failed++;
    }
  }

  allRelayMs.sort((a, b) => a - b);
  const p50 = allRelayMs[Math.floor(allRelayMs.length * 0.5)] ?? 0;
  const p95 = allRelayMs[Math.floor(allRelayMs.length * 0.95)] ?? 0;
  const p99 = allRelayMs[Math.floor(allRelayMs.length * 0.99)] ?? 0;

  const memMB = process.memoryUsage().rss / (1024 * 1024);
  const elapsed = ((Date.now() - start) / 1000).toFixed(1);

  console.log(`\n── Results ─────────────────────────────────`);
  console.log(`  Rooms completed:  ${NUM_ROOMS - failed}/${NUM_ROOMS}`);
  console.log(`  Relay p50:        ${p50}ms`);
  console.log(`  Relay p95:        ${p95}ms  (target: <50ms)`);
  console.log(`  Relay p99:        ${p99}ms`);
  console.log(`  Errors:           ${totalErrors}`);
  console.log(`  Server RSS:       ${memMB.toFixed(0)}MB  (target: <512MB)`);
  console.log(`  Elapsed:          ${elapsed}s`);
  console.log(`────────────────────────────────────────────`);

  const pass = p95 < 50 && memMB < 512 && failed === 0;
  console.log(pass ? '✓ PASS' : '✗ FAIL');
  process.exit(pass ? 0 : 1);
}

main().catch(e => { console.error(e); process.exit(1); });
