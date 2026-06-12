/**
 * ReplayFile — `.aoarep` serializable format.
 * A replay = command log + game setup (seed, map, civs).
 * Loading re-runs startGame with same seed then feeds all commands.
 *
 * Version triple: [schema, major, minor] — any mismatch on load rejects.
 */
import type { Command } from '../sim/Command';

export const REPLAY_VERSION: [number, number, number] = [1, 0, 0];
export const REPLAY_MAGIC = 'aoa-replay-v1';

export interface ReplaySetup {
  mapType:   number; // MapType enum value
  simSeed:   number;
  playerCiv: number; // Civilization enum
  opponents: Array<{ civ: number; difficulty: number; personality: number }>;
}

export interface AoaRep {
  magic:    typeof REPLAY_MAGIC;
  version:  [number, number, number];
  setup:    ReplaySetup;
  commands: Command[];
  /** FNV-1a checksums sampled every 30 ticks (for verification). */
  checksums: number[];
  durationTicks: number;
}

export function writeReplay(rep: AoaRep): string {
  return JSON.stringify(rep);
}

export function readReplay(json: string): AoaRep {
  const data = JSON.parse(json) as Partial<AoaRep>;
  if (data.magic !== REPLAY_MAGIC) throw new Error('Invalid replay file (wrong magic)');
  const [sv, smaj] = data.version ?? [0, 0, 0];
  const [rv, rmaj] = REPLAY_VERSION;
  if (sv !== rv || smaj !== rmaj) throw new Error(`Replay version mismatch: ${data.version} vs ${REPLAY_VERSION}`);
  return data as AoaRep;
}

// ── localStorage persistence ─────────────────────────────────────────────────

const SLOT_KEY = (slot: number) => `aoa.rep.${slot}`;

export function saveRepToSlot(slot: number, rep: AoaRep): void {
  try { localStorage.setItem(SLOT_KEY(slot), writeReplay(rep)); } catch {}
}

export function loadRepFromSlot(slot: number): AoaRep | null {
  const raw = localStorage.getItem(SLOT_KEY(slot));
  if (!raw) return null;
  try { return readReplay(raw); } catch { return null; }
}

export function listRepSlots(): number[] {
  const slots: number[] = [];
  for (let i = 0; i < localStorage.length; i++) {
    const k = localStorage.key(i);
    if (k?.startsWith('aoa.rep.')) {
      const n = parseInt(k.slice(8));
      if (!isNaN(n)) slots.push(n);
    }
  }
  return slots.sort((a, b) => a - b);
}
