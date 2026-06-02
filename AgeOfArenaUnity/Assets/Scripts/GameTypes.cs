/// <summary>
/// Shared gameplay enums and small value types for the Age of Arena vertical
/// slice. Mirrors the Three.js types so the systems read the same way.
/// </summary>
public enum ResourceKind { Food, Wood, Gold, Stone }

public enum UnitState { Idle, Moving, Gathering, ReturningToDropoff, MovingToAttack, Attacking, Constructing }

public enum UnitType { Villager, Militia, Archer, Cavalry, Trebuchet, Scout, Medic, Spearman, Monk, TradeCart }

/// <summary>Damage class for the armor counter matrix. Siege bypasses both armor types.</summary>
public enum DamageType { Melee, Pierce, Siege }

public enum BuildingType { TownCenter, House, Barracks, ArcheryRange, Stable, Farm, LumberCamp, MiningCamp, Mill, Market, Castle, Wall, Gate, Wonder, WatchTower, Blacksmith, Monastery, University }

/// <summary>Tech progression tier. Higher ages gate buildings/units/techs.</summary>
public enum Age { Dark, Feudal, Castle, Imperial }

/// <summary>
/// Strategic flavour for an <see cref="EnemyAI"/> brain. Tunes army size, push
/// timing and economy weight so the three enemy teams play differently.
/// </summary>
public enum AIPersonality { Balanced, Rusher, Boomer }

/// <summary>Global AI difficulty. Scales every enemy's production speed, army cap and
/// economy on top of its <see cref="AIPersonality"/> so one slider tunes the challenge.</summary>
public enum Difficulty { Easy, Normal, Hard, Insane }

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
    // Economy upgrades
    DoubleBitAxe,   // +wood gather
    Wheelbarrow,    // +all gather
    // University techs
    Masonry,        // +stone wall hp
    Fortified,      // +building armor
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
