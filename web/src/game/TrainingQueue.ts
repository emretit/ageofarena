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
  [UnitType.Camel]:         Age.Castle,
  [UnitType.CavalryArcher]: Age.Castle,
  [UnitType.Medic]:         Age.Castle,
  [UnitType.Scorpion]:      Age.Castle,
  [UnitType.FireShip]:      Age.Castle,
  [UnitType.DemoShip]:      Age.Castle,
  // Civ-unique units (Castle Age; Eagle line is Barracks)
  [UnitType.TeutonicKnight]: Age.Castle,
  [UnitType.WarElephant]:    Age.Castle,
  [UnitType.Mangudai]:       Age.Castle,
  [UnitType.Samurai]:        Age.Castle,
  [UnitType.ThrowingAxeman]: Age.Castle,
  [UnitType.Cataphract]:     Age.Castle,
  [UnitType.Berserk]:        Age.Castle,
  [UnitType.Mameluke]:       Age.Castle,
  [UnitType.WoadRaider]:     Age.Castle,
  [UnitType.ChuKoNu]:        Age.Castle,
  [UnitType.Huskarl]:        Age.Castle,
  [UnitType.Janissary]:      Age.Castle,
  [UnitType.Eagle]:          Age.Feudal,
  [UnitType.EliteEagle]:     Age.Imperial,
};

/** Civ-unique units — only trainable by the listed civilization. */
const UNIT_CIV_GATE: Partial<Record<UnitType, Civilization>> = {
  [UnitType.TeutonicKnight]: Civilization.Teutons,
  [UnitType.WarElephant]:    Civilization.Persians,
  [UnitType.Mangudai]:       Civilization.Mongols,
  [UnitType.Samurai]:        Civilization.Japanese,
  [UnitType.ThrowingAxeman]: Civilization.Franks,
  [UnitType.Cataphract]:     Civilization.Byzantines,
  [UnitType.Berserk]:        Civilization.Vikings,
  [UnitType.Mameluke]:       Civilization.Saracens,
  [UnitType.WoadRaider]:     Civilization.Celts,
  [UnitType.ChuKoNu]:        Civilization.Chinese,
  [UnitType.Huskarl]:        Civilization.Goths,
  [UnitType.Janissary]:      Civilization.Turks,
  [UnitType.Eagle]:          Civilization.Aztecs,
  [UnitType.EliteEagle]:     Civilization.Aztecs,
};

/** Units denied to specific civilizations (port of CivDefs.DENIED_UNITS). */
const DENIED_UNITS: Partial<Record<Civilization, ReadonlySet<UnitType>>> = {
  [Civilization.Aztecs]: new Set([UnitType.Cavalry, UnitType.Scout]),
};

/** Which unit types a building can train. */
export const TRAINABLE: Partial<Record<BuildingType, UnitType[]>> = {
  [BuildingType.TownCenter]: [UnitType.Villager],
  [BuildingType.Barracks]:   [UnitType.Militia, UnitType.Spearman, UnitType.Eagle, UnitType.EliteEagle],
  [BuildingType.ArcheryRange]: [UnitType.Archer, UnitType.Skirmisher, UnitType.Longbowman, UnitType.CavalryArcher],
  [BuildingType.Stable]:     [UnitType.Cavalry, UnitType.Scout, UnitType.Camel],
  [BuildingType.Monastery]:  [UnitType.Monk, UnitType.Medic],
  [BuildingType.Castle]:     [
    UnitType.Trebuchet,
    // Civ-unique units — filtered to the building owner's civ via UNIT_CIV_GATE.
    UnitType.TeutonicKnight, UnitType.WarElephant, UnitType.Mangudai, UnitType.Samurai,
    UnitType.ThrowingAxeman, UnitType.Cataphract, UnitType.Berserk, UnitType.Mameluke,
    UnitType.WoadRaider, UnitType.ChuKoNu, UnitType.Huskarl, UnitType.Janissary,
  ],
  [BuildingType.SiegeWorkshop]: [UnitType.Ram, UnitType.Mangonel, UnitType.Scorpion],
  [BuildingType.Market]:     [UnitType.TradeCart],
  [BuildingType.Dock]:       [UnitType.FishingShip, UnitType.Galley, UnitType.FireShip, UnitType.DemoShip],
};

/** Trainable units at a building, filtered to the owning team's civ (hides other civs' uniques). */
export function trainableFor(buildingType: BuildingType, teamId: number): UnitType[] {
  const all = TRAINABLE[buildingType];
  if (!all) return [];
  const civ = getTeamCiv(teamId);
  return all.filter(t => {
    const gate = UNIT_CIV_GATE[t];
    return gate === undefined || gate === civ;
  });
}

interface QueueEntry { type: UnitType; timer: number; total: number; }

export class TrainingQueue {
  private readonly queues = new Map<Building, QueueEntry[]>();
  private static readonly MAX_QUEUE = 5;
  /** AEGIS cheat: when true, all training completes instantly. */
  static aegisMode = false;

  train(building: Building, type: UnitType, rm: ResourceManager): boolean {
    const row = getUnitRow(type);
    // Count all units currently in training queues — they will spawn and consume pop.
    let inQueue = 0;
    for (const q of this.queues.values()) inQueue += q.length;
    if (rm.pop + inQueue >= rm.popCap) return false;
    const minAge = UNIT_MIN_AGE[type] ?? Age.Dark;
    if (rm.age < minAge) return false;
    const teamCiv = getTeamCiv(building.teamId);
    const denied = DENIED_UNITS[teamCiv];
    if (denied?.has(type)) return false;
    // Civ-unique units: only the owning civ may train them.
    const civGate = UNIT_CIV_GATE[type];
    if (civGate !== undefined && civGate !== teamCiv) return false;
    const foodCost = type === UnitType.Militia
      ? Math.max(0, row.trainFood - rm.techMilitiaFoodDiscount)
      : row.trainFood;
    if (!rm.canAfford(foodCost, row.trainWood, row.trainGold)) return false;
    const queue = this._queue(building);
    if (queue.length >= TrainingQueue.MAX_QUEUE) return false;
    rm.deduct(foodCost, row.trainWood, row.trainGold);
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
      if (TrainingQueue.aegisMode) entry.timer = 0;
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
