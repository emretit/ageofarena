/// <summary>
/// Shared gameplay enums and small value types for the Age of Arena vertical
/// slice. Mirrors the Three.js types so the systems read the same way.
/// </summary>
public enum ResourceKind { Food, Wood, Gold, Stone }

public enum UnitState { Idle, Moving, Gathering, ReturningToDropoff, MovingToAttack, Attacking, Constructing }

public enum UnitType
{
    Villager, Militia, Archer, Cavalry, Trebuchet, Scout, Medic, Spearman, Monk, TradeCart,
    Galley, Longbowman, Skirmisher, Camel, Ram, Mangonel, CavalryArcher, FireShip, DemoShip,
    // ── M9/CIVU: civilization unique units (Castle) + M9/EAGLE (Barracks) ──
    TeutonicKnight,  // Teutons — slow heavy infantry, high armor
    WarElephant,     // Persians — massive HP, bonus vs buildings
    Mangudai,        // Mongols — cavalry archer, bonus vs siege
    Samurai,         // Japanese — fast infantry
    // ── N4/CIVU: second wave of unique units ──
    ThrowingAxeman,  // Franks — ranged infantry (throws axes, melee damage)
    Cataphract,      // Byzantines — heavy cavalry, bonus vs infantry
    Berserk,         // Vikings — infantry that self-regenerates HP
    Mameluke,        // Saracens — camel rider that throws scimitars (ranged, anti-cavalry)
    Eagle,           // Aztecs — fast scout-warrior (EAGLE)
    // ── M9/EAGLE upgrade ──
    EliteEagle,      // Eagle → Elite Eagle (Aztecs, Imperial)
    // ── M10/VREGI: Regicide mode ──
    King,            // Regicide — each team's royal unit; losing it = eliminated
    // ── M14/FISH: naval economy ──
    FishingShip,     // gathers food from Fish ponds / Fish Traps, deposits at the Dock
}

/// <summary>Damage class for the armor counter matrix. Siege bypasses both armor types.</summary>
public enum DamageType { Melee, Pierce, Siege }

/// <summary>
/// AoE2-style armor classes (M7/ARMC). A unit/building belongs to one or more
/// classes; an attacker's <see cref="UnitEntity.BonusDamageVs"/> adds flat bonus
/// damage when the target carries the matching class (additive bonus-damage model,
/// replacing the old multiplicative anti-cavalry/anti-archer/anti-structure factors).
/// </summary>
[System.Flags]
public enum ArmorClass
{
    None     = 0,
    Infantry = 1 << 0,
    Cavalry  = 1 << 1,
    Archer   = 1 << 2,
    Siege    = 1 << 3,
    Building = 1 << 4,
    Ship     = 1 << 5,
    Camel    = 1 << 6,
}

public enum BuildingType { TownCenter, House, Barracks, ArcheryRange, Stable, Farm, LumberCamp, MiningCamp, Mill, Market, Castle, Wall, Gate, Wonder, WatchTower, Blacksmith, Monastery, University, Dock, SiegeWorkshop, Outpost, BombardTower, FishTrap }

/// <summary>Tech progression tier. Higher ages gate buildings/units/techs.</summary>
public enum Age { Dark, Feudal, Castle, Imperial }

/// <summary>
/// Match setup mode. Controls starting resources, spawn layout and victory paths.
/// Random = standard AoE2 arena. Others mirror AoE2 game modes.
/// </summary>
public enum GameMode { Random, Deathmatch, Regicide, Nomad }

/// <summary>
/// A team's diplomatic stance toward another team.
/// Enemy = attack on sight; Neutral = no auto-attack, trade/pass allowed;
/// Allied = treated as friendly (share vision, never auto-attacked).
/// </summary>
public enum DiplomacyState { Enemy, Neutral, Allied }

/// <summary>
/// Strategic flavour for an <see cref="EnemyAI"/> brain. Tunes army size, push
/// timing and economy weight so the three enemy teams play differently.
/// </summary>
public enum AIPersonality { Balanced, Rusher, Boomer }

/// <summary>AI challenge level — 6 tiers, monotonically harder. Scales spawn interval,
/// army cap, and eco-mult for AI teams.</summary>
public enum Difficulty { Easy, Moderate, Normal, Hard, Insane, Extreme }

/// <summary>
/// Every researchable technology. The two age advances are modelled as techs so
/// they flow through the same research queue/UI as upgrades.
/// </summary>
public enum TechType
{
    // Age advances (researched at the Town Center)
    FeudalAge, CastleAge, ImperialAge,
    // Military upgrades (flat "blacksmith" bonuses)
    Forging,        // +melee attack (Militia, Cavalry)
    Fletching,      // +archer attack, +range
    Bodkin,         // +archer attack
    ScaleMail,      // +hp (Militia, Cavalry)
    Bloodlines,     // +hp (Cavalry)
    // Unit upgrade lines (tier promotions: bigger stat jumps + a new display name)
    ManAtArms,      // Militia tier 2 (Feudal): +atk, +hp
    Longswordsman,  // Militia tier 3 (Castle, requires ManAtArms): +atk, +hp
    Crossbowman,    // Archer tier 2 (Castle): +atk, +range, +hp
    Cavalier,       // Cavalry tier 2 (Castle): +atk, +hp
    // Imperial tier promotions (require Castle tier + Imperial age)
    Champion,       // Longswordsman tier 4 (Imperial)
    Arbalest,       // Crossbowman tier 3 (Imperial)
    Paladin,        // Cavalier tier 3 (Imperial)
    // Counter-unit lines (M2)
    EliteSkirmisher,// Skirmisher tier 2 (Imperial): +atk, +hp
    Pikeman,        // Spearman tier 2 (Castle): +atk, +hp
    Halberdier,     // Spearman tier 3 (Imperial, requires Pikeman)
    HeavyCamel,     // Camel tier 2 (Imperial): +atk, +hp
    // Mobile-unit lines (M4)
    TwoHandedSwordsman, // Militia tier 3.5 (Imperial, requires Longswordsman; Champion requires this)
    LightCavalry,       // Scout tier 2 (Castle): +atk, +hp, combat-capable
    Hussar,             // Scout tier 3 (Imperial, requires LightCavalry)
    HeavyCavalryArcher, // Cavalry Archer tier 2 (Imperial): +atk, +hp
    WarGalley,          // Galley tier 2 (Castle): +atk, +hp
    Galleon,            // Galley tier 3 (Imperial, requires WarGalley)
    // Economy upgrades
    DoubleBitAxe,   // +wood gather
    Wheelbarrow,    // +all gather
    HorseCollar,    // +farm food capacity (Feudal)
    HeavyPlow,      // +farm food capacity (Castle)
    // University techs
    Masonry,        // +stone wall hp
    Fortified,      // +building armor
    GuardTower,     // Watch Tower → Guard Tower: +tower attack/hp (Castle)
    Keep,           // Guard Tower → Keep: +tower attack/range/hp (Imperial, requires GuardTower)
    // ── M6: Blacksmith attack lines (additive on top of base, infantry & cavalry) ──
    IronCasting,    // +melee attack (Castle, requires Forging)
    BlastFurnace,   // +melee attack (Imperial, requires IronCasting)
    // ── M6: Blacksmith infantry armor (ScaleMail→ChainMail→PlateMail) ──
    ChainMail,      // +infantry melee/pierce armor (Castle)
    PlateMail,      // +infantry melee/pierce armor (Imperial)
    // ── M6 (BFUR): Blacksmith cavalry armor (barding) ──
    ScaleBarding,   // +cavalry armor (Feudal)
    ChainBarding,   // +cavalry armor (Castle)
    PlateBarding,   // +cavalry armor (Imperial)
    // ── M6 (ARRM): Blacksmith archer armor + Bracer ──
    PaddedArcherArmor,  // +archer melee/pierce armor (Feudal)
    LeatherArcherArmor, // +archer armor (Castle)
    RingArcherArmor,    // +archer armor (Imperial)
    Bracer,             // +archer attack & range (Imperial, requires Bodkin)
    // ── M6 (ECON): economy gather techs ──
    Loom,           // +villager hp & armor (Dark, Town Center)
    BowSaw,         // +wood gather (Castle, Lumber Camp)
    GoldMining,     // +gold gather (Feudal, Mining Camp)
    StoneMining,    // +stone gather (Feudal, Mining Camp)
    CropRotation,   // +farm food capacity (Imperial, Mill)
    // ── M6 (CAVT): Stable husbandry ──
    Husbandry,      // +cavalry move speed (Castle)
    // ── M6 (CARA): Market caravan ──
    Caravan,        // +trade cart yield (Castle)
    // ── M6 (UNIV): University military techs ──
    Ballistics,     // projectile accuracy vs moving targets (Castle)
    Chemistry,      // +1 missile attack: archers/towers/galleys (Imperial)
    Architecture,   // +building hp & armor (Castle)
    // ── M7 (MONK/CONV): Monastery monk techs ──
    Sanctity,       // +Monk hp (Castle)
    BlockPrinting,  // +Monk conversion range (Castle)
    Redemption,     // Monk may convert buildings/siege (Castle)
    Theocracy,      // group convert spends only one monk's faith → faster, faith-retaining (Imperial)
    // ── M8 (MKTT): Market economy techs ──
    Coinage,        // tribute sent tax-free (Castle)
    Banking,        // +trade cart gold (stacks with Caravan) (Imperial)
    Guilds,         // narrows market sell/buy spread (Imperial)
    // ── M9 (EAGLE): Eagle Warrior upgrade ──
    EliteEagle,     // Eagle → Elite Eagle: +hp/+atk (Aztecs, Imperial)
    // ── M9 (CIVT): civilization unique techs (researched at the Castle) ──
    Chivalry,       // Franks (Castle): cavalry +20 hp
    BeardedAxe,     // Franks (Imperial): infantry +2 atk
    Ironclad,       // Teutons (Castle): siege +4 armor
    Crenellations,  // Teutons (Imperial): +tower range
}

/// <summary>Stance controlling auto-aggro and pursuit behavior.</summary>
public enum AttackStance { Aggressive, Defensive, StandGround, NoAttack }

/// <summary>What a gatherer is currently carrying back to a dropoff.</summary>
public struct Carry
{
    public ResourceKind kind;
    public int amount;

    public void Clear() { amount = 0; }
}

/// <summary>Static definition of a trainable unit for a given building type.</summary>
public struct UnitTrainable
{
    public UnitType unitType;
    public float trainTime;
    public int food, wood, gold;
    public string hotkey; // display hint

    public UnitTrainable(UnitType t, float time, int f, int w, int g, string hk)
    {
        unitType = t; trainTime = time;
        food = f; wood = w; gold = g; hotkey = hk;
    }
}
