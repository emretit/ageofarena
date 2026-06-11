/**
 * ResearchSystem.ts — Port of ResearchSystem.cs + TechDefs.cs (core subset).
 * Blacksmith/LumberCamp/Mill/Barracks/Stable/TC research queue.
 * Costs from TechDefs.cs (Unity source of truth).
 */
import { Age, ArmorClass, BuildingType, UnitType } from "../core/GameTypes";
import type { ResourceManager } from "../core/ResourceManager";
import type { Unit } from "./Unit";
import type { Building } from "./Building";

export const enum TechId {
  // Blacksmith — Feudal
  Fletching           = "Fletching",
  Forging             = "Forging",
  PaddedArcherArmor   = "PaddedArcherArmor",
  ScaleMail           = "ScaleMail",
  // Blacksmith — Castle
  IronCasting         = "IronCasting",
  ChainMail           = "ChainMail",
  LeatherArcherArmor  = "LeatherArcherArmor",
  Bodkin              = "Bodkin",
  // Blacksmith — Imperial
  BlastFurnace        = "BlastFurnace",
  PlateMail           = "PlateMail",
  RingArcherArmor     = "RingArcherArmor",
  ScaleBarding        = "ScaleBarding",
  ChainBarding        = "ChainBarding",
  PlateBarding        = "PlateBarding",
  // Gather — Feudal
  DoubleBitAxe        = "DoubleBitAxe",
  Wheelbarrow         = "Wheelbarrow",
  HorseCollar         = "HorseCollar",
  Loom                = "Loom",
  // Gather — Castle
  BowSaw              = "BowSaw",
  HandCart            = "HandCart",
  HeavyPlow           = "HeavyPlow",
  // Military — Feudal
  ManAtArms           = "ManAtArms",
  // Military — Castle
  Longswordsman       = "Longswordsman",
  Bloodlines          = "Bloodlines",
  Crossbowman         = "Crossbowman",
  Cavalier            = "Cavalier",
  Pikeman             = "Pikeman",
  // Military — Imperial
  TwoHandedSwordsman  = "TwoHandedSwordsman",
  Champion            = "Champion",
  Arbalest            = "Arbalest",
  Paladin             = "Paladin",
  Halberdier          = "Halberdier",
  EliteSkirmisher     = "EliteSkirmisher",
}

export interface TechDef {
  label: string;
  host: BuildingType;
  minAge: Age;
  food: number;
  wood: number;
  gold: number;
  time: number;
  prereq?: TechId;
}

export const TECH_DEFS: Record<TechId, TechDef> = {
  // ── Blacksmith Feudal ────────────────────────────────────────────────────
  [TechId.Fletching]:          { label: "Fletching",           host: BuildingType.Blacksmith, minAge: Age.Feudal,   food: 100, wood:  0, gold:  50, time: 20 },
  [TechId.Forging]:            { label: "Forging",             host: BuildingType.Blacksmith, minAge: Age.Feudal,   food: 150, wood:  0, gold:   0, time: 20 },
  [TechId.PaddedArcherArmor]:  { label: "Padded Arch. Armor",  host: BuildingType.Blacksmith, minAge: Age.Feudal,   food: 100, wood:  0, gold:  50, time: 22 },
  [TechId.ScaleMail]:          { label: "Scale Mail Armor",    host: BuildingType.Blacksmith, minAge: Age.Feudal,   food: 100, wood:  0, gold:  50, time: 22 },
  [TechId.ScaleBarding]:       { label: "Scale Barding",       host: BuildingType.Blacksmith, minAge: Age.Feudal,   food: 150, wood:  0, gold:   0, time: 22 },
  // ── Blacksmith Castle ────────────────────────────────────────────────────
  [TechId.IronCasting]:        { label: "Iron Casting",        host: BuildingType.Blacksmith, minAge: Age.Castle,   food: 220, wood:  0, gold: 120, time: 28, prereq: TechId.Forging },
  [TechId.ChainMail]:          { label: "Chain Mail Armor",    host: BuildingType.Blacksmith, minAge: Age.Castle,   food: 200, wood:  0, gold: 100, time: 28, prereq: TechId.ScaleMail },
  [TechId.LeatherArcherArmor]: { label: "Leather Arch. Armor", host: BuildingType.Blacksmith, minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 28, prereq: TechId.PaddedArcherArmor },
  [TechId.Bodkin]:             { label: "Bodkin Arrow",        host: BuildingType.Blacksmith, minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 25, prereq: TechId.Fletching },
  [TechId.ChainBarding]:       { label: "Chain Barding",       host: BuildingType.Blacksmith, minAge: Age.Castle,   food: 250, wood:  0, gold: 150, time: 28, prereq: TechId.ScaleBarding },
  // ── Blacksmith Imperial ──────────────────────────────────────────────────
  [TechId.BlastFurnace]:       { label: "Blast Furnace",       host: BuildingType.Blacksmith, minAge: Age.Imperial, food: 275, wood:  0, gold: 225, time: 32, prereq: TechId.IronCasting },
  [TechId.PlateMail]:          { label: "Plate Mail Armor",    host: BuildingType.Blacksmith, minAge: Age.Imperial, food: 300, wood:  0, gold: 150, time: 32, prereq: TechId.ChainMail },
  [TechId.RingArcherArmor]:    { label: "Ring Archer Armor",   host: BuildingType.Blacksmith, minAge: Age.Imperial, food: 250, wood:  0, gold: 200, time: 32, prereq: TechId.LeatherArcherArmor },
  [TechId.PlateBarding]:       { label: "Plate Barding",       host: BuildingType.Blacksmith, minAge: Age.Imperial, food: 350, wood:  0, gold: 200, time: 32, prereq: TechId.ChainBarding },
  // ── Gather Feudal ────────────────────────────────────────────────────────
  [TechId.DoubleBitAxe]:       { label: "Double-Bit Axe",      host: BuildingType.LumberCamp, minAge: Age.Feudal,   food: 100, wood:  0, gold:   0, time: 18 },
  [TechId.Wheelbarrow]:        { label: "Wheelbarrow",          host: BuildingType.TownCenter, minAge: Age.Feudal,   food: 150, wood: 50, gold:   0, time: 22 },
  [TechId.HorseCollar]:        { label: "Horse Collar",         host: BuildingType.Mill,       minAge: Age.Feudal,   food:  75, wood:  0, gold:   0, time: 20 },
  [TechId.Loom]:               { label: "Loom",                 host: BuildingType.TownCenter, minAge: Age.Dark,     food:   0, wood:  0, gold:  50, time: 25 },
  // ── Gather Castle ────────────────────────────────────────────────────────
  [TechId.BowSaw]:             { label: "Bow Saw",              host: BuildingType.LumberCamp, minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 25, prereq: TechId.DoubleBitAxe },
  [TechId.HandCart]:           { label: "Hand Cart",            host: BuildingType.TownCenter, minAge: Age.Castle,   food: 300, wood:200, gold:   0, time: 35, prereq: TechId.Wheelbarrow },
  [TechId.HeavyPlow]:          { label: "Heavy Plow",           host: BuildingType.Mill,       minAge: Age.Castle,   food: 125, wood:  0, gold:   0, time: 25, prereq: TechId.HorseCollar },
  // ── Military Feudal ──────────────────────────────────────────────────────
  [TechId.ManAtArms]:          { label: "Man-at-Arms",          host: BuildingType.Barracks,   minAge: Age.Feudal,   food: 100, wood:  0, gold:  40, time: 25 },
  // ── Military Castle ──────────────────────────────────────────────────────
  [TechId.Longswordsman]:      { label: "Long Swordsman",       host: BuildingType.Barracks,   minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 30, prereq: TechId.ManAtArms },
  [TechId.Bloodlines]:         { label: "Bloodlines",           host: BuildingType.Stable,     minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 25 },
  [TechId.Crossbowman]:        { label: "Crossbowman",          host: BuildingType.ArcheryRange,minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 30 },
  [TechId.Cavalier]:           { label: "Cavalier",             host: BuildingType.Stable,     minAge: Age.Castle,   food: 150, wood:  0, gold: 100, time: 30 },
  [TechId.Pikeman]:            { label: "Pikeman",              host: BuildingType.Barracks,   minAge: Age.Castle,   food: 100, wood:  0, gold:  50, time: 28 },
  // ── Military Imperial ────────────────────────────────────────────────────
  [TechId.TwoHandedSwordsman]: { label: "Two-Handed Swordsman", host: BuildingType.Barracks,   minAge: Age.Imperial, food: 150, wood:  0, gold: 120, time: 32, prereq: TechId.Longswordsman },
  [TechId.Champion]:           { label: "Champion",             host: BuildingType.Barracks,   minAge: Age.Imperial, food: 200, wood:  0, gold: 150, time: 35, prereq: TechId.TwoHandedSwordsman },
  [TechId.Arbalest]:           { label: "Arbalest",             host: BuildingType.ArcheryRange,minAge: Age.Imperial, food: 200, wood:  0, gold: 150, time: 35, prereq: TechId.Crossbowman },
  [TechId.Paladin]:            { label: "Paladin",              host: BuildingType.Stable,     minAge: Age.Imperial, food: 200, wood:  0, gold: 150, time: 35, prereq: TechId.Cavalier },
  [TechId.Halberdier]:         { label: "Halberdier",           host: BuildingType.Barracks,   minAge: Age.Imperial, food: 150, wood:  0, gold: 100, time: 32, prereq: TechId.Pikeman },
  [TechId.EliteSkirmisher]:    { label: "Elite Skirmisher",     host: BuildingType.ArcheryRange,minAge: Age.Imperial, food: 150, wood:  0, gold: 100, time: 30 },
};

/** Techs available per building type (player-facing order). */
export const BUILDING_TECHS: Partial<Record<BuildingType, TechId[]>> = {
  [BuildingType.Blacksmith]:  [
    TechId.Fletching, TechId.Forging, TechId.PaddedArcherArmor, TechId.ScaleMail, TechId.ScaleBarding,
    TechId.IronCasting, TechId.ChainMail, TechId.LeatherArcherArmor, TechId.Bodkin, TechId.ChainBarding,
    TechId.BlastFurnace, TechId.PlateMail, TechId.RingArcherArmor, TechId.PlateBarding,
  ],
  [BuildingType.LumberCamp]:  [TechId.DoubleBitAxe, TechId.BowSaw],
  [BuildingType.Mill]:        [TechId.HorseCollar, TechId.HeavyPlow],
  [BuildingType.TownCenter]:  [TechId.Loom, TechId.Wheelbarrow, TechId.HandCart],
  [BuildingType.Barracks]:    [TechId.ManAtArms, TechId.Longswordsman, TechId.Pikeman, TechId.TwoHandedSwordsman, TechId.Champion, TechId.Halberdier],
  [BuildingType.Stable]:      [TechId.Bloodlines, TechId.Cavalier, TechId.Paladin],
  [BuildingType.ArcheryRange]:[TechId.Crossbowman, TechId.Arbalest, TechId.EliteSkirmisher],
};

interface QueueEntry { tech: TechId; timer: number; total: number; }

export class ResearchSystem {
  /** Per-team set of completed techs. */
  private readonly done = new Map<number, Set<TechId>>();
  /** Per-building active research queue (max 1 at a time). */
  private readonly queues = new Map<Building, QueueEntry>();

  isResearched(teamId: number, tech: TechId): boolean {
    return this.done.get(teamId)?.has(tech) ?? false;
  }

  /** Returns the in-progress entry for a building (for HUD progress bar). */
  active(b: Building): QueueEntry | undefined {
    return this.queues.get(b);
  }

  /** Returns all available (not yet researched, prereqs met, age ok) techs for a building. */
  available(b: Building, rm: ResourceManager): TechId[] {
    const list = BUILDING_TECHS[b.buildingType] ?? [];
    return list.filter(t => {
      if (this.isResearched(b.teamId, t)) return false;
      const def = TECH_DEFS[t];
      if (rm.age < def.minAge) return false;
      if (def.prereq && !this.isResearched(b.teamId, def.prereq)) return false;
      return true;
    });
  }

  start(b: Building, tech: TechId, rm: ResourceManager): boolean {
    if (this.queues.has(b)) return false; // already busy
    if (this.isResearched(b.teamId, tech)) return false;
    const def = TECH_DEFS[tech];
    if (!rm.canAfford(def.food, def.wood, def.gold)) return false;
    rm.deduct(def.food, def.wood, def.gold);
    this.queues.set(b, { tech, timer: def.time, total: def.time });
    return true;
  }

  tick(units: Unit[], teamRes: ResourceManager[], dt: number) {
    for (const [b, entry] of this.queues) {
      entry.timer -= dt;
      if (entry.timer <= 0) {
        this.queues.delete(b);
        this._complete(b.teamId, entry.tech, units, teamRes);
      }
    }
  }

  private _complete(teamId: number, tech: TechId, units: Unit[], teamRes: ResourceManager[]) {
    let set = this.done.get(teamId);
    if (!set) { set = new Set(); this.done.set(teamId, set); }
    set.add(tech);
    for (const u of units) {
      if (u.teamId !== teamId || !u.alive) continue;
      applyTechBonus(u, tech);
    }
    applyGatherBonus(tech, teamRes[teamId]);
  }
}

/** Apply a single tech bonus to an existing unit (retroactive on research). */
export function applyTechBonus(u: Unit, tech: TechId) {
  switch (tech) {
    // ── Blacksmith archer attack ───────────────────────────────────────────
    case TechId.Fletching:
      if (u.armorClass & ArmorClass.Archer) (u as { baseAtk: number }).baseAtk += 1;
      break;
    case TechId.Bodkin:
      if (u.armorClass & ArmorClass.Archer) (u as { baseAtk: number }).baseAtk += 1;
      break;
    // ── Blacksmith melee attack ────────────────────────────────────────────
    case TechId.Forging:
      if (u.armorClass & (ArmorClass.Infantry | ArmorClass.Cavalry)) (u as { baseAtk: number }).baseAtk += 1;
      break;
    case TechId.IronCasting:
      if (u.armorClass & (ArmorClass.Infantry | ArmorClass.Cavalry)) (u as { baseAtk: number }).baseAtk += 1;
      break;
    case TechId.BlastFurnace:
      if (u.armorClass & (ArmorClass.Infantry | ArmorClass.Cavalry)) (u as { baseAtk: number }).baseAtk += 2;
      break;
    // ── Blacksmith infantry armor ──────────────────────────────────────────
    case TechId.ScaleMail:
      if (u.armorClass & ArmorClass.Infantry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.ChainMail:
      if (u.armorClass & ArmorClass.Infantry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.PlateMail:
      if (u.armorClass & ArmorClass.Infantry) { (u as { armorMelee: number }).armorMelee += 2; (u as { armorPierce: number }).armorPierce += 2; }
      break;
    // ── Blacksmith cavalry barding ─────────────────────────────────────────
    case TechId.ScaleBarding:
      if (u.armorClass & ArmorClass.Cavalry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.ChainBarding:
      if (u.armorClass & ArmorClass.Cavalry) { (u as { armorMelee: number }).armorMelee += 1; (u as { armorPierce: number }).armorPierce += 1; }
      break;
    case TechId.PlateBarding:
      if (u.armorClass & ArmorClass.Cavalry) { (u as { armorMelee: number }).armorMelee += 2; (u as { armorPierce: number }).armorPierce += 2; }
      break;
    // ── Blacksmith archer armor ────────────────────────────────────────────
    case TechId.PaddedArcherArmor:
      if (u.armorClass & ArmorClass.Archer) (u as { armorPierce: number }).armorPierce += 1;
      break;
    case TechId.LeatherArcherArmor:
      if (u.armorClass & ArmorClass.Archer) (u as { armorPierce: number }).armorPierce += 1;
      break;
    case TechId.RingArcherArmor:
      if (u.armorClass & ArmorClass.Archer) (u as { armorPierce: number }).armorPierce += 1;
      break;
    // ── Loom (Villager defense) ────────────────────────────────────────────
    case TechId.Loom:
      if (u.unitType === UnitType.Villager) {
        (u as { maxHp: number }).maxHp += 15;
        u.hp = Math.min(u.hp + 15, u.maxHp);
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { armorPierce: number }).armorPierce += 1;
      }
      break;
    // ── Wheelbarrow / Hand Cart (Villager speed) ───────────────────────────
    case TechId.Wheelbarrow:
      if (u.unitType === UnitType.Villager) (u as { moveSpeed: number }).moveSpeed += 0.1;
      break;
    case TechId.HandCart:
      if (u.unitType === UnitType.Villager) (u as { moveSpeed: number }).moveSpeed += 0.1;
      break;
    // ── Cavalry upgrades ───────────────────────────────────────────────────
    case TechId.Bloodlines:
      if (u.armorClass & ArmorClass.Cavalry) {
        (u as { maxHp: number }).maxHp += 20;
        u.hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    case TechId.Cavalier:
      if (u.unitType === UnitType.Cavalry) {
        (u as { maxHp: number }).maxHp += 20;
        u.hp = Math.min(u.hp + 20, u.maxHp);
        (u as { baseAtk: number }).baseAtk += 2;
      }
      break;
    case TechId.Paladin:
      if (u.unitType === UnitType.Cavalry) {
        (u as { maxHp: number }).maxHp += 25;
        u.hp = Math.min(u.hp + 25, u.maxHp);
        (u as { baseAtk: number }).baseAtk += 3;
      }
      break;
    // ── Militia line ───────────────────────────────────────────────────────
    case TechId.ManAtArms:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
      }
      break;
    case TechId.Longswordsman:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { maxHp: number }).maxHp += 15;
        (u as { hp: number }).hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    case TechId.TwoHandedSwordsman:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { maxHp: number }).maxHp += 15;
        (u as { hp: number }).hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    case TechId.Champion:
      if (u.unitType === UnitType.Militia) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { armorMelee: number }).armorMelee += 1;
        (u as { maxHp: number }).maxHp += 20;
        (u as { hp: number }).hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    // ── Spearman line ──────────────────────────────────────────────────────
    case TechId.Pikeman:
      if (u.unitType === UnitType.Spearman) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { maxHp: number }).maxHp += 15;
        (u as { hp: number }).hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    case TechId.Halberdier:
      if (u.unitType === UnitType.Spearman) {
        (u as { baseAtk: number }).baseAtk += 3;
        (u as { maxHp: number }).maxHp += 20;
        (u as { hp: number }).hp = Math.min(u.hp + 20, u.maxHp);
      }
      break;
    // ── Archer line ────────────────────────────────────────────────────────
    case TechId.Crossbowman:
      if (u.unitType === UnitType.Archer) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { maxHp: number }).maxHp += 10;
        u.hp = Math.min(u.hp + 10, u.maxHp);
      }
      break;
    case TechId.Arbalest:
      if (u.unitType === UnitType.Archer) {
        (u as { baseAtk: number }).baseAtk += 2;
        (u as { maxHp: number }).maxHp += 15;
        u.hp = Math.min(u.hp + 15, u.maxHp);
      }
      break;
    // ── Elite Skirmisher ───────────────────────────────────────────────────
    case TechId.EliteSkirmisher:
      if (u.unitType === UnitType.Skirmisher) {
        (u as { baseAtk: number }).baseAtk += 1;
        (u as { maxHp: number }).maxHp += 10;
        u.hp = Math.min(u.hp + 10, u.maxHp);
      }
      break;
  }
}

/** Apply research bonuses that affect gather rates (not unit stats). */
function applyGatherBonus(tech: TechId, rm: ResourceManager | undefined) {
  if (!rm) return;
  switch (tech) {
    case TechId.HorseCollar:  rm.techGatherFoodMult += 0.15; break;
    case TechId.HeavyPlow:    rm.techGatherFoodMult += 0.15; break;
    case TechId.DoubleBitAxe: rm.techGatherWoodMult += 0.20; break;
    case TechId.BowSaw:       rm.techGatherWoodMult += 0.20; break;
  }
}
