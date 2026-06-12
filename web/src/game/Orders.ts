/**
 * Orders — single command gateway for player + AI.
 * All movement/attack/gather/stop orders flow through here.
 * Faz 13 will promote these to serializable Command objects; for now they are
 * direct executor calls behind a clean API.
 */

import type { Unit } from './Unit';
import type { Building } from './Building';
import type { ResourceNode } from './ResourceNode';
import type { GatherSystem } from './GatherSystem';
import type { CombatSystem } from './CombatSystem';
import type { PathQueue } from '../sim/PathQueue';
import { getFormationOffsets, FormationType } from '../sim/Formation';

export { FormationType };

// ── Move ──────────────────────────────────────────────────────────────────────

export function orderMove(
  units: Unit[],
  goalX: number, goalZ: number,
  queue: PathQueue,
  formation = FormationType.Grid,
): void {
  const live = units.filter(u => u.alive && !u.isGarrisoned);
  if (live.length === 0) return;

  const offsets = getFormationOffsets(live.length, formation);
  for (let i = 0; i < live.length; i++) {
    const u = live[i];
    u.attackTarget         = null;
    u.attackTargetBuilding = null;
    const [ox, oz] = offsets[i] ?? [0, 0];
    // Use forced request so player commands bypass throttle
    queue.requestForced(u, goalX + ox, goalZ + oz, 'land', u.teamId, 1);
  }
}

// ── Stop ──────────────────────────────────────────────────────────────────────

export function orderStop(units: Unit[]): void {
  for (const u of units) {
    u.waypoints            = [];
    u.waypointIdx          = 0;
    u.velX                 = 0;
    u.velZ                 = 0;
    u.attackTarget         = null;
    u.attackTargetBuilding = null;
    u.stopMoving();
  }
}

// ── Attack ────────────────────────────────────────────────────────────────────

export function orderAttackUnit(
  units: Unit[], target: Unit,
  combat: CombatSystem, queue: PathQueue,
): void {
  for (const u of units) {
    if (!u.alive || u.isGarrisoned) continue;
    u.waypoints = []; u.waypointIdx = 0;
    combat.attackUnit(u, target);
    queue.requestForced(u, target.x, target.z, 'land', u.teamId, 1);
  }
}

export function orderAttackBuilding(
  units: Unit[], target: Building,
  combat: CombatSystem, queue: PathQueue,
): void {
  for (const u of units) {
    if (!u.alive || u.isGarrisoned) continue;
    u.waypoints = []; u.waypointIdx = 0;
    combat.attackBuilding(u, target);
    queue.requestForced(u, target.pos.x, target.pos.z, 'land', u.teamId, 1);
  }
}

// ── Gather ────────────────────────────────────────────────────────────────────

export function orderGather(
  units: Unit[], node: ResourceNode,
  buildings: Building[], gather: GatherSystem, queue: PathQueue,
): void {
  for (const u of units) {
    if (!u.alive || !u.gathers || u.isGarrisoned) continue;
    u.waypoints = []; u.waypointIdx = 0;
    gather.assignGather(u, node, buildings);
    // Queue path to node — overrides direct moveTo from assignGather
    queue.requestForced(u, node.root.position.x, node.root.position.z, 'land', u.teamId, 1);
  }
}
