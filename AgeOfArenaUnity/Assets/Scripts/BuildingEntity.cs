using UnityEngine;

/// <summary>
/// Identifies a building in the game world and exposes its trainable units.
/// Attached to the root GameObject created by <see cref="BuildingFactory"/>.
/// </summary>
public class BuildingEntity : MonoBehaviour, IDamageable
{
    public BuildingType type;
    public int teamId;

    public float hp;
    public float maxHp;

    // Construction state (driven by BuildSystem in the building-placement feature).
    public bool underConstruction;
    public float buildProgress;   // 0..1 while under construction
    public float buildTime;

    // Defensive-fire cooldown for buildings that auto-shoot (Castle); stats come
    // from BuildingDefs. Driven by BuildingCombatSystem. 0 for passive buildings.
    public float attackCooldown;

    /// <summary>Per-type hitpoints, sourced from the central <see cref="BuildingDefs"/> table.</summary>
    public static float MaxHpFor(BuildingType t) => BuildingDefs.Get(t).maxHp;

    // Start (not Awake): the factory sets `type` right after AddComponent, which
    // is after Awake has already run — so hp must be derived one step later.
    void Start()
    {
        if (maxHp <= 0f) maxHp = MaxHpFor(type);
        if (hp <= 0f) hp = maxHp;
    }

    // ── IDamageable ─────────────────────────────────────────────────────────
    public int TeamId => teamId;
    public bool IsAlive => this != null && hp > 0f;
    public Transform Transform => transform;
    public float Radius => 2.6f; // footprint so attackers stop at the wall, not the centre

    public void TakeDamage(float amount)
    {
        if (hp <= 0f) return;
        hp -= amount;
        if (hp <= 0f) Die();
    }

    void Die()
    {
        hp = 0f;
        // List removal deferred to GameManager's end-of-frame compaction.
        var gm = GameManager.Instance;
        if (gm != null && gm.selectedBuilding == this) gm.selectedBuilding = null;
        GameEvents.FireBuildingDestroyed(this, teamId);
        Destroy(gameObject);
    }

    static readonly UnitTrainable[] TownCenterTrainables =
    {
        new(UnitType.Villager, 25f, 50, 0, 0, "V"),
    };

    static readonly UnitTrainable[] BarracksTrainables =
    {
        new(UnitType.Militia, 21f, 0, 60, 20, "M"),
        new(UnitType.Scout,   14f, 30, 0,  0, "S"), // fast, no-damage recon
    };

    static readonly UnitTrainable[] ArcheryTrainables =
    {
        new(UnitType.Archer, 22f, 0, 25, 45, "A"),
    };

    static readonly UnitTrainable[] StableTrainables =
    {
        new(UnitType.Cavalry, 24f, 80, 0, 0, "C"),
    };

    static readonly UnitTrainable[] CastleTrainables =
    {
        new(UnitType.Trebuchet, 40f, 0, 200, 100, "S"),
        new(UnitType.Medic,     26f, 60, 0,   0, "H"), // heals nearby friendly units
    };

    static readonly UnitTrainable[] Empty = System.Array.Empty<UnitTrainable>();

    /// <summary>Age this building's owning team has reached (null-safe → Dark).</summary>
    Age TeamAge
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null ? gm.teamTech[teamId].age : Age.Dark;
        }
    }

    /// <summary>Minimum age a unit type can be trained at.</summary>
    static Age MinAgeFor(UnitType t) => t switch
    {
        UnitType.Archer    => Age.Feudal,
        UnitType.Cavalry   => Age.Castle,
        UnitType.Trebuchet => Age.Castle,
        UnitType.Medic     => Age.Castle, // trained at the Castle, Castle Age
        _                  => Age.Dark,   // Villager, Militia, Scout
    };

    public UnitTrainable[] GetTrainables()
    {
        if (underConstruction) return Empty; // no production until the build completes
        var all = type switch
        {
            BuildingType.TownCenter   => TownCenterTrainables,
            BuildingType.Barracks     => BarracksTrainables,
            BuildingType.ArcheryRange => ArcheryTrainables,
            BuildingType.Stable       => StableTrainables,
            BuildingType.Castle       => CastleTrainables,
            _                         => Empty,
        };

        // Filter out units the team hasn't unlocked yet (Archer→Feudal, Cavalry→Castle).
        Age age = TeamAge;
        int n = 0;
        for (int i = 0; i < all.Length; i++) if (age >= MinAgeFor(all[i].unitType)) n++;
        if (n == all.Length) return all;
        if (n == 0) return Empty;
        var filtered = new UnitTrainable[n];
        int j = 0;
        for (int i = 0; i < all.Length; i++)
            if (age >= MinAgeFor(all[i].unitType)) filtered[j++] = all[i];
        return filtered;
    }

    /// <summary>Technologies the owning team can research at this building right now.</summary>
    public System.Collections.Generic.List<TechDef> GetResearchables()
    {
        var gm = GameManager.Instance;
        if (underConstruction || gm == null)
            return new System.Collections.Generic.List<TechDef>();
        var tech = gm.teamTech[teamId];
        return TechDefs.ForBuilding(type, tech.age, tech);
    }
}
