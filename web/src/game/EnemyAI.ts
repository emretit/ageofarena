/**
 * EnemyAI.ts — simplified port of EnemyAI.cs.
 * Gather → build → train → research → attack loop for team 1 (AI opponent).
 */
import * as THREE from "three";
import { Age, BuildingType, ResourceKind, UnitState, UnitType } from "../core/GameTypes";
import { navGrid, GRID_SIZE, FLAG_WATER } from "../sim/NavGrid";
import { ResourceManager } from "../core/ResourceManager";
import { AgeSystem } from "./AgeSystem";
import { GatherSystem } from "./GatherSystem";
import { TrainingQueue } from "./TrainingQueue";
import type { Unit } from "./Unit";
import { Building, DEFS } from "./Building";
import type { ResourceNode } from "./ResourceNode";
import { type ResearchSystem, TECH_DEFS, TechId } from "./ResearchSystem";
import type { CommandBus } from "../sim/CommandBus";
import { qEncode } from "../sim/Command";
import { DMath } from "../sim/DMath";

export enum Difficulty {
  Easiest = 0, Easy, Normal, Hard, Harder, Hardest,
}

export enum Personality { Rusher, Balanced, Boomer }

interface DifficultyConfig {
  firstPush:    number; // seconds before first attack
  gatherMult:   number; // resource deposit multiplier (applied at deposit)
  villagerGoal: number; // target villager count before military push
  usesAttackMove: boolean;
  retreatAt:    number; // retreat when this fraction of army is lost (0=never)
}

const DIFFICULTY_TABLE: DifficultyConfig[] = [
  { firstPush: 420, gatherMult: 0.7,  villagerGoal:  8, usesAttackMove: false, retreatAt: 0    }, // Easiest
  { firstPush: 360, gatherMult: 0.85, villagerGoal: 10, usesAttackMove: false, retreatAt: 0    }, // Easy
  { firstPush: 240, gatherMult: 1.0,  villagerGoal: 14, usesAttackMove: false, retreatAt: 0    }, // Normal
  { firstPush: 150, gatherMult: 1.1,  villagerGoal: 18, usesAttackMove: true,  retreatAt: 0.4  }, // Hard
  { firstPush:  90, gatherMult: 1.2,  villagerGoal: 22, usesAttackMove: true,  retreatAt: 0.35 }, // Harder
  { firstPush:  60, gatherMult: 1.5,  villagerGoal: 28, usesAttackMove: true,  retreatAt: 0.3  }, // Hardest
];

// Personality-filtered build orders and train priorities
const RUSHER_TRAIN_PRIORITY  = [BuildingType.Barracks, BuildingType.ArcheryRange];
const BALANCED_TRAIN_PRIORITY = [BuildingType.Barracks, BuildingType.ArcheryRange, BuildingType.Stable];
const BOOMER_TRAIN_PRIORITY  = [BuildingType.Stable, BuildingType.ArcheryRange, BuildingType.Barracks];

const BUILD_CHECK_INTERVAL    = 20;
const TRAIN_CHECK_INTERVAL    = 10;
const RESEARCH_CHECK_INTERVAL = 30;

/**
 * Ordered build priority. AI attempts to build each type in order as
 * resources allow.
 */
const BUILD_ORDER: Array<{
  type: BuildingType;
  maxOwned: number;
  minElapsed: number;
  minAge: Age;
}> = [
  { type: BuildingType.House,       maxOwned: 6,  minElapsed: 30,  minAge: Age.Dark   },
  { type: BuildingType.Barracks,    maxOwned: 1,  minElapsed: 60,  minAge: Age.Dark   },
  { type: BuildingType.LumberCamp,  maxOwned: 1,  minElapsed: 90,  minAge: Age.Feudal },
  { type: BuildingType.Mill,        maxOwned: 1,  minElapsed: 90,  minAge: Age.Feudal },
  { type: BuildingType.MiningCamp,  maxOwned: 1,  minElapsed: 100, minAge: Age.Feudal },
  { type: BuildingType.Blacksmith,  maxOwned: 1,  minElapsed: 110, minAge: Age.Feudal },
  { type: BuildingType.ArcheryRange,maxOwned: 1,  minElapsed: 120, minAge: Age.Feudal },
  { type: BuildingType.Dock,        maxOwned: 1,  minElapsed: 80,  minAge: Age.Dark   },
  { type: BuildingType.House,       maxOwned: 10, minElapsed: 180, minAge: Age.Dark   },
  { type: BuildingType.Stable,      maxOwned: 1,  minElapsed: 200, minAge: Age.Castle },
  { type: BuildingType.University,  maxOwned: 1,  minElapsed: 250, minAge: Age.Castle },
  { type: BuildingType.Monastery,   maxOwned: 1,  minElapsed: 300, minAge: Age.Castle },
  { type: BuildingType.Castle,      maxOwned: 1,  minElapsed: 350, minAge: Age.Castle },
];

/**
 * AI tech research priority — ordered by desirability.
 * AI picks the first unresearched tech it can afford from a building it owns.
 */
const TECH_PRIORITY: TechId[] = [
  // Dark Age eco
  TechId.Loom,
  // Feudal eco
  TechId.HorseCollar, TechId.DoubleBitAxe, TechId.Wheelbarrow,
  TechId.GoldMining, TechId.StoneMining,
  // Feudal military
  TechId.Fletching, TechId.Forging, TechId.ScaleMail, TechId.PaddedArcherArmor,
  TechId.ManAtArms,
  // Castle eco
  TechId.HeavyPlow, TechId.BowSaw, TechId.HandCart,
  TechId.GoldShaftMining, TechId.StoneMiningUpgrade,
  // Castle military
  TechId.IronCasting, TechId.ChainMail, TechId.Bodkin, TechId.LeatherArcherArmor,
  TechId.Bloodlines, TechId.Longswordsman, TechId.Crossbowman, TechId.Cavalier, TechId.Pikeman,
  TechId.LightCavalry, TechId.Husbandry,
  // Castle university
  TechId.Masonry, TechId.Ballistics,
  // Imperial eco
  TechId.CropRotation,
  // Imperial military
  TechId.BlastFurnace, TechId.PlateMail, TechId.RingArcherArmor, TechId.Bracer,
  TechId.TwoHandedSwordsman, TechId.Arbalest, TechId.Paladin, TechId.Halberdier,
  TechId.EliteSkirmisher, TechId.Hussar,
  // Imperial university
  TechId.Chemistry, TechId.Architecture,
  // Castle civ unique (Castle Age) — start() filters by civGate
  TechId.Chivalry, TechId.Ironclad, TechId.Yeomen, TechId.Nomads, TechId.Yasama,
  TechId.Kamandaran, TechId.Atlatl, TechId.GreekFire, TechId.Chieftains, TechId.Madrasah,
  TechId.Stronghold, TechId.GreatWall, TechId.Anarchy, TechId.Sipahi,
  // Castle civ unique (Imperial Age)
  TechId.BeardedAxe, TechId.Crenellations, TechId.Warwolf, TechId.Drill, TechId.Kataparuto,
  TechId.Mahouts, TechId.GarlandWars, TechId.Logistica, TechId.Berserkergang, TechId.Zealotry,
  TechId.FurorCeltica, TechId.Rocketry, TechId.Perfusion, TechId.Artillery,
];

type ArmyState = 'gathering' | 'rallying' | 'attacking' | 'retreating';

export class EnemyAI {
  private elapsed = 0;
  private gateLifted = false;
  private lastTrainCheck = 0;
  private lastBuildCheck = 0;
  private lastAgeCheck = 0;
  private lastResearchCheck = 0;
  private armyState: ArmyState = 'gathering';
  private armyPeakSize = 0; // track peak to detect losses
  private tickOffset: number;

  private readonly cfg: DifficultyConfig;

  constructor(
    private readonly teamId: number,
    private readonly rm: ResourceManager,
    private readonly ageSystem: AgeSystem,
    private readonly gather: GatherSystem,
    private readonly training: TrainingQueue,
    private readonly research: ResearchSystem,
    difficulty  = Difficulty.Normal,
    private readonly personality = Personality.Balanced,
    tickOffset  = 0,
    private readonly bus?: CommandBus,
  ) {
    this.cfg = DIFFICULTY_TABLE[difficulty];
    this.tickOffset = tickOffset;
  }

  tick(
    units: Unit[],
    buildings: Building[],
    nodes: ResourceNode[],
    scene: THREE.Scene,
    dt: number,
    pathQueue?: import('../sim/PathQueue').PathQueue,
    allyTeams: number[] = [],
  ) {
    this.elapsed += dt;

    const myUnits        = units.filter(u => u.teamId === this.teamId && u.alive);
    const myMilitary     = myUnits.filter(u => !u.gathers && u.domain === 'land');
    const myNaval        = myUnits.filter(u => !u.gathers && u.domain === 'water');
    const myBuildings    = buildings.filter(b => b.teamId === this.teamId && b.alive);
    const enemyBuildings = buildings.filter(b => b.teamId !== this.teamId && !allyTeams.includes(b.teamId) && b.alive);

    // ── Assign idle villagers to nearest resource node ─────────────────────
    for (const v of myUnits) {
      if (!v.gathers || v.state !== UnitState.Idle) continue;
      const node = this._nearestNode(v, nodes);
      if (!node) continue;
      if (this.bus) {
        this.bus.issue({ kind: 'gather', teamId: this.teamId, ai: true, unitIds: [v.id], nodeId: node.id });
      } else {
        this.gather.assignGather(v, node, buildings);
      }
    }

    // ── Age advancement ────────────────────────────────────────────────────
    this.ageSystem.tick(this.rm, dt);
    if (this.elapsed - this.lastAgeCheck >= 30) {
      this.lastAgeCheck = this.elapsed;
      if (this.ageSystem.progress() < 0) {
        // Replicated command (single deterministic path); direct fallback when no bus.
        if (this.bus) this.bus.issue({ kind: 'ageUp', teamId: this.teamId, ai: true });
        else this.ageSystem.startAgeUp(this.rm);
      }
    }

    // ── Build order ────────────────────────────────────────────────────────
    if (this.elapsed - this.lastBuildCheck >= BUILD_CHECK_INTERVAL) {
      this.lastBuildCheck = this.elapsed;
      this._tryBuild(myBuildings, buildings, scene);
    }

    // ── Train military units from production buildings ─────────────────────
    if (this.elapsed - this.lastTrainCheck >= TRAIN_CHECK_INTERVAL) {
      this.lastTrainCheck = this.elapsed;
      const trainPriority = this.personality === Personality.Rusher  ? RUSHER_TRAIN_PRIORITY
                          : this.personality === Personality.Boomer   ? BOOMER_TRAIN_PRIORITY
                          : BALANCED_TRAIN_PRIORITY;
      for (const b of myBuildings) {
        if (!trainPriority.includes(b.buildingType)) continue;
        const trainTypes: UnitType[] = [];
        if (b.buildingType === BuildingType.Barracks) {
          trainTypes.push(UnitType.Militia, UnitType.Spearman);
        } else if (b.buildingType === BuildingType.ArcheryRange) {
          trainTypes.push(UnitType.Archer);
        } else if (b.buildingType === BuildingType.Stable) {
          trainTypes.push(UnitType.Cavalry);
        }
        for (const type of trainTypes) {
          if (this.bus) {
            this.bus.issue({ kind: 'train', teamId: this.teamId, ai: true, buildingId: b.id, unitType: type });
          } else {
            this.training.train(b, type, this.rm);
          }
        }
      }
      // Naval training from Dock: 2 FishingShips first (fish economy), then Galleys
      const fisherCount = myUnits.filter(u => u.unitType === UnitType.FishingShip).length;
      for (const b of myBuildings) {
        if (b.buildingType !== BuildingType.Dock) continue;
        const navalType = fisherCount < 2 ? UnitType.FishingShip : UnitType.Galley;
        if (this.bus) {
          this.bus.issue({ kind: 'train', teamId: this.teamId, ai: true, buildingId: b.id, unitType: navalType });
        } else {
          this.training.train(b, navalType, this.rm);
        }
      }
    }

    // ── Research techs ────────────────────────────────────────────────────
    if (this.elapsed - this.lastResearchCheck >= RESEARCH_CHECK_INTERVAL) {
      this.lastResearchCheck = this.elapsed;
      this._tryResearch(myBuildings);
    }

    // ── Army state machine ────────────────────────────────────────────────
    if (!this.gateLifted && this.elapsed >= this.cfg.firstPush) {
      this.gateLifted = true;
      this.armyState = 'rallying';
    }

    this._tickArmyState(myMilitary, myBuildings, enemyBuildings, pathQueue);
    this._tickNavalState(myNaval, enemyBuildings);
  }

  private _tickArmyState(
    myMilitary: Unit[],
    myBuildings: Building[],
    enemyBuildings: Building[],
    pathQueue?: import('../sim/PathQueue').PathQueue,
  ) {
    if (!this.gateLifted || enemyBuildings.length === 0) return;

    const target = enemyBuildings.find(b => b.buildingType === BuildingType.TownCenter)
      ?? enemyBuildings[0];

    if (this.armyState === 'rallying') {
      const tc = myBuildings.find(b => b.buildingType === BuildingType.TownCenter);
      const dest = tc ? tc.pos : target.pos;
      const idleArmy = myMilitary.filter(u => u.state === UnitState.Idle);
      if (idleArmy.length > 0) {
        if (this.bus) {
          this.bus.issue({ kind: 'move', teamId: this.teamId, ai: true, unitIds: idleArmy.map(u => u.id), qx: qEncode(dest.x), qz: qEncode(dest.z), queued: false });
        } else {
          for (const u of idleArmy) { u.moveTo(dest.clone()); u.state = UnitState.Moving; }
        }
      }
      if (myMilitary.length >= 3) {
        this.armyPeakSize = myMilitary.length;
        this.armyState = 'attacking';
      }
    } else if (this.armyState === 'attacking') {
      if (myMilitary.length > this.armyPeakSize) this.armyPeakSize = myMilitary.length;
      if (this.cfg.retreatAt > 0 && this.armyPeakSize > 0) {
        const lossRatio = 1 - myMilitary.length / this.armyPeakSize;
        if (lossRatio >= this.cfg.retreatAt) {
          this.armyState = 'retreating';
          this.armyPeakSize = 0;
          return;
        }
      }
      const readyUnits = myMilitary.filter(u =>
        u.state === UnitState.Idle || u.state === UnitState.MovingToAttack || u.state === UnitState.AttackMove
      );
      if (readyUnits.length > 0) {
        if (this.bus) {
          if (this.cfg.usesAttackMove) {
            this.bus.issue({ kind: 'attackMove', teamId: this.teamId, ai: true, unitIds: readyUnits.map(u => u.id), qx: qEncode(target.pos.x), qz: qEncode(target.pos.z) });
          } else {
            this.bus.issue({ kind: 'attackBuilding', teamId: this.teamId, ai: true, unitIds: readyUnits.map(u => u.id), targetId: target.id });
          }
        } else {
          for (const u of readyUnits) {
            if (this.cfg.usesAttackMove && pathQueue) {
              u.state = UnitState.AttackMove;
              u.attackMoveGoalX = target.pos.x;
              u.attackMoveGoalZ = target.pos.z;
              pathQueue.request(u, target.pos.x, target.pos.z, u.domain, this.teamId, 2);
            } else {
              u.state = UnitState.MovingToAttack;
              u.attackTargetBuilding = target;
              u.moveTo(target.pos.clone());
            }
          }
        }
      }
    } else if (this.armyState === 'retreating') {
      const tc = myBuildings.find(b => b.buildingType === BuildingType.TownCenter);
      for (const u of myMilitary) {
        u.attackTarget = null;
        u.attackTargetBuilding = null;
      }
      if (tc && myMilitary.length > 0) {
        if (this.bus) {
          this.bus.issue({ kind: 'move', teamId: this.teamId, ai: true, unitIds: myMilitary.map(u => u.id), qx: qEncode(tc.pos.x), qz: qEncode(tc.pos.z), queued: false });
        } else {
          for (const u of myMilitary) { u.moveTo(tc.pos.clone()); u.state = UnitState.Moving; }
        }
      }
      if (myMilitary.length > 0) {
        const tc2 = myBuildings.find(b => b.buildingType === BuildingType.TownCenter);
        if (!tc2) { this.armyState = 'gathering'; return; }
        const allNear = myMilitary.every(u => {
          const dx = u.x - tc2.pos.x; const dz = u.z - tc2.pos.z;
          return dx * dx + dz * dz < 100;
        });
        if (allNear) { this.armyState = 'gathering'; this.elapsed = Math.max(0, this.elapsed - 60); }
      } else {
        this.armyState = 'gathering';
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

      // Dock must sit on a shore cell (land cell adjacent to water) — use dedicated finder.
      // On non-naval maps the finder returns null and we skip Dock silently.
      let foundPos: THREE.Vector3 | null = null;
      if (entry.type === BuildingType.Dock) {
        foundPos = this._findShoreDockPos(tc.pos);
      } else {
        // Find walkable position in spiral — try 8 angles before giving up
        const baseAngle = (myBuildings.length * 1.2) % (Math.PI * 2);
        const radius = 10 + Math.floor(myBuildings.length / 5) * 4;
        for (let attempt = 0; attempt < 8; attempt++) {
          const angle = (baseAngle + attempt * (Math.PI / 4)) % (Math.PI * 2);
          const px = tc.pos.x + DMath.cos(angle) * radius;
          const pz = tc.pos.z + DMath.sin(angle) * radius;
          const [cx, cz] = navGrid.worldToCell(px, pz);
          if (navGrid.isWalkable(cx, cz)) { foundPos = new THREE.Vector3(px, 0, pz); break; }
        }
      }
      if (!foundPos) continue; // no valid spot this check — try next building type

      // Placement → command (deterministic, shares the player's path: cost deduct +
      // NavGrid stamp happen in the executor callback in main.ts). Direct-build fallback
      // only when no bus is wired (headless tests).
      if (this.bus) {
        this.bus.issue({ kind: 'placeBuilding', teamId: this.teamId, ai: true, unitIds: [], buildingType: entry.type, qx: qEncode(foundPos.x), qz: qEncode(foundPos.z) });
      } else {
        this.rm.deduct(0, def.costWood, def.costGold, def.costStone);
        allBuildings.push(new Building(scene, foundPos, this.teamId, entry.type));
      }
      break; // one building per check interval
    }
  }

  private _tryResearch(myBuildings: Building[]) {
    for (const techId of TECH_PRIORITY) {
      if (this.research.isResearched(this.teamId, techId)) continue;
      const def = TECH_DEFS[techId];
      const host = myBuildings.find(
        b => b.buildingType === def.host && b.alive && !this.research.active(b)
      );
      if (!host) continue;
      if (this.bus) {
        this.bus.issue({ kind: 'research', teamId: this.teamId, ai: true, buildingId: host.id, techId });
      } else {
        this.research.start(host, techId, this.rm);
      }
    }
  }

  /** Naval army: push Galleys toward the enemy TC position (water path stops at shore). */
  private _tickNavalState(myNaval: Unit[], enemyBuildings: Building[]) {
    if (!this.gateLifted || myNaval.length === 0 || enemyBuildings.length === 0) return;
    const target = enemyBuildings.find(b => b.buildingType === BuildingType.TownCenter)
      ?? enemyBuildings[0];
    const ready = myNaval.filter(u =>
      u.state === UnitState.Idle || u.state === UnitState.AttackMove || u.state === UnitState.MovingToAttack,
    );
    if (ready.length === 0) return;
    if (this.bus) {
      this.bus.issue({
        kind: 'attackMove', teamId: this.teamId, ai: true,
        unitIds: ready.map(u => u.id),
        qx: qEncode(target.pos.x), qz: qEncode(target.pos.z),
      });
    } else {
      for (const u of ready) {
        u.state = UnitState.AttackMove;
        u.attackMoveGoalX = target.pos.x;
        u.attackMoveGoalZ = target.pos.z;
      }
    }
  }

  /**
   * Find a shore-adjacent land cell (walkable, at least one orthogonal neighbour is water)
   * within radii 12..26 from the AI's TC. Returns null on non-naval maps where no shore
   * exists at that range (Dock will be skipped silently).
   */
  private _findShoreDockPos(tcPos: THREE.Vector3): THREE.Vector3 | null {
    const [tcCx, tcCz] = navGrid.worldToCell(tcPos.x, tcPos.z);
    for (let r = 12; r <= 26; r++) {
      for (let a = 0; a < 16; a++) {
        const angle = ((a / 16) * Math.PI * 2 + this.teamId * 0.3) % (Math.PI * 2);
        const cx = Math.round(tcCx + Math.cos(angle) * r);
        const cz = Math.round(tcCz + Math.sin(angle) * r);
        if (!navGrid.isWalkable(cx, cz, 'land')) continue;
        const fi = cz * GRID_SIZE + cx;
        const nearWater =
          ((navGrid.flags[fi + 1]          ?? 0) & FLAG_WATER) !== 0 ||
          ((navGrid.flags[fi - 1]          ?? 0) & FLAG_WATER) !== 0 ||
          ((navGrid.flags[fi + GRID_SIZE]  ?? 0) & FLAG_WATER) !== 0 ||
          ((navGrid.flags[fi - GRID_SIZE]  ?? 0) & FLAG_WATER) !== 0;
        if (nearWater) {
          const [wx, wz] = navGrid.cellToWorld(cx, cz);
          return new THREE.Vector3(wx, 0, wz);
        }
      }
    }
    return null;
  }

  private _nearestNode(v: Unit, nodes: ResourceNode[]): ResourceNode | null {
    let best: ResourceNode | null = null;
    let bestDist = Infinity;
    for (const n of nodes) {
      if (n.depleted || !n.hasRoom) continue;
      if (n.domain !== v.domain) continue; // land villagers skip water fish nodes (and vice versa)
      const d = v.pos.distanceTo(n.root.position);
      if (d < bestDist) { bestDist = d; best = n; }
    }
    return best;
  }
}
