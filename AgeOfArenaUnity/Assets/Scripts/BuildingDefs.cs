/// <summary>
/// Central static data for every <see cref="BuildingType"/>: cost, build time,
/// population it provides, hitpoints, and build-menu display/hotkey. Keeps the
/// numbers in one place so <see cref="BuildingEntity"/> (hp/pop), the build menu
/// (cost/hotkey) and the placement/AI systems all read the same source.
/// Scaled-down AoE-style values for the vertical slice.
/// </summary>
public struct BuildingDef
{
    public BuildingType type;
    public int food, wood, gold, stone;
    public float buildTime;
    public int popProvided;
    public float maxHp;
    public string display;
    public char hotkey;        // build-menu shortcut
    public bool buildable;     // shown in the villager build menu
    public bool isDropoff;     // gatherers can deposit carried resources here
    public int dropoffMask;    // bit i set = accepts ResourceKind i (0 = none)

    // Defensive fire: attackRange > 0 marks a building that auto-shoots nearby
    // enemies (BuildingCombatSystem reads these). 0 = passive (the default).
    public float attackRange, attackDamage, attackInterval;

    public Age minAge;         // age the team must have reached to build this

    // Garrison: max units that can shelter inside. 0 = cannot garrison (default).
    // Garrisoned units are hidden + healed and add defensive arrows (BuildingCombatSystem).
    public int garrisonCapacity;

    // Armor: subtracted from incoming damage of the matching DamageType (min-1 floor
    // applied in TakeDamage). Siege damage bypasses both armor types (treated as 0).
    public float meleeArmor, pierceArmor;

    public BuildingDef(BuildingType t, int f, int w, int g, int s, float time,
        int pop, float hp, string name, char hk, bool canBuild,
        bool drop = false, int dropMask = 0,
        float atkRange = 0f, float atkDmg = 0f, float atkInterval = 0f,
        Age minAge = Age.Dark, int garrisonCap = 0,
        float meleeArm = 1f, float pierceArm = 3f)
    {
        type = t; food = f; wood = w; gold = g; stone = s;
        buildTime = time; popProvided = pop; maxHp = hp;
        display = name; hotkey = hk; buildable = canBuild;
        isDropoff = drop; dropoffMask = dropMask;
        attackRange = atkRange; attackDamage = atkDmg; attackInterval = atkInterval;
        this.minAge = minAge;
        garrisonCapacity = garrisonCap;
        meleeArmor = meleeArm; pierceArmor = pierceArm;
    }
}

public static class BuildingDefs
{
    // Drop-off acceptance masks (bit i = ResourceKind i).
    const int MaskAll  = (1 << (int)ResourceKind.Food) | (1 << (int)ResourceKind.Wood)
                       | (1 << (int)ResourceKind.Gold) | (1 << (int)ResourceKind.Stone);
    const int MaskWood = 1 << (int)ResourceKind.Wood;
    const int MaskFood = 1 << (int)ResourceKind.Food;
    const int MaskMine = (1 << (int)ResourceKind.Gold) | (1 << (int)ResourceKind.Stone);

    static readonly BuildingDef[] Table =
    {
        //               type                  f    w   g   s  time  pop  hp     name             hk  build  drop    mask
        new(BuildingType.TownCenter,    0,    0,  0,  0,  60f,  5,  600f, "Town Center",   'T', false, true,  MaskAll, garrisonCap: 10, meleeArm: 3f, pierceArm: 5f),
        new(BuildingType.House,         0,   30,  0,  0,  12f,  5,  300f, "House",         'H', true),
        new(BuildingType.Barracks,      0,  120,  0,  0,  25f,  0,  400f, "Barracks",      'B', true),
        new(BuildingType.ArcheryRange,  0,  120,  0,  0,  25f,  0,  400f, "Archery Range", 'R', true,  minAge: Age.Feudal),
        new(BuildingType.Stable,        0,  120,  0,  0,  25f,  0,  400f, "Stable",        'T', true,  minAge: Age.Castle),
        new(BuildingType.Farm,          0,   60,  0,  0,  12f,  0,  200f, "Farm",          'F', true),
        new(BuildingType.LumberCamp,    0,   50,  0,  0,  10f,  0,  150f, "Lumber Camp",   'L', true,  true,  MaskWood),
        new(BuildingType.MiningCamp,    0,   50,  0,  0,  10f,  0,  150f, "Mining Camp",   'G', true,  true,  MaskMine),
        new(BuildingType.Mill,          0,   60,  0,  0,  12f,  0,  150f, "Mill",          'I', true,  true,  MaskFood),
        new(BuildingType.Market,        0,  175,  0,  0,  25f,  0,  350f, "Market",        'K', true),
        // Castle: heavy stone cost (forces stone economy), high hp, +pop, and it
        // auto-fires arrows at nearby enemies (atkRange > 0). TC stays passive.
        new(BuildingType.Castle,        0,    0,  0,650,  80f, 10, 2000f, "Castle",        'E', true,  false, 0, 9f, 18f, 1.5f, garrisonCap: 15, meleeArm: 8f, pierceArm: 8f),
        // Defensive palisade & gate. Cheap wood, fast build, available from Dark Age.
        // Wall blocks pathfinding (carving NavMeshObstacle); Gate is a passable opening.
        new(BuildingType.Wall,          0,   10,  0,  0,   4f,  0,  200f, "Wall",          'W', true,  meleeArm: 10f, pierceArm: 10f),
        new(BuildingType.Gate,          0,   30,  0,  0,   8f,  0,  450f, "Gate",          'O', true),
        // Wonder: an Imperial-age victory building — very expensive, slow to raise, and
        // tough. Once finished it starts a victory countdown (MatchSystem); destroying it
        // cancels the count. A third win condition alongside conquest and relics.
        new(BuildingType.Wonder,        0,  500,800,600, 150f,  0, 3000f, "Anıt (Wonder)", 'Y', true,  minAge: Age.Imperial, meleeArm: 5f, pierceArm: 8f),
    };

    public static BuildingDef Get(BuildingType t)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].type == t) return Table[i];
        return Table[0];
    }

    /// <summary>True if <paramref name="t"/> is a drop-off building (any resource).</summary>
    public static bool IsDropoff(BuildingType t) => Get(t).isDropoff;

    /// <summary>True if a team at <paramref name="age"/> has unlocked building <paramref name="t"/>.</summary>
    public static bool UnlockedAt(BuildingType t, Age age) => age >= Get(t).minAge;

    /// <summary>Max units that can garrison inside <paramref name="t"/> (0 = none).</summary>
    public static int GarrisonCapacityFor(BuildingType t) => Get(t).garrisonCapacity;

    /// <summary>True if a gatherer carrying <paramref name="kind"/> can deposit at <paramref name="t"/>.</summary>
    public static bool AcceptsDropoff(BuildingType t, ResourceKind kind)
    {
        var d = Get(t);
        return d.isDropoff && (d.dropoffMask & (1 << (int)kind)) != 0;
    }

    /// <summary>All player-buildable building defs, in menu order.</summary>
    public static System.Collections.Generic.IEnumerable<BuildingDef> Buildable()
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].buildable) yield return Table[i];
    }
}
