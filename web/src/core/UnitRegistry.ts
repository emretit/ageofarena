/**
 * N4.registry port — static per-unit data table. All numbers match Unity's
 * UnitRegistry.cs + UnitFactory HP values so balance is identical.
 */
import { ArmorClass, ArmorClassFlags, DamageType, UnitType } from "./GameTypes";
import type { Domain } from "../sim/NavGrid";

export interface BonusVsEntry { cls: ArmorClassFlags; bonus: number; }

export interface UnitRow {
  hp: number;
  moveSpeed: number;       // web world-units/sec (AoE2 speed × 4.375)
  baseAtk: number;
  baseRange: number;
  attackInterval: number;  // seconds between attacks (BAL.combat tuned)
  aggroRadius: number;
  armorClass: ArmorClassFlags;
  armorMelee: number;
  armorPierce: number;
  damageKind: DamageType;
  isRanged: boolean;
  splashRadius: number;
  bonusVs: BonusVsEntry[];
  trainFood: number;
  trainWood: number;
  trainGold: number;
  trainTime: number;       // seconds
  gathers: boolean;
  domain?: Domain;         // movement domain — undefined = 'land'; naval units = 'water'
}

function bv(cls: ArmorClassFlags, bonus: number): BonusVsEntry { return { cls, bonus }; }

const table = new Map<UnitType, UnitRow>([
  [UnitType.Villager, {
    hp: 25, moveSpeed: 3.5, baseAtk: 2, baseRange: 1.1, attackInterval: 1.6,
    aggroRadius: 0, armorClass: ArmorClass.None, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 50, trainWood: 0, trainGold: 0, trainTime: 25, gathers: true,
  }],
  [UnitType.Militia, {
    hp: 40, moveSpeed: 4.4, baseAtk: 5, baseRange: 1.3, attackInterval: 1.9,
    aggroRadius: 7, armorClass: ArmorClass.Infantry, armorMelee: 0, armorPierce: 1,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 60, trainWood: 20, trainGold: 0, trainTime: 21, gathers: false,
  }],
  [UnitType.Archer, {
    hp: 30, moveSpeed: 4.2, baseAtk: 4, baseRange: 6.5, attackInterval: 1.9,
    aggroRadius: 9, armorClass: ArmorClass.Archer, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0, bonusVs: [],
    trainFood: 25, trainWood: 45, trainGold: 0, trainTime: 35, gathers: false,
  }],
  [UnitType.Cavalry, {
    hp: 100, moveSpeed: 6.1, baseAtk: 8, baseRange: 1.4, attackInterval: 1.7,
    aggroRadius: 8, armorClass: ArmorClass.Cavalry, armorMelee: 2, armorPierce: 2,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 80, trainWood: 0, trainGold: 75, trainTime: 30, gathers: false,
  }],
  [UnitType.Spearman, {
    hp: 45, moveSpeed: 4.4, baseAtk: 4, baseRange: 1.5, attackInterval: 2.6,
    aggroRadius: 7, armorClass: ArmorClass.Infantry, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Cavalry, 15)],
    trainFood: 35, trainWood: 25, trainGold: 0, trainTime: 22, gathers: false,
  }],
  [UnitType.Scout, {
    hp: 55, moveSpeed: 8.4, baseAtk: 3, baseRange: 1.3, attackInterval: 1.6,
    aggroRadius: 0, armorClass: ArmorClass.Cavalry, armorMelee: 0, armorPierce: 2,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 80, trainWood: 0, trainGold: 0, trainTime: 30, gathers: false,
  }],
  [UnitType.Trebuchet, {
    hp: 150, moveSpeed: 2.6, baseAtk: 35, baseRange: 15, attackInterval: 9.0,
    aggroRadius: 15, armorClass: ArmorClass.Siege, armorMelee: 2, armorPierce: 6,
    damageKind: DamageType.Siege, isRanged: true, splashRadius: 1.5,
    bonusVs: [bv(ArmorClass.Building, 70)],
    trainFood: 0, trainWood: 200, trainGold: 200, trainTime: 60, gathers: false,
  }],
  [UnitType.Longbowman, {
    hp: 35, moveSpeed: 4.2, baseAtk: 5, baseRange: 8.5, attackInterval: 1.9,
    aggroRadius: 11, armorClass: ArmorClass.Archer, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0, bonusVs: [],
    trainFood: 25, trainWood: 45, trainGold: 0, trainTime: 35, gathers: false,
  }],
  [UnitType.Skirmisher, {
    hp: 30, moveSpeed: 4.2, baseAtk: 3, baseRange: 5, attackInterval: 2.8,
    aggroRadius: 9, armorClass: ArmorClass.Archer, armorMelee: 0, armorPierce: 3,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Archer, 3)],
    trainFood: 25, trainWood: 35, trainGold: 0, trainTime: 22, gathers: false,
  }],
  [UnitType.Mangonel, {
    hp: 60, moveSpeed: 2.6, baseAtk: 25, baseRange: 9, attackInterval: 5.5,
    aggroRadius: 11, armorClass: ArmorClass.Siege, armorMelee: 0, armorPierce: 6,
    damageKind: DamageType.Siege, isRanged: true, splashRadius: 1.8, bonusVs: [],
    trainFood: 0, trainWood: 160, trainGold: 135, trainTime: 46, gathers: false,
  }],
  [UnitType.Ram, {
    hp: 200, moveSpeed: 2.6, baseAtk: 4, baseRange: 1.3, attackInterval: 5.0,
    aggroRadius: 4, armorClass: ArmorClass.Siege, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Siege, isRanged: false, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Building, 40)],
    trainFood: 0, trainWood: 160, trainGold: 0, trainTime: 36, gathers: false,
  }],
  [UnitType.Monk, {
    hp: 30, moveSpeed: 3.5, baseAtk: 0, baseRange: 4.5, attackInterval: 3.0,
    aggroRadius: 0, armorClass: ArmorClass.None, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 0, trainWood: 0, trainGold: 100, trainTime: 51, gathers: false,
  }],
  [UnitType.TradeCart, {
    hp: 70, moveSpeed: 4.4, baseAtk: 0, baseRange: 0, attackInterval: 999,
    aggroRadius: 0, armorClass: ArmorClass.None, armorMelee: 0, armorPierce: 4,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 100, trainWood: 0, trainGold: 50, trainTime: 50, gathers: false,
  }],
  // ── Naval (water domain) — Dock-trained ──────────────────────────────────
  [UnitType.FishingShip, {
    hp: 60, moveSpeed: 5.0, baseAtk: 0, baseRange: 0, attackInterval: 999,
    aggroRadius: 0, armorClass: ArmorClass.Ship, armorMelee: 0, armorPierce: 4,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 0, trainWood: 75, trainGold: 0, trainTime: 40, gathers: true,
    domain: 'water',
  }],
  [UnitType.Galley, {
    hp: 120, moveSpeed: 5.5, baseAtk: 6, baseRange: 7, attackInterval: 3.0,
    aggroRadius: 9, armorClass: ArmorClass.Ship, armorMelee: 0, armorPierce: 6,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0, bonusVs: [],
    trainFood: 0, trainWood: 90, trainGold: 30, trainTime: 60, gathers: false,
    domain: 'water',
  }],
  // ── P1 parity: base units (AoE2:DE stats, speed ×4.375) ──────────────────
  [UnitType.Camel, {
    hp: 100, moveSpeed: 6.3, baseAtk: 6, baseRange: 1.4, attackInterval: 2.0,
    aggroRadius: 8, armorClass: ArmorClass.Cavalry | ArmorClass.Camel, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Cavalry, 9)],
    trainFood: 55, trainWood: 0, trainGold: 70, trainTime: 22, gathers: false,
  }],
  [UnitType.CavalryArcher, {
    hp: 50, moveSpeed: 6.1, baseAtk: 6, baseRange: 6.0, attackInterval: 2.0,
    aggroRadius: 9, armorClass: ArmorClass.Cavalry | ArmorClass.Archer, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0, bonusVs: [],
    trainFood: 0, trainWood: 40, trainGold: 70, trainTime: 34, gathers: false,
  }],
  [UnitType.Medic, {
    hp: 35, moveSpeed: 3.5, baseAtk: 0, baseRange: 0, attackInterval: 999,
    aggroRadius: 0, armorClass: ArmorClass.None, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 0, trainWood: 0, trainGold: 60, trainTime: 40, gathers: false,
  }],
  [UnitType.Scorpion, {
    hp: 40, moveSpeed: 2.6, baseAtk: 12, baseRange: 9, attackInterval: 3.6,
    aggroRadius: 11, armorClass: ArmorClass.Siege, armorMelee: 0, armorPierce: 5,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0.7,
    bonusVs: [bv(ArmorClass.Infantry, 4)],
    trainFood: 0, trainWood: 75, trainGold: 75, trainTime: 30, gathers: false,
  }],
  // ── P1 parity: warships (water domain, Dock-trained) ─────────────────────
  [UnitType.FireShip, {
    hp: 120, moveSpeed: 6.0, baseAtk: 4, baseRange: 4.5, attackInterval: 0.6,
    aggroRadius: 8, armorClass: ArmorClass.Ship, armorMelee: 0, armorPierce: 4,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Ship, 6)],
    trainFood: 0, trainWood: 75, trainGold: 25, trainTime: 60, gathers: false,
    domain: 'water',
  }],
  [UnitType.DemoShip, {
    // Self-destruct: CombatSystem detonates on contact (baseAtk = blast, splashRadius = blast radius).
    hp: 60, moveSpeed: 6.5, baseAtk: 140, baseRange: 1.6, attackInterval: 1.0,
    aggroRadius: 7, armorClass: ArmorClass.Ship, armorMelee: 0, armorPierce: 4,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 2.5, bonusVs: [],
    trainFood: 0, trainWood: 70, trainGold: 50, trainTime: 31, gathers: false,
    domain: 'water',
  }],
  // ── Regicide royal unit — not trainable; high HP, cannot attack (flee & protect) ──
  [UnitType.King, {
    hp: 150, moveSpeed: 5.0, baseAtk: 0, baseRange: 0, attackInterval: 999,
    aggroRadius: 0, armorClass: ArmorClass.Infantry, armorMelee: 2, armorPierce: 2,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 0, trainWood: 0, trainGold: 0, trainTime: 0, gathers: false,
  }],
  // ── Civilization unique units (Castle/Barracks, civ-gated in TrainingQueue) ──────
  [UnitType.TeutonicKnight, {
    hp: 80, moveSpeed: 3.0, baseAtk: 12, baseRange: 1.3, attackInterval: 2.0,
    aggroRadius: 7, armorClass: ArmorClass.Infantry, armorMelee: 5, armorPierce: 2,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 85, trainWood: 0, trainGold: 40, trainTime: 19, gathers: false,
  }],
  [UnitType.WarElephant, {
    hp: 250, moveSpeed: 2.6, baseAtk: 15, baseRange: 1.5, attackInterval: 2.2,
    aggroRadius: 8, armorClass: ArmorClass.Cavalry, armorMelee: 1, armorPierce: 2,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0.5,
    bonusVs: [bv(ArmorClass.Building, 7)],
    trainFood: 200, trainWood: 0, trainGold: 75, trainTime: 31, gathers: false,
  }],
  [UnitType.Mangudai, {
    hp: 60, moveSpeed: 6.3, baseAtk: 6, baseRange: 6.0, attackInterval: 2.0,
    aggroRadius: 9, armorClass: ArmorClass.Cavalry | ArmorClass.Archer, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Siege, 5)],
    trainFood: 0, trainWood: 55, trainGold: 65, trainTime: 26, gathers: false,
  }],
  [UnitType.Samurai, {
    hp: 60, moveSpeed: 4.5, baseAtk: 8, baseRange: 1.3, attackInterval: 1.4,
    aggroRadius: 7, armorClass: ArmorClass.Infantry, armorMelee: 1, armorPierce: 1,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 60, trainWood: 0, trainGold: 30, trainTime: 9, gathers: false,
  }],
  [UnitType.ThrowingAxeman, {
    hp: 50, moveSpeed: 4.4, baseAtk: 7, baseRange: 4.0, attackInterval: 2.0,
    aggroRadius: 8, armorClass: ArmorClass.Infantry, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Melee, isRanged: true, splashRadius: 0, bonusVs: [],
    trainFood: 55, trainWood: 0, trainGold: 25, trainTime: 17, gathers: false,
  }],
  [UnitType.Cataphract, {
    hp: 110, moveSpeed: 5.4, baseAtk: 9, baseRange: 1.4, attackInterval: 1.7,
    aggroRadius: 8, armorClass: ArmorClass.Cavalry, armorMelee: 2, armorPierce: 1,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Infantry, 9)],
    trainFood: 70, trainWood: 0, trainGold: 75, trainTime: 20, gathers: false,
  }],
  [UnitType.Berserk, {
    hp: 62, moveSpeed: 4.4, baseAtk: 9, baseRange: 1.3, attackInterval: 2.0,
    aggroRadius: 7, armorClass: ArmorClass.Infantry, armorMelee: 1, armorPierce: 1,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 65, trainWood: 0, trainGold: 25, trainTime: 16, gathers: false,
  }],
  [UnitType.Mameluke, {
    hp: 65, moveSpeed: 6.0, baseAtk: 7, baseRange: 3.0, attackInterval: 2.0,
    aggroRadius: 8, armorClass: ArmorClass.Cavalry | ArmorClass.Camel, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Melee, isRanged: true, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Cavalry, 10)],
    trainFood: 55, trainWood: 0, trainGold: 85, trainTime: 23, gathers: false,
  }],
  [UnitType.WoadRaider, {
    hp: 65, moveSpeed: 5.5, baseAtk: 8, baseRange: 1.3, attackInterval: 1.9,
    aggroRadius: 7, armorClass: ArmorClass.Infantry, armorMelee: 0, armorPierce: 1,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
    trainFood: 65, trainWood: 0, trainGold: 25, trainTime: 10, gathers: false,
  }],
  [UnitType.ChuKoNu, {
    hp: 45, moveSpeed: 4.2, baseAtk: 4, baseRange: 6.0, attackInterval: 1.0,
    aggroRadius: 9, armorClass: ArmorClass.Archer, armorMelee: 0, armorPierce: 0,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0, bonusVs: [],
    trainFood: 0, trainWood: 40, trainGold: 35, trainTime: 19, gathers: false,
  }],
  [UnitType.Huskarl, {
    hp: 60, moveSpeed: 4.6, baseAtk: 10, baseRange: 1.3, attackInterval: 2.0,
    aggroRadius: 7, armorClass: ArmorClass.Infantry, armorMelee: 0, armorPierce: 6,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Archer, 6)],
    trainFood: 52, trainWood: 0, trainGold: 26, trainTime: 16, gathers: false,
  }],
  [UnitType.Janissary, {
    hp: 50, moveSpeed: 4.2, baseAtk: 17, baseRange: 8.0, attackInterval: 3.0,
    aggroRadius: 10, armorClass: ArmorClass.Archer, armorMelee: 1, armorPierce: 0,
    damageKind: DamageType.Pierce, isRanged: true, splashRadius: 0, bonusVs: [],
    trainFood: 60, trainWood: 0, trainGold: 55, trainTime: 17, gathers: false,
  }],
  [UnitType.Eagle, {
    hp: 55, moveSpeed: 5.5, baseAtk: 7, baseRange: 1.3, attackInterval: 2.0,
    aggroRadius: 8, armorClass: ArmorClass.Infantry, armorMelee: 0, armorPierce: 2,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Siege, 3)],
    trainFood: 20, trainWood: 0, trainGold: 50, trainTime: 35, gathers: false,
  }],
  [UnitType.EliteEagle, {
    hp: 60, moveSpeed: 5.5, baseAtk: 9, baseRange: 1.3, attackInterval: 2.0,
    aggroRadius: 8, armorClass: ArmorClass.Infantry, armorMelee: 0, armorPierce: 4,
    damageKind: DamageType.Melee, isRanged: false, splashRadius: 0,
    bonusVs: [bv(ArmorClass.Siege, 5)],
    trainFood: 20, trainWood: 0, trainGold: 50, trainTime: 20, gathers: false,
  }],
]);

const defaultRow: UnitRow = {
  hp: 25, moveSpeed: 3.5, baseAtk: 2, baseRange: 1.1, attackInterval: 1.6,
  aggroRadius: 0, armorClass: ArmorClass.None, armorMelee: 0, armorPierce: 0,
  damageKind: DamageType.Melee, isRanged: false, splashRadius: 0, bonusVs: [],
  trainFood: 50, trainWood: 0, trainGold: 0, trainTime: 25, gathers: false,
};

export function getUnitRow(t: UnitType): UnitRow {
  return table.get(t) ?? defaultRow;
}
