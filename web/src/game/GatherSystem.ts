/**
 * GatherSystem.cs port — drives the villager gather loop:
 * walk to node → harvest at interval → carry back to dropoff → deposit → repeat.
 */
import * as THREE from "three";
import { ResourceKind, UnitState } from "../core/GameTypes";
import { ResourceManager } from "../core/ResourceManager";
import { getTeamBonus } from "../core/CivState";
import { navGrid } from "../sim/NavGrid";
import type { Unit } from "./Unit";
import type { ResourceNode } from "./ResourceNode";
import type { Building } from "./Building";

const CARRY_CAPACITY = 10;
const DROPOFF_RANGE  = 2.5;

function gatherRangeFor(kind: ResourceKind): number {
  switch (kind) {
    case ResourceKind.Wood:  return 1.8;
    case ResourceKind.Gold:  return 2.2;
    case ResourceKind.Stone: return 2.2;
    default:                 return 1.4;
  }
}

/** Seconds between gather ticks — BAL.eco values matching Unity GatherSystem. */
export function gatherIntervalFor(kind: ResourceKind): number {
  switch (kind) {
    case ResourceKind.Food:  return 1.0;
    case ResourceKind.Gold:  return 1.1;
    case ResourceKind.Stone: return 1.1;
    case ResourceKind.Wood:  return 1.25;
    default:                 return 1.1;
  }
}

export class GatherSystem {
  /** Called on each successful harvest tick (GatherHit SFX seam). */
  onGatherTick: (() => void) | null = null;

  /** Per-villager gather timer (seconds until next harvest tick). */
  private readonly timers = new Map<Unit, number>();

  assignGather(v: Unit, node: ResourceNode, buildings: Building[] = []) {
    if (!v.gathers || node.depleted || !node.hasRoom) return;

    // Release previous node slot (covers both switch-node and same-node re-assign)
    if (v.gatherTarget) {
      v.gatherTarget.currentGatherers = Math.max(0, v.gatherTarget.currentGatherers - 1);
    }

    node.currentGatherers++;
    v.gatherTarget = node;
    v.carryKind = node.kind;
    v.carryAmount = 0;
    this.timers.set(v, 0);
    v.dropoffBuilding = this._nearestDropoff(v, node.kind, buildings) ?? null;

    const approachRange = gatherRangeFor(node.kind) * 0.7;
    v.moveTo(this._approachPoint(node.root.position, v.pos, approachRange));
    v.state = UnitState.Gathering;
  }

  tick(
    units: Unit[],
    buildings: Building[],
    teamRes: ResourceManager[],
    scene: THREE.Scene,
    dt: number,
  ) {
    for (const v of units) {
      if (!v.alive || !v.gathers) continue;
      if (v.state !== UnitState.Gathering &&
          v.state !== UnitState.ReturningToDropoff) continue;

      const node = v.gatherTarget;
      if (!node) { v.state = UnitState.Idle; continue; }

      if (v.state === UnitState.Gathering) {
        this._tickGather(v, node, buildings, teamRes, scene, dt);
      } else {
        this._tickReturn(v, node, buildings, teamRes, dt);
      }
    }
  }

  private _tickGather(
    v: Unit,
    node: ResourceNode,
    buildings: Building[],
    teamRes: ResourceManager[],
    scene: THREE.Scene,
    dt: number,
  ) {
    // Walk to node first
    const dist = v.pos.distanceTo(node.root.position);
    if (dist > gatherRangeFor(node.kind)) {
      if (!v.isAtTarget()) return; // still walking
      // Arrived but still out of range — nudge closer
      v.moveTo(this._approachPoint(node.root.position, v.pos, gatherRangeFor(node.kind) * 0.6));
      return;
    }

    v.stopMoving();

    if (node.depleted) {
      node.currentGatherers = Math.max(0, node.currentGatherers - 1);
      v.gatherTarget = null;
      v.state = UnitState.Idle;
      if (node.destroyOnDeplete) node.remove(scene);
      return;
    }

    // Advance gather timer
    let timer = (this.timers.get(v) ?? 0) + dt;
    const civBonus = getTeamBonus(v.teamId);
    const rm = teamRes[v.teamId];
    const gatherMult = node.kind === ResourceKind.Food
      ? (civBonus.gatherFoodMult + civBonus.teamGatherFoodBonus) * (rm?.techGatherFoodMult ?? 1)
      : node.kind === ResourceKind.Wood  ? civBonus.gatherWoodMult  * (rm?.techGatherWoodMult  ?? 1)
      : node.kind === ResourceKind.Gold  ? civBonus.gatherGoldMult  * (rm?.techGatherGoldMult  ?? 1)
      : node.kind === ResourceKind.Stone ? 1                        * (rm?.techGatherStoneMult ?? 1)
      : 1;
    const interval = gatherIntervalFor(node.kind) / gatherMult;
    if (timer >= interval) {
      timer -= interval;
      const taken = node.take(1);
      v.carryAmount += taken;
      v.carryKind = node.kind;
      if (taken > 0) this.onGatherTick?.();
    }
    this.timers.set(v, timer);

    // Full — return to dropoff
    if (v.carryAmount >= CARRY_CAPACITY) {
      const dropoff = v.dropoffBuilding ?? this._nearestDropoff(v, node.kind, buildings);
      if (dropoff) {
        v.dropoffBuilding = dropoff;
        v.moveTo(dropoff.pos.clone());
        v.state = UnitState.ReturningToDropoff; // must come after moveTo
      }
    }
  }

  private _tickReturn(
    v: Unit,
    node: ResourceNode,
    buildings: Building[],
    teamRes: ResourceManager[],
    dt: number,
  ) {
    const dropoff = v.dropoffBuilding ?? this._nearestDropoff(v, v.carryKind as ResourceKind, buildings);
    if (!dropoff) { v.state = UnitState.Idle; return; }
    v.dropoffBuilding = dropoff;

    const dist = v.pos.distanceTo(dropoff.pos);
    if (dist > DROPOFF_RANGE) return; // still walking

    // Deposit
    const rm = teamRes[v.teamId];
    if (rm) {
      rm.gain(v.carryKind as ResourceKind, v.carryAmount);
    }
    v.carryAmount = 0;

    // Resume gathering
    if (!node.depleted) {
      v.moveTo(this._approachPoint(node.root.position, v.pos, gatherRangeFor(node.kind) * 0.6));
      v.state = UnitState.Gathering;
    } else {
      node.currentGatherers = Math.max(0, node.currentGatherers - 1);
      v.gatherTarget = null;
      v.state = UnitState.Idle;
    }
  }

  private _nearestDropoff(v: Unit, kind: ResourceKind, buildings: Building[]): Building | null {
    const bit = 1 << (kind as number);
    let nearest: Building | null = null;
    let best = Infinity;
    for (const b of buildings) {
      if (!b.alive) continue;
      if (b.teamId !== v.teamId) continue;
      if (!b.def.isDropoff) continue;
      if (!(b.def.dropoffMask & bit)) continue;
      const d = v.pos.distanceTo(b.pos);
      if (d < best) { best = d; nearest = b; }
    }
    return nearest;
  }

  private _approachPoint(nodePos: THREE.Vector3, _fromPos: THREE.Vector3, _range: number): THREE.Vector3 {
    // Use NavGrid to find nearest walkable cell adjacent to the node.
    // This avoids approach points landing inside trees or building footprints.
    const [wx, wz] = navGrid.nearestFreeCellWorld(nodePos.x, nodePos.z);
    return new THREE.Vector3(wx, 0, wz);
  }

  /**
   * Tick idle-farm decay and auto-reseed (ResourceNode.SimTick port).
   * Farm nodes (decayPerSecond > 0) lose food while idle; when depleted they
   * reseed for 60 wood (Franks pay half-rate decay via farmDecayMult).
   */
  tickFarms(nodes: ResourceNode[], teamRes: ResourceManager[], dt: number) {
    for (const n of nodes) {
      if (n.decayPerSecond <= 0 || n.amount <= 0) continue;
      if (n.currentGatherers > 0) { n.decayAccum = 0; continue; }
      const decayMult = n.ownerTeamId >= 0
        ? (getTeamBonus(n.ownerTeamId).farmDecayMult ?? 1)
        : 1;
      n.decayAccum += n.decayPerSecond * decayMult * dt;
      if (n.decayAccum >= 1) {
        const dec = Math.floor(n.decayAccum);
        n.amount = Math.max(0, n.amount - dec);
        n.decayAccum -= dec;
      }
      if (n.amount === 0 && !n.destroyOnDeplete) {
        // Farm reseed: 60 wood cost; if unaffordable farm stays empty until next tick
        const rm = n.ownerTeamId >= 0 ? teamRes[n.ownerTeamId] : null;
        if (rm && rm.wood >= 60) {
          rm.wood -= 60;
          rm.onChange?.();
          n.amount = (n as { maxAmount: number }).maxAmount;
          n.decayAccum = 0;
        }
      }
    }
  }

  /** Remove timer entries for dead/non-gathering units; decrement their node slots. */
  prune() {
    for (const [v] of this.timers) {
      if (!v.alive || (v.state !== UnitState.Gathering && v.state !== UnitState.ReturningToDropoff)) {
        if (!v.alive && v.gatherTarget) {
          v.gatherTarget.currentGatherers = Math.max(0, v.gatherTarget.currentGatherers - 1);
          v.gatherTarget = null;
        }
        this.timers.delete(v);
      }
    }
  }
}
