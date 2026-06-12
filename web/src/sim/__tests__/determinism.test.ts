/**
 * Determinism tests — Faz 14 (WEB14.test).
 * Verifies: CommandBus tick+seq ordering, command log JSON round-trip,
 * Checksum FNV-1a consistency, DMath table accuracy, SimRng stability.
 */
import { describe, it, expect } from 'vitest';
import { CommandBus } from '../CommandBus';
import { Checksum } from '../Checksum';
import { DMath } from '../DMath';
import { SimRng } from '../SimRng';
import { allocId, resetIds } from '../EntityIds';
import { UnitType, BuildingType } from '../../core/GameTypes';

// ── CommandBus ordering ──────────────────────────────────────────────────────

describe('CommandBus', () => {
  it('stamps tick and seq correctly', () => {
    const bus = new CommandBus();
    bus.advanceTick();
    bus.issue({ kind: 'stop', teamId: 0, ai: false, unitIds: [1] });
    bus.issue({ kind: 'stop', teamId: 1, ai: true,  unitIds: [2] });
    const cmds = bus.drain();
    expect(cmds).toHaveLength(2);
    expect(cmds[0].tick).toBe(1);
    expect(cmds[0].seq).toBe(0);
    expect(cmds[1].seq).toBe(0); // first cmd for team 1
  });

  it('drains in (teamId, seq) order', () => {
    const bus = new CommandBus();
    bus.advanceTick();
    bus.issue({ kind: 'stop', teamId: 2, ai: false, unitIds: [3] });
    bus.issue({ kind: 'stop', teamId: 0, ai: false, unitIds: [1] });
    bus.issue({ kind: 'stop', teamId: 1, ai: false, unitIds: [2] });
    const cmds = bus.drain();
    expect(cmds.map(c => c.teamId)).toEqual([0, 1, 2]);
  });

  it('clears pending after drain', () => {
    const bus = new CommandBus();
    bus.advanceTick();
    bus.issue({ kind: 'stop', teamId: 0, ai: false, unitIds: [1] });
    bus.drain();
    const cmds2 = bus.drain();
    expect(cmds2).toHaveLength(0);
  });

  it('log accumulates across ticks', () => {
    const bus = new CommandBus();
    bus.advanceTick();
    bus.issue({ kind: 'stop', teamId: 0, ai: false, unitIds: [1] });
    bus.drain();
    bus.advanceTick();
    bus.issue({ kind: 'stop', teamId: 0, ai: false, unitIds: [2] });
    bus.drain();
    expect(bus.getLog()).toHaveLength(2);
  });

  it('getLog JSON round-trip is lossless', () => {
    const bus = new CommandBus();
    bus.advanceTick();
    bus.issue({ kind: 'move', teamId: 0, ai: false, unitIds: [1, 2], qx: 256, qz: 512, queued: false });
    bus.issue({ kind: 'train', teamId: 1, ai: true, buildingId: 10, unitType: UnitType.Militia });
    bus.drain();
    const log = bus.getLog();
    const json = JSON.stringify(log);
    const restored = JSON.parse(json) as typeof log;
    expect(restored).toHaveLength(2);
    const [m, t] = restored;
    expect(m.kind).toBe('move');
    if (m.kind === 'move') { expect(m.qx).toBe(256); expect(m.qz).toBe(512); }
    expect(t.kind).toBe('train');
    if (t.kind === 'train') { expect(t.unitType).toBe(UnitType.Militia); }
  });

  it('same seed + same commands → same Checksum', () => {
    function run(): number {
      const bus = new CommandBus();
      for (let tick = 0; tick < 10; tick++) {
        bus.advanceTick();
        bus.issue({ kind: 'stop', teamId: tick % 3, ai: tick % 2 === 0, unitIds: [tick + 1] });
        bus.drain();
      }
      return Checksum.ofCommandLog(bus.getLog());
    }
    expect(run()).toBe(run());
  });
});

// ── Checksum (FNV-1a) ────────────────────────────────────────────────────────

describe('Checksum', () => {
  it('is deterministic for fixed inputs', () => {
    const make = () => new Checksum().feedInt(42).feedInt(1337).feedQ(3.14159).value;
    expect(make()).toBe(make());
  });

  it('differs when input changes', () => {
    const a = new Checksum().feedInt(1).value;
    const b = new Checksum().feedInt(2).value;
    expect(a).not.toBe(b);
  });

  it('feedQ rounds to q*256 consistently', () => {
    const cs1 = new Checksum().feedQ(1.5).value;
    const cs2 = new Checksum().feedInt(384).value; // 1.5 * 256 = 384
    expect(cs1).toBe(cs2);
  });
});

// ── DMath table accuracy ─────────────────────────────────────────────────────

describe('DMath', () => {
  it('sin(0) ≈ 0', () => expect(Math.abs(DMath.sin(0))).toBeLessThan(0.001));
  it('cos(0) ≈ 1', () => expect(Math.abs(DMath.cos(0) - 1)).toBeLessThan(0.001));
  it('sin(π/2) ≈ 1', () => expect(Math.abs(DMath.sin(Math.PI / 2) - 1)).toBeLessThan(0.001));
  it('cos(π) ≈ -1', () => expect(Math.abs(DMath.cos(Math.PI) + 1)).toBeLessThan(0.002));
  it('is deterministic (same call twice = same result)', () => {
    const a = DMath.sin(1.23456);
    const b = DMath.sin(1.23456);
    expect(a).toBe(b);
  });
});

// ── SimRng ───────────────────────────────────────────────────────────────────

describe('SimRng', () => {
  it('produces same sequence for same seed', () => {
    const r1 = new SimRng(1453);
    const r2 = new SimRng(1453);
    for (let i = 0; i < 100; i++) expect(r1.next()).toBe(r2.next());
  });

  it('state save/restore works', () => {
    const r = new SimRng(42);
    r.next(); r.next();
    const snap = r.state;
    const a = r.next();
    r.state = snap;
    expect(r.next()).toBe(a);
  });

  it('different seeds produce different sequences', () => {
    const r1 = new SimRng(1); const r2 = new SimRng(2);
    expect(r1.next()).not.toBe(r2.next());
  });
});

// ── EntityIds ────────────────────────────────────────────────────────────────

describe('EntityIds', () => {
  it('allocId is monotonic and starts at 1 after reset', () => {
    resetIds();
    expect(allocId()).toBe(1);
    expect(allocId()).toBe(2);
    expect(allocId()).toBe(3);
  });

  it('resetIds starts fresh', () => {
    resetIds();
    const a = allocId();
    resetIds();
    const b = allocId();
    expect(a).toBe(b);
  });
});

// ── CommandBus throughput (perf gate: ≥3000 tick/s) ─────────────────────────

describe('CommandBus throughput', () => {
  it('achieves ≥3000 bus ticks/sec with 10 cmds/tick', () => {
    const bus = new CommandBus();
    const N_TICKS = 1000;
    const CMDS_PER_TICK = 10;
    const t0 = Date.now();
    for (let t = 0; t < N_TICKS; t++) {
      bus.advanceTick();
      for (let c = 0; c < CMDS_PER_TICK; c++) {
        bus.issue({ kind: 'stop', teamId: c % 4, ai: c % 2 === 0, unitIds: [c + 1] });
      }
      bus.drain();
    }
    const ms = Date.now() - t0;
    const ticksPerSec = (N_TICKS / ms) * 1000;
    console.log(`CommandBus throughput: ${Math.round(ticksPerSec)} ticks/s (${ms}ms for ${N_TICKS} ticks)`);
    expect(ticksPerSec).toBeGreaterThan(3000);
  });
});
