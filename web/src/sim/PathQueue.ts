/**
 * PathQueue — batches A* path requests per sim tick.
 * Budget: max 8 paths per tick, max 4000 A* nodes total per tick.
 * Zero three.js imports — pure sim layer.
 */

import { NavGrid, Domain } from './NavGrid';
import { findPath } from './Pathfinder';
import type { MoveUnit } from './MovementSystem';

const MAX_PER_TICK  = 8;
const MAX_NODES     = 4000;
const REPATH_DELAY  = 0.5; // seconds between re-path requests for same unit

interface PathRequest {
  unit:    MoveUnit;
  goalX:   number;
  goalZ:   number;
  domain:  Domain;
  teamId:  number;
  priority: number;          // higher = processed first
}

export class PathQueue {
  private _queue: PathRequest[] = [];
  private readonly _lastRepath = new Map<MoveUnit, number>();
  private _elapsed = 0;

  /** Request a path for `unit`. Replaces any existing pending request for this unit. */
  request(
    unit:   MoveUnit,
    goalX:  number,
    goalZ:  number,
    domain: Domain = 'land',
    teamId  = -1,
    priority = 0,
  ): void {
    // Repath throttle
    const last = this._lastRepath.get(unit) ?? -999;
    if (this._elapsed - last < REPATH_DELAY) return;

    // Replace existing request for this unit
    const existing = this._queue.findIndex(r => r.unit === unit);
    const req: PathRequest = { unit, goalX, goalZ, domain, teamId, priority };
    if (existing >= 0) this._queue[existing] = req;
    else this._queue.push(req);
  }

  /** Force a request ignoring throttle (e.g. fresh command from player). */
  requestForced(
    unit:   MoveUnit,
    goalX:  number,
    goalZ:  number,
    domain: Domain = 'land',
    teamId  = -1,
    priority = 1,
  ): void {
    this._lastRepath.delete(unit); // reset throttle
    this.request(unit, goalX, goalZ, domain, teamId, priority);
  }

  /** Process up to budget per tick. Call once per sim step. */
  tick(nav: NavGrid, dt: number): void {
    this._elapsed += dt;

    if (this._queue.length === 0) return;

    // Sort: higher priority first, then FIFO (stable via index preserved by sort stability)
    this._queue.sort((a, b) => b.priority - a.priority);

    let processed  = 0;
    let nodesBudget = MAX_NODES;

    while (this._queue.length > 0 && processed < MAX_PER_TICK && nodesBudget > 0) {
      const req = this._queue.shift()!;
      const { unit, goalX, goalZ, domain, teamId } = req;

      if (!unit.alive) continue; // unit died while waiting

      const nodesForThis = Math.min(nodesBudget, 4000);
      const result = findPath(unit.x, unit.z, goalX, goalZ, nav, domain, teamId, nodesForThis);

      // Estimate nodes used: rough heuristic based on path length
      nodesBudget -= Math.max(50, result.waypoints.length * 20);

      if (result.waypoints.length > 0) {
        unit.waypoints    = result.waypoints;
        unit.waypointIdx  = 0;
      }

      this._lastRepath.set(unit, this._elapsed);
      processed++;
    }
  }

  /** Remove stale entries for dead units. */
  prune(): void {
    this._queue = this._queue.filter(r => r.unit.alive);
    for (const [u] of this._lastRepath) {
      if (!u.alive) this._lastRepath.delete(u);
    }
  }

  get pendingCount(): number { return this._queue.length; }
}
