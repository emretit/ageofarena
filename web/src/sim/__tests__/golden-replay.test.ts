/**
 * Golden replay regression — Faz 15 (WEB15.verify).
 *
 * Runs a deterministic headless battle (no renderer) and checksums the final sim
 * state. Two guarantees:
 *   1. Determinism: the same scenario run twice produces the identical checksum.
 *   2. Regression: the checksum is pinned via an inline snapshot — any intentional
 *      balance change (unit atk/hp/armor, movement, combat timing) flips it and
 *      fails the test, exactly as a golden replay should.
 *
 * Headless is possible because Unit/Building build only THREE primitives
 * (Group/Mesh/Color), never a WebGLRenderer or DOM canvas.
 */
import { describe, it, expect } from 'vitest';
import * as THREE from 'three';
import { Unit } from '../../game/Unit';
import { CombatSystem } from '../../game/CombatSystem';
import { MovementSystem } from '../MovementSystem';
import { PathQueue } from '../PathQueue';
import { navGrid } from '../NavGrid';
import { Checksum } from '../Checksum';
import { resetIds } from '../EntityIds';
import { resetDiplomacy } from '../../core/Diplomacy';
import { UnitType } from '../../core/GameTypes';
import { orderAttackMove } from '../../game/Orders';

/** Run a fixed headless battle and return an FNV checksum of the final state. */
function runBattle(perSide: number, ticks: number): number {
  resetIds();
  resetDiplomacy();
  navGrid.reset();

  const scene = new THREE.Scene();
  const units: Unit[] = [];
  for (let i = 0; i < perSide; i++) {
    const row = Math.floor(i / 5);
    const col = i % 5;
    const type = i % 2 ? UnitType.Archer : UnitType.Militia;
    units.push(new Unit(scene, new THREE.Vector3(-18 + col, 0, row * 1.2), 0, type));
    units.push(new Unit(scene, new THREE.Vector3( 18 - col, 0, row * 1.2), 1, type));
  }

  const combat = new CombatSystem();
  const movement = new MovementSystem();
  const pathQueue = new PathQueue();
  const noBuildings: never[] = [];

  // Each side attack-moves at the other's start line.
  orderAttackMove(units.filter(u => u.teamId === 0), 18, 0, pathQueue);
  orderAttackMove(units.filter(u => u.teamId === 1), -18, 0, pathQueue);

  const dt = 1 / 30;
  for (let t = 0; t < ticks; t++) {
    pathQueue.tick(navGrid, dt);
    movement.tick(units, navGrid, dt);
    combat.tick(units, noBuildings, dt);
  }

  // Checksum the canonical final state.
  const cs = new Checksum();
  for (const u of units) {
    cs.feedInt(u.alive ? 1 : 0).feedQ(u.x).feedQ(u.z).feedInt(Math.max(0, Math.round(u.hp)));
  }
  return cs.value >>> 0;
}

describe('golden replay', () => {
  it('is deterministic: same scenario twice → identical checksum', () => {
    expect(runBattle(10, 300)).toBe(runBattle(10, 300));
  });

  it('matches the pinned 10v10 golden checksum (balance regression guard)', () => {
    // Pinned by inline snapshot — flips on any intentional balance change.
    expect(runBattle(10, 300)).toMatchInlineSnapshot(`343308238`);
  });

  it('matches the pinned 4v4 short-battle golden checksum', () => {
    expect(runBattle(4, 150)).toMatchInlineSnapshot(`803690722`);
  });

  it('matches the pinned 20v20 large-battle golden checksum', () => {
    expect(runBattle(20, 400)).toMatchInlineSnapshot(`3661716493`);
  });
});
