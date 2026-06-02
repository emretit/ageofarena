/// <summary>
/// Shared gameplay enums and small value types for the Age of Arena vertical
/// slice. Mirrors the Three.js types so the systems read the same way.
/// </summary>
public enum ResourceKind { Food, Wood, Gold, Stone }

public enum UnitState { Idle, Moving, Gathering, ReturningToDropoff, MovingToAttack, Attacking, Constructing }

public enum UnitType { Villager, Militia, Archer, Cavalry, Trebuchet, Scout, Medic }

public enum BuildingType { TownCenter, House, Barracks, ArcheryRange, Stable, Farm, LumberCamp, MiningCamp, Mill, Market, Castle, Wall, Gate }

/// <summary>Tech progression tier. Higher ages gate buildings/units/techs.</summary>
public enum Age { Dark, Feudal, Castle }

/// <summary>
/// Strategic flavour for an <see cref="EnemyAI"/> brain. Tunes army size, push
/// timing and economy weight so the three enemy teams play differently.
/// </summary>
public enum AIPersonality { Balanced, Rusher, Boomer }

/// <summary>
/// Every researchable technology. The two age advances are modelled as techs so
/// they flow through the same research queue/UI as upgrades.
/// </summary>
public enum TechType
{
    // Age advances (researched at the Town Center)
    FeudalAge, CastleAge,
    // Military upgrades
    Forging,        // +melee attack (Militia, Cavalry)
    Fletching,      // +archer attack, +range
    Bodkin,         // +archer attack
    ScaleMail,      // +hp (Militia, Cavalry)
    Bloodlines,     // +hp (Cavalry)
    // Economy upgrades
    DoubleBitAxe,   // +wood gather
    Wheelbarrow,    // +all gather
}

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
