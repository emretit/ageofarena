/**
 * Naval domain tests — Faz 3 (water domain movement).
 * Pins: NavGrid water/land cell separation + MovementSystem honouring per-unit domain
 * (ships move on water & are kept off land; land units are kept off water).
 */
import { describe, it, expect } from 'vitest';
import { NavGrid, type Domain } from '../NavGrid';
import { MovementSystem, type MoveUnit } from '../MovementSystem';

function mkUnit(x: number, z: number, domain: Domain, goal: [number, number]): MoveUnit {
  return {
    x, z, velX: 0, velZ: 0, moveSpeed: 5, teamId: 0,
    alive: true, isGarrisoned: false,
    waypoints: [goal], waypointIdx: 0, domain, facingAngle: 0,
  };
}

describe('naval domain', () => {
  it('separates water and land cells by domain', () => {
    const nav = new NavGrid();
    nav.markWaterBeyondRadius(10); // beyond r=10 from map centre is water
    // (15,0): far from centre → water; (0,0): centre → land
    expect(nav.isWalkableWorld(15, 0, 'water')).toBe(true);
    expect(nav.isWalkableWorld(15, 0, 'land')).toBe(false);
    expect(nav.isWalkableWorld(0, 0, 'land')).toBe(true);
    expect(nav.isWalkableWorld(0, 0, 'water')).toBe(false);
  });

  it('ship advances toward a water waypoint (not clamped back)', () => {
    const nav = new NavGrid();
    nav.markWaterBeyondRadius(10);
    const ms = new MovementSystem();
    const ship = mkUnit(15, 0, 'water', [15, 8]); // start + goal both water
    ms.tick([ship], nav, 0.2);
    expect(ship.z).toBeGreaterThan(0); // moved toward the waypoint
  });

  it('land unit is clamped at the shore and never enters water', () => {
    const nav = new NavGrid();
    nav.markWaterBeyondRadius(5); // water boundary at ~r=5
    const ms = new MovementSystem();
    const land = mkUnit(4, 0, 'land', [12, 0]); // heads from land into deep water
    for (let i = 0; i < 12; i++) ms.tick([land], nav, 0.2);
    expect(land.x).toBeLessThan(6); // cannot cross the shore into water
  });
});
