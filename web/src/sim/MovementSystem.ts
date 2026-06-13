/**
 * MovementSystem — waypoint follower + soft separation.
 * Reads/writes unit.x, unit.z (plain numbers). Zero three.js imports.
 */

import { NavGrid } from './NavGrid';
import { SpatialHash } from './SpatialHash';
import { DMath } from './DMath';

export interface MoveUnit {
  x: number;
  z: number;
  velX: number;
  velZ: number;
  moveSpeed: number;
  teamId: number;
  alive: boolean;
  isGarrisoned: boolean;
  waypoints: [number, number][];
  waypointIdx: number;
  // Optional: facing angle written by MovementSystem, read by view layer
  facingAngle: number;
}

const ARRIVAL_TOLERANCE = 0.25;   // world units — within this of waypoint = arrived
const SEP_RADIUS        = 0.7;    // push units apart below this distance
const SEP_STRENGTH      = 0.3;    // how hard to push

export class MovementSystem {
  private readonly _hash = new SpatialHash<MoveUnit>();
  private readonly _neighbours: MoveUnit[] = []; // reused per-unit separation scratch

  tick(units: MoveUnit[], nav: NavGrid, dt: number): void {
    // Rebuild spatial hash for this tick
    const alive = units.filter(u => u.alive && !u.isGarrisoned);
    this._hash.rebuild(alive);

    for (const u of alive) {
      if (u.waypoints.length === 0) { u.velX = 0; u.velZ = 0; continue; }

      const [wx, wz] = u.waypoints[u.waypointIdx];
      const dx = wx - u.x;
      const dz = wz - u.z;
      const dist2 = dx * dx + dz * dz;

      if (dist2 < ARRIVAL_TOLERANCE * ARRIVAL_TOLERANCE) {
        // Arrived at current waypoint
        u.waypointIdx++;
        if (u.waypointIdx >= u.waypoints.length) {
          u.waypoints = [];
          u.waypointIdx = 0;
          u.velX = 0;
          u.velZ = 0;
        }
        continue;
      }

      const dist  = Math.sqrt(dist2);
      const step  = Math.min(dist, u.moveSpeed * dt);
      const nx    = dx / dist;
      const nz    = dz / dist;

      let newX = u.x + nx * step;
      let newZ = u.z + nz * step;

      // Clamp to walkable cell — if new pos blocked try cardinal slides
      const [cx, cz] = nav.worldToCell(newX, newZ);
      if (!nav.isWalkable(cx, cz)) {
        const [cx2] = nav.worldToCell(newX, u.z);
        const [, cz2] = nav.worldToCell(u.x, newZ);
        if (nav.isWalkable(cx2, cz)) { newZ = u.z; }
        else if (nav.isWalkable(cx, cz2)) { newX = u.x; }
        else { newX = u.x; newZ = u.z; }
      }

      u.x = newX;
      u.z = newZ;
      u.velX = nx * u.moveSpeed;
      u.velZ = nz * u.moveSpeed;

      // Facing angle (atan2 in XZ plane) — view layer will sync to root.rotation.y
      u.facingAngle = DMath.atan2(dx, dz);
    }

    // ── Soft separation (after all movement) ─────────────────────────────────
    this._hash.rebuild(alive); // refresh positions

    const neighbours = this._neighbours;
    for (const u of alive) {
      neighbours.length = 0;
      this._hash.query(u.x, u.z, SEP_RADIUS, neighbours);

      for (let ni = 0; ni < neighbours.length; ni++) {
        const other = neighbours[ni];
        if (other === u) continue;
        const dx  = u.x - other.x;
        const dz  = u.z - other.z;
        const d2  = dx * dx + dz * dz;
        if (d2 >= SEP_RADIUS * SEP_RADIUS) continue;
        let px: number, pz: number;
        if (d2 < 0.0001) {
          // Exact overlap — push in alternating cardinal directions (deterministic via index)
          const dir = (ni & 1) ? 1 : -1;
          px = SEP_STRENGTH * 0.5 * dir;
          pz = SEP_STRENGTH * 0.5 * -dir;
        } else {
          const d   = Math.sqrt(d2);
          const push = ((SEP_RADIUS - d) / SEP_RADIUS) * SEP_STRENGTH;
          px   = (dx / d) * push;
          pz   = (dz / d) * push;
        }

        // Only push to walkable cells (no cell-tuple allocation)
        const nx2 = u.x + px; const nz2 = u.z + pz;
        if (nav.isWalkableWorld(nx2, nz2)) { u.x = nx2; u.z = nz2; }
      }
    }
  }
}
