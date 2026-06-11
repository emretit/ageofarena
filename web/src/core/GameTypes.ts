/** Core gameplay enums — GameTypes.cs port. Keep names aligned with C# source. */

export enum ResourceKind { Food = 0, Wood = 1, Gold = 2, Stone = 3 }

export enum UnitState {
  Idle, Moving, Gathering, ReturningToDropoff,
  MovingToAttack, Attacking, Constructing,
}

export enum UnitType {
  Villager, Militia, Archer, Cavalry, Spearman,
  Trebuchet, Scout, Longbowman, Skirmisher, Mangonel, Ram,
  Monk, TradeCart,
}

export enum DamageType { Melee = 0, Pierce = 1, Siege = 2 }

/** Bit-flag armor classes — [Flags] enum equivalent. */
export const ArmorClass = {
  None:     0,
  Infantry: 1 << 0,
  Cavalry:  1 << 1,
  Archer:   1 << 2,
  Siege:    1 << 3,
  Building: 1 << 4,
  Ship:     1 << 5,
  Camel:    1 << 6,
} as const;
export type ArmorClassFlags = number;

export enum BuildingType {
  TownCenter, House, Barracks, ArcheryRange, Stable,
  Farm, LumberCamp, MiningCamp, Mill, Market,
  Castle, Wall, Wonder, Blacksmith, Monastery,
}

/** Age advancement — Age.Dark=0 … Age.Imperial=3 (matches Unity enum Age). */
export enum Age { Dark = 0, Feudal = 1, Castle = 2, Imperial = 3 }
