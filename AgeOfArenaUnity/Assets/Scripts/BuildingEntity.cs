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
        Destroy(gameObject);
    }

    static readonly UnitTrainable[] TownCenterTrainables =
    {
        new(UnitType.Villager, 25f, 50, 0, 0, "V"),
    };

    static readonly UnitTrainable[] BarracksTrainables =
    {
        new(UnitType.Militia, 21f, 0, 60, 20, "M"),
    };

    static readonly UnitTrainable[] ArcheryTrainables =
    {
        new(UnitType.Archer, 22f, 0, 25, 45, "A"),
    };

    static readonly UnitTrainable[] StableTrainables =
    {
        new(UnitType.Cavalry, 24f, 80, 0, 0, "C"),
    };

    static readonly UnitTrainable[] Empty = System.Array.Empty<UnitTrainable>();

    public UnitTrainable[] GetTrainables()
    {
        if (underConstruction) return Empty; // no production until the build completes
        return type switch
        {
            BuildingType.TownCenter   => TownCenterTrainables,
            BuildingType.Barracks     => BarracksTrainables,
            BuildingType.ArcheryRange => ArcheryTrainables,
            BuildingType.Stable       => StableTrainables,
            _                         => Empty,
        };
    }
}
