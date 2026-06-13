/** Core gameplay enums — GameTypes.cs port. Keep names aligned with C# source. */

export enum ResourceKind { Food = 0, Wood = 1, Gold = 2, Stone = 3 }

export enum UnitState {
  Idle, Moving, Gathering, ReturningToDropoff,
  MovingToAttack, Attacking, Constructing,
  AttackMove, // moving to destination but engages any enemy in aggroRadius along the way
  Patrol,     // attack-move ping-pong between patrolA and patrolB
}

export enum AttackStance {
  Aggressive,   // chase any enemy in aggroRadius (default)
  Defensive,    // attack only if attacked; chase limited to 8u leash
  StandGround,  // attack in range but never move to chase
  NoAttack,     // ignore all enemies
}

export enum UnitType {
  Villager, Militia, Archer, Cavalry, Spearman,
  Trebuchet, Scout, Longbowman, Skirmisher, Mangonel, Ram,
  Monk, TradeCart,
  FishingShip, Galley, // naval (water domain) — Dock-trained
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
  University, SiegeWorkshop, Dock, WatchTower,
  Gate,
}

/** Age advancement — Age.Dark=0 … Age.Imperial=3 (matches Unity enum Age). */
export enum Age { Dark = 0, Feudal = 1, Castle = 2, Imperial = 3 }
