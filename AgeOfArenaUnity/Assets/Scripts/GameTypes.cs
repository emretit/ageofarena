/// <summary>
/// Shared gameplay enums and small value types for the Age of Arena vertical
/// slice. Mirrors the Three.js types so the systems read the same way.
/// </summary>
public enum ResourceKind { Food, Wood, Gold, Stone }

public enum UnitState { Idle, Moving, Gathering, ReturningToDropoff, MovingToAttack, Attacking, Constructing }

public enum UnitType { Villager, Militia, Archer, Cavalry }

public enum BuildingType { TownCenter, House, Barracks, ArcheryRange, Stable, Farm, LumberCamp, MiningCamp, Mill }

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
