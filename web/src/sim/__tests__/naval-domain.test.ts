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

  it('markIslands carves land discs and leaves the gaps as water', () => {
    const nav = new NavGrid();
    nav.markIslands([[-30, 0], [30, 0]], 10); // two islands, gap between them
    // Island centres are land:
    expect(nav.isWalkableWorld(-30, 0, 'land')).toBe(true);
    expect(nav.isWalkableWorld(30, 0, 'land')).toBe(true);
    // The gap between islands is water — land units can't cross, ships can:
    expect(nav.isWalkableWorld(0, 0, 'land')).toBe(false);
    expect(nav.isWalkableWorld(0, 0, 'water')).toBe(true);
  });

  it('ship advances on water but is clamped at the shore (never enters land)', () => {
    const nav = new NavGrid();
    nav.markWaterBeyondRadius(5); // land within r=5, water beyond
    const ms = new MovementSystem();
    const ship = mkUnit(8, 0, 'water', [0, 0]); // ship in water heading toward the land centre
    for (let i = 0; i < 12; i++) ms.tick([ship], nav, 0.2);
    // Domain-aware: the ship crosses water toward the shore (x drops from 8) but the clamp
    // stops it before land (x stays > ~4). If domain were ignored ('land' default), the
    // ship's water start cell would be unwalkable and it would never move (x stays 8) —
    // so this lower-and-upper bound distinguishes domain-aware code from domain-ignoring.
    expect(ship.x).toBeGreaterThan(4);
    expect(ship.x).toBeLessThan(7.5);
  });
});
