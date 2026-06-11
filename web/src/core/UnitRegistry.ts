/**
 * N4.registry port — static per-unit data table. All numbers match Unity's
 * UnitRegistry.cs + UnitFactory HP values so balance is identical.
 */
import { ArmorClass, ArmorClassFlags, DamageType, UnitType } from "./GameTypes";

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
