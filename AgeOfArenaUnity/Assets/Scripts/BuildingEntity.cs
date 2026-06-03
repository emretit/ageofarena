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

    // Armor (sourced from BuildingDefs in Start). Siege damage bypasses both types.
    public float meleeArmor;
    public float pierceArmor;

    // Rally point: when set, units trained here (TrainingQueue) walk to rallyPoint
    // on spawn instead of idling at the gate. Set via right-click while selected.
    public bool hasRally;
    public Vector3 rallyPoint;

    // Garrison: units sheltered inside (hidden + healed; add defensive arrows via
    // BuildingCombatSystem). Capacity comes from BuildingDefs; 0 = cannot garrison.
    public readonly System.Collections.Generic.List<UnitEntity> garrison = new();
    public int GarrisonCapacity => BuildingDefs.GarrisonCapacityFor(type);
    public int GarrisonCount => garrison.Count;
    public bool HasGarrisonSpace => GarrisonCount < GarrisonCapacity;

    /// <summary>Per-type hitpoints, sourced from the central <see cref="BuildingDefs"/> table.</summary>
    public static float MaxHpFor(BuildingType t) => BuildingDefs.Get(t).maxHp;

    // Start (not Awake): the factory sets `type` right after AddComponent, which
    // is after Awake has already run — so hp must be derived one step later.
    void Start()
    {
        if (maxHp <= 0f) maxHp = MaxHpFor(type);
        // Byzantines: buildings +10% HP (buildingHpMult); other civs ×1.0.
        float bMult = GameManager.Instance?.TeamCivBonus(teamId).buildingHpMult ?? 1f;
        if (bMult != 1f) maxHp *= bMult;
        if (hp <= 0f) hp = maxHp;
        var def = BuildingDefs.Get(type);
        meleeArmor = def.meleeArmor;
        pierceArmor = def.pierceArmor;
    }

    // ── IDamageable ─────────────────────────────────────────────────────────
    public int TeamId => teamId;
    public bool IsAlive => this != null && hp > 0f;
    public Transform Transform => transform;
    public float Radius => 2.6f; // footprint so attackers stop at the wall, not the centre

    public void TakeDamage(float amount, DamageType damageType = DamageType.Melee)
    {
        if (hp <= 0f) return;
        // Masonry / Fortified Wall (University techs) add armor to all team buildings.
        var tech = GameManager.Instance?.teamTech[teamId];
        float armor = damageType switch
        {
            DamageType.Pierce => pierceArmor + (tech?.BuildingPierceArmor ?? 0f),
            DamageType.Melee  => meleeArmor + (tech?.BuildingMeleeArmor ?? 0f),
            _                 => 0f,  // Siege bypasses armor
        };
        hp -= Mathf.Max(1f, amount - armor);
        // VFX: tint building darker as it takes damage (red shift at low HP).
        float frac = maxHp > 0f ? hp / maxHp : 1f;
        if (frac < 0.5f) TintDamage(frac);
        if (hp <= 0f) Die();
    }

    void TintDamage(float hpFrac)
    {
        // Lerp toward char-black at 0 HP so heavily damaged buildings look burned.
        var tint = Color.Lerp(new Color(0.15f, 0.08f, 0.05f), Color.white, hpFrac * 2f);
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r.sharedMaterial != null)
            {
                var m = r.material; // instance copy
                m.color = tint;
            }
    }

    void Die()
    {
        hp = 0f;
        // List removal deferred to GameManager's end-of-frame compaction.
        var gm = GameManager.Instance;
        // Units sheltering inside die with the building (they have no escape).
        gm?.garrison?.OnBuildingDestroyed(this);
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
        new(UnitType.Militia,   21f,  0, 60, 20, "M"),
        new(UnitType.Spearman,  18f, 35, 25,  0, "P"), // anti-cavalry; food 35 wood 25
        new(UnitType.Scout,     14f, 30,  0,  0, "S"), // fast, no-damage recon
    };

    static readonly UnitTrainable[] ArcheryTrainables =
    {
        new(UnitType.Archer,     22f, 0, 25, 45, "A"),
        new(UnitType.Skirmisher, 22f, 0, 25, 35, "K"), // anti-archer; wood+gold (Feudal)
    };

    // Britons unique: Longbowman available at ArcheryRange (Castle Age+).
    static readonly UnitTrainable[] ArcheryTrainablesBritons =
    {
        new(UnitType.Archer,      22f, 0, 25, 45, "A"),
        new(UnitType.Skirmisher,  22f, 0, 25, 35, "K"),
        new(UnitType.Longbowman,  26f, 0, 35, 65, "L"),
    };

    static readonly UnitTrainable[] StableTrainables =
    {
        new(UnitType.Cavalry,       24f, 80, 0,  0, "C"),
        new(UnitType.Camel,         22f, 55, 0, 60, "D"), // anti-cavalry; food+gold (Castle)
        new(UnitType.CavalryArcher, 26f, 0, 40, 70, "A"), // mobile archer; wood+gold (Castle)
    };

    static readonly UnitTrainable[] CastleTrainables =
    {
        new(UnitType.Trebuchet, 40f, 0, 200, 100, "S"),
        new(UnitType.Medic,     26f, 60, 0,   0, "H"),
    };

    static readonly UnitTrainable[] MonasteryTrainables =
    {
        new(UnitType.Monk, 30f, 0, 0, 100, "K"), // gold cost — holy unit
    };

    static readonly UnitTrainable[] MarketTrainables =
    {
        new(UnitType.TradeCart, 35f, 0, 80, 50, "Q"), // wood+gold — trade route unit
    };

    static readonly UnitTrainable[] DockTrainables =
    {
        new(UnitType.Galley,   35f, 0, 120, 60, "G"), // wood+gold — naval combat unit
        new(UnitType.FireShip, 32f, 0, 100, 45, "F"), // anti-ship (Feudal)
        new(UnitType.DemoShip, 30f, 0,  70, 50, "D"), // explosive splash (Castle)
    };

    static readonly UnitTrainable[] SiegeWorkshopTrainables =
    {
        new(UnitType.Ram,      40f, 0, 160,  75, "R"), // anti-structure, pierce-immune
        new(UnitType.Mangonel, 45f, 0, 160, 135, "M"), // area-damage siege
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

    /// <summary>Civilization of this building's owning team.</summary>
    Civilization TeamCiv
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null ? gm.teamCivs[teamId] : Civilization.None;
        }
    }

    bool IsBritons => TeamCiv == Civilization.Britons;

    /// <summary>Minimum age a unit type can be trained at.</summary>
    static Age MinAgeFor(UnitType t) => t switch
    {
        UnitType.Archer      => Age.Feudal,
        UnitType.Spearman    => Age.Feudal,
        UnitType.Cavalry     => Age.Castle,
        UnitType.Trebuchet   => Age.Castle,
        UnitType.Medic       => Age.Castle,
        UnitType.Monk        => Age.Castle,
        UnitType.Longbowman  => Age.Castle,
        UnitType.Galley      => Age.Feudal,
        UnitType.Skirmisher  => Age.Feudal,
        UnitType.Camel       => Age.Castle,
        UnitType.Ram         => Age.Castle,
        UnitType.Mangonel    => Age.Castle,
        UnitType.CavalryArcher => Age.Castle,
        UnitType.FireShip    => Age.Feudal,
        UnitType.DemoShip    => Age.Castle,
        _                    => Age.Dark,
    };

    public UnitTrainable[] GetTrainables()
    {
        if (underConstruction) return Empty; // no production until the build completes
        var all = type switch
        {
            BuildingType.TownCenter   => TownCenterTrainables,
            BuildingType.Barracks     => BarracksTrainables,
            BuildingType.ArcheryRange => IsBritons ? ArcheryTrainablesBritons : ArcheryTrainables,
            BuildingType.Stable       => StableTrainables,
            BuildingType.Castle       => CastleTrainables,
            BuildingType.Monastery    => MonasteryTrainables,
            BuildingType.Market       => MarketTrainables,
            BuildingType.Dock         => DockTrainables,
            BuildingType.SiegeWorkshop => SiegeWorkshopTrainables,
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
