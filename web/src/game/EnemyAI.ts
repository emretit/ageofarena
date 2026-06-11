/**
 * EnemyAI.ts — simplified port of EnemyAI.cs.
 * Gather → build → train → attack loop for team 1 (AI opponent).
 */
import * as THREE from "three";
import { Age, BuildingType, ResourceKind, UnitState, UnitType } from "../core/GameTypes";
import { ResourceManager } from "../core/ResourceManager";
import { AgeSystem } from "./AgeSystem";
import { GatherSystem } from "./GatherSystem";
import { TrainingQueue } from "./TrainingQueue";
import type { Unit } from "./Unit";
import { Building, DEFS } from "./Building";
import type { ResourceNode } from "./ResourceNode";

/** BAL.ai: first attack gates by difficulty (Normal = 240s). */
const FIRST_PUSH_TIME = 240;
const BUILD_CHECK_INTERVAL = 20;
const TRAIN_CHECK_INTERVAL = 10;

/**
 * Ordered build priority. AI attempts to build each type in order as
 * resources allow. Each entry maps to { maxOwned, minWood, minElapsed }.
 */
const BUILD_ORDER: Array<{
  type: BuildingType;
  maxOwned: number;
  minElapsed: number;
  minAge: Age;
}> = [
  { type: BuildingType.House,       maxOwned: 6,  minElapsed: 30,  minAge: Age.Dark   },
  { type: BuildingType.Barracks,    maxOwned: 1,  minElapsed: 60,  minAge: Age.Dark   },
  { type: BuildingType.ArcheryRange,maxOwned: 1,  minElapsed: 120, minAge: Age.Feudal },
  { type: BuildingType.Stable,      maxOwned: 1,  minElapsed: 200, minAge: Age.Castle },
  { type: BuildingType.House,       maxOwned: 10, minElapsed: 180, minAge: Age.Dark   },
];

export class EnemyAI {
  private elapsed = 0;
  private gateLifted = false;
  private lastTrainCheck = 0;
  private lastBuildCheck = 0;
  private lastAgeCheck = 0;

  constructor(
    private readonly teamId: number,
    private readonly rm: ResourceManager,
    private readonly ageSystem: AgeSystem,
    private readonly gather: GatherSystem,
    private readonly training: TrainingQueue,
  ) {}

  tick(
    units: Unit[],
    buildings: Building[],
    nodes: ResourceNode[],
    scene: THREE.Scene,
    dt: number,
  ) {
    this.elapsed += dt;

    const myUnits     = units.filter(u => u.teamId === this.teamId && u.alive);
    const myBuildings = buildings.filter(b => b.teamId === this.teamId && b.alive);
    const enemyBuildings = buildings.filter(b => b.teamId !== this.teamId && b.alive);

    // ── Assign idle villagers to nearest resource node ─────────────────────
    for (const v of myUnits) {
      if (!v.gathers || v.state !== UnitState.Idle) continue;
      const node = this._nearestNode(v, nodes);
      if (node) this.gather.assignGather(v, node, buildings);
    }

    // ── Age advancement ────────────────────────────────────────────────────
    this.ageSystem.tick(this.rm, dt);
    if (this.elapsed - this.lastAgeCheck >= 30) {
      this.lastAgeCheck = this.elapsed;
      if (this.ageSystem.progress() < 0) this.ageSystem.startAgeUp(this.rm);
    }

    // ── Build order ────────────────────────────────────────────────────────
    if (this.elapsed - this.lastBuildCheck >= BUILD_CHECK_INTERVAL) {
      this.lastBuildCheck = this.elapsed;
      this._tryBuild(myBuildings, buildings, scene);
    }

    // ── Train military units from production buildings ─────────────────────
    if (this.elapsed - this.lastTrainCheck >= TRAIN_CHECK_INTERVAL) {
      this.lastTrainCheck = this.elapsed;
      for (const b of myBuildings) {
        if (b.buildingType === BuildingType.Barracks) {
          this.training.train(b, UnitType.Militia, this.rm);
          this.training.train(b, UnitType.Spearman, this.rm);
        } else if (b.buildingType === BuildingType.ArcheryRange) {
          this.training.train(b, UnitType.Archer, this.rm);
        } else if (b.buildingType === BuildingType.Stable) {
          this.training.train(b, UnitType.Cavalry, this.rm);
        }
      }
    }

    // ── Attack when gate lifts ─────────────────────────────────────────────
    if (!this.gateLifted && this.elapsed >= FIRST_PUSH_TIME) {
      this.gateLifted = true;
    }

    if (this.gateLifted && enemyBuildings.length > 0) {
      const target = enemyBuildings.find(b => b.buildingType === BuildingType.TownCenter)
        ?? enemyBuildings[0];

      for (const u of myUnits) {
        if (!u.gathers && u.state === UnitState.Idle) {
          u.state = UnitState.MovingToAttack;
          u.attackTargetBuilding = target;
          u.moveTo(target.pos.clone());
        }
      }
    }
  }

  private _tryBuild(myBuildings: Building[], allBuildings: Building[], scene: THREE.Scene) {
    const tc = myBuildings.find(b => b.buildingType === BuildingType.TownCenter);
    if (!tc) return;

    for (const entry of BUILD_ORDER) {
      if (this.elapsed < entry.minElapsed) continue;
      if (this.rm.age < entry.minAge) continue;

      const owned = myBuildings.filter(b => b.buildingType === entry.type).length;
      if (owned >= entry.maxOwned) continue;

      const def = DEFS[entry.type];
      // Buildings cost wood (and sometimes stone); deduct wood/stone/gold
      if (!this.rm.canAfford(0, def.costWood, def.costGold, def.costStone)) continue;

      this.rm.deduct(0, def.costWood, def.costGold, def.costStone);

      // Place near TC in a spiral — use myBuildings.length for both angle and radius
      const angle = (myBuildings.length * 1.2) % (Math.PI * 2);
      const radius = 10 + Math.floor(myBuildings.length / 5) * 4;
      const pos = tc.pos.clone().add(
        new THREE.Vector3(Math.cos(angle) * radius, 0, Math.sin(angle) * radius),
      );
      allBuildings.push(new Building(scene, pos, this.teamId, entry.type));
      break; // one building per check interval
    }
  }

  private _nearestNode(v: Unit, nodes: ResourceNode[]): ResourceNode | null {
    let best: ResourceNode | null = null;
    let bestDist = Infinity;
    for (const n of nodes) {
      if (n.depleted || !n.hasRoom) continue;
      const d = v.pos.distanceTo(n.root.position);
      if (d < bestDist) { bestDist = d; best = n; }
    }
    return best;
  }
}
