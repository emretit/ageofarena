/**
 * TrainingQueue.cs port — manages unit production from a building.
 * TC trains Villagers; Barracks trains Militia/Archers; etc.
 */
import * as THREE from "three";
import { Age, BuildingType, UnitType } from "../core/GameTypes";
import { Civilization } from "../core/CivilizationDefs";
import { ResourceManager } from "../core/ResourceManager";
import { getUnitRow } from "../core/UnitRegistry";
import { getTeamBonus, getTeamCiv } from "../core/CivState";
import { simRng } from "../sim/SimRng";
import { navGrid } from "../sim/NavGrid";
import type { Building } from "./Building";
import { Unit } from "./Unit";
import type { ResearchSystem } from "./ResearchSystem";

/** Minimum age required to train each unit type (Age.Dark = no gate). */
const UNIT_MIN_AGE: Partial<Record<UnitType, Age>> = {
  [UnitType.Archer]:      Age.Feudal,
  [UnitType.Spearman]:    Age.Feudal,
  [UnitType.Skirmisher]:  Age.Feudal,
  [UnitType.Longbowman]:  Age.Feudal,
  [UnitType.Scout]:       Age.Feudal,
  [UnitType.Cavalry]:     Age.Castle,
  [UnitType.Trebuchet]:   Age.Castle,
  [UnitType.Mangonel]:    Age.Castle,
  [UnitType.Ram]:         Age.Castle,
  [UnitType.Galley]:      Age.Feudal,
};

/** Units denied to specific civilizations (port of CivDefs.DENIED_UNITS). */
const DENIED_UNITS: Partial<Record<Civilization, ReadonlySet<UnitType>>> = {
  [Civilization.Aztecs]: new Set([UnitType.Cavalry, UnitType.Scout]),
};

/** Which unit types a building can train. */
export const TRAINABLE: Partial<Record<BuildingType, UnitType[]>> = {
  [BuildingType.TownCenter]: [UnitType.Villager],
  [BuildingType.Barracks]:   [UnitType.Militia, UnitType.Spearman],
  [BuildingType.ArcheryRange]: [UnitType.Archer, UnitType.Skirmisher, UnitType.Longbowman],
  [BuildingType.Stable]:     [UnitType.Cavalry, UnitType.Scout],
  [BuildingType.Monastery]:  [UnitType.Monk],
  [BuildingType.Castle]:     [UnitType.Trebuchet],
  [BuildingType.Market]:     [UnitType.TradeCart],
  [BuildingType.Dock]:       [UnitType.FishingShip, UnitType.Galley],
};

interface QueueEntry { type: UnitType; timer: number; total: number; }

export class TrainingQueue {
  private readonly queues = new Map<Building, QueueEntry[]>();
  private static readonly MAX_QUEUE = 5;

  train(building: Building, type: UnitType, rm: ResourceManager): boolean {
    const row = getUnitRow(type);
    // Count all units currently in training queues — they will spawn and consume pop.
    let inQueue = 0;
    for (const q of this.queues.values()) inQueue += q.length;
    if (rm.pop + inQueue >= rm.popCap) return false;
    const minAge = UNIT_MIN_AGE[type] ?? Age.Dark;
    if (rm.age < minAge) return false;
    const denied = DENIED_UNITS[getTeamCiv(building.teamId)];
    if (denied?.has(type)) return false;
    if (!rm.canAfford(row.trainFood, row.trainWood, row.trainGold)) return false;
    const queue = this._queue(building);
    if (queue.length >= TrainingQueue.MAX_QUEUE) return false;
    rm.deduct(row.trainFood, row.trainWood, row.trainGold);
    const trainMult = getTeamBonus(building.teamId).unitTrainTimeMult;
    const trainTime = Math.max(1, row.trainTime * trainMult);
    queue.push({ type, timer: trainTime, total: trainTime });
    return true;
  }

  tick(
    buildings: Building[],
    units: Unit[],
    scene: THREE.Scene,
    research: ResearchSystem,
    dt: number,
  ) {
    for (const b of buildings) {
      if (!b.alive) { this.queues.delete(b); continue; }
      const queue = this.queues.get(b);
      if (!queue?.length) continue;

      const entry = queue[0];
      entry.timer -= dt;
      if (entry.timer <= 0) {
        queue.shift();
        let sx = b.pos.x + simRng.range(-2, 2);
        let sz = b.pos.z + simRng.range(-2, 2);
        // Naval units must spawn on water — snap to the nearest water cell near the Dock.
        if (getUnitRow(entry.type).domain === 'water') {
          [sx, sz] = navGrid.nearestFreeCellWorld(sx, sz, 'water');
        }
        const spawnPos = new THREE.Vector3(sx, 0, sz);
        const spawned = new Unit(scene, spawnPos, b.teamId, entry.type);
        // Apply all already-completed techs so new units match upgraded veterans
        research.applyCompletedResearchTo(spawned, b.teamId);
        units.push(spawned);
        if (b.rallyPoint) spawned.moveTo(b.rallyPoint.clone());
      }
    }
  }

  /** Progress [0..1] of the front item in queue, or -1 if empty. */
  progress(b: Building): number {
    const q = this.queues.get(b);
    if (!q?.length) return -1;
    return 1 - q[0].timer / q[0].total;
  }

  queueLength(b: Building): number {
    return this.queues.get(b)?.length ?? 0;
  }

  private _queue(b: Building): QueueEntry[] {
    let q = this.queues.get(b);
    if (!q) { q = []; this.queues.set(b, q); }
    return q;
  }
}
