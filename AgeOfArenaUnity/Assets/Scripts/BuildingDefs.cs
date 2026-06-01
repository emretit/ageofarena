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

    public BuildingDef(BuildingType t, int f, int w, int g, int s, float time,
        int pop, float hp, string name, char hk, bool canBuild,
        bool drop = false, int dropMask = 0)
    {
        type = t; food = f; wood = w; gold = g; stone = s;
        buildTime = time; popProvided = pop; maxHp = hp;
        display = name; hotkey = hk; buildable = canBuild;
        isDropoff = drop; dropoffMask = dropMask;
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
        new(BuildingType.TownCenter,    0,    0,  0,  0,  60f,  5,  600f, "Town Center",   'T', false, true,  MaskAll),
        new(BuildingType.House,         0,   30,  0,  0,  12f,  5,  300f, "House",         'H', true),
        new(BuildingType.Barracks,      0,  120,  0,  0,  25f,  0,  400f, "Barracks",      'B', true),
        new(BuildingType.ArcheryRange,  0,  120,  0,  0,  25f,  0,  400f, "Archery Range", 'R', true),
        new(BuildingType.Stable,        0,  120,  0,  0,  25f,  0,  400f, "Stable",        'T', true),
        new(BuildingType.Farm,          0,   60,  0,  0,  12f,  0,  200f, "Farm",          'F', true),
        new(BuildingType.LumberCamp,    0,   50,  0,  0,  10f,  0,  150f, "Lumber Camp",   'L', true,  true,  MaskWood),
        new(BuildingType.MiningCamp,    0,   50,  0,  0,  10f,  0,  150f, "Mining Camp",   'G', true,  true,  MaskMine),
        new(BuildingType.Mill,          0,   60,  0,  0,  12f,  0,  150f, "Mill",          'I', true,  true,  MaskFood),
    };

    public static BuildingDef Get(BuildingType t)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].type == t) return Table[i];
        return Table[0];
    }

    /// <summary>True if <paramref name="t"/> is a drop-off building (any resource).</summary>
    public static bool IsDropoff(BuildingType t) => Get(t).isDropoff;

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
