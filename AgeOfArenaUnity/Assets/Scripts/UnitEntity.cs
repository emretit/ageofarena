using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A controllable unit (villager / militia). Movement is handled by
/// <see cref="NavMeshAgent"/>; gather logic lives in <see cref="GatherSystem"/>.
/// State machine: Idle ↔ Moving ↔ Gathering ↔ ReturningToDropoff.
/// </summary>
public class UnitEntity : MonoBehaviour, IDamageable
{
    public int unitId;
    public int teamId;
    public UnitType type;

    public float hp = 25f;
    public float maxHp = 25f;
    // Factory-assigned base max HP, captured in Start() before any tech/veterancy/civ
    // bonus is layered on. RecomputeMaxHp() always derives maxHp from this base so
    // bonuses can never double-count or freeze (CIVV / RETR / veterancy share one model).
    public float baseMaxHp;
    public float moveSpeed = 3.5f;
    // Factory-assigned base move speed, captured in Start() before civ/tech multipliers.
    // RecomputeSpeed() always derives moveSpeed from this base (Husbandry / Wheelbarrow /
    // Mongol cavalry) so multipliers never compound or freeze.
    public float baseMoveSpeed;

    public UnitState state = UnitState.Idle;
    public Vector3 targetPos;

    public ResourceNode gatherTarget;
    public Carry carrying;

    // ── Combat ──────────────────────────────────────────────────────────────
    public IDamageable attackTarget;
    public float attackCooldown;   // seconds until the next swing is allowed

    // Attack-move: advance toward attackMoveDest but engage any enemy that comes
    // within aggro range, resuming the advance after each fight. Driven by
    // CombatSystem; set by CommandSystem, cleared by any other player order.
    public bool attackMove;
    public Vector3 attackMoveDest;

    // Cavalry charge: resets after 4s out of combat; first melee hit deals 2.5× damage.
    public float chargeTimer = 4f;
    public bool  ChargeReady => type == UnitType.Cavalry && chargeTimer >= 4f;

    // Attack stance: controls auto-aggro and pursuit.
    public AttackStance stance = AttackStance.Aggressive;

    // Patrol: unit bounces between patrolA and patrolB. Cleared on any player order.
    public bool patrolActive;
    public Vector3 patrolA, patrolB;

    // Veterancy: accumulate kills → rank (0=recruit, 1=veteran, 2=elite).
    // Each rank grants +10% attack (via VeteranMult in AttackDamage) and +10 max HP
    // (via RecomputeMaxHp). Recruit/Veteran/Elite = ×1.0 / ×1.1 / ×1.2 attack.
    public int killCount;
    public int veteranRank;
    public const float VetHpPerRank = 10f;   // flat max-HP gained per veteran rank
    /// <summary>Attack multiplier from veteran rank: +10% per rank (recruit 1.0, elite 1.2).</summary>
    public float VeteranMult => 1f + 0.10f * veteranRank;

    // Monk conversion: time spent channeling on the current target.
    public float convertProgress;
    public const float ConvertTime = 4f;  // legacy fixed convert time (fallback)
    // CONV: AoE2 conversions are probabilistic — they take a variable time. Each new
    // conversion rolls a random threshold in [ConvertMinTime, ConvertMaxTime]; Theocracy
    // shortens it. convertThreshold holds the rolled target for the in-progress convert.
    public const float ConvertMinTime = 3f;
    public const float ConvertMaxTime = 7f;
    public float convertThreshold;
    // Faith: a Monk must be at full faith to start a conversion; faith drops to 0
    // after a successful convert and regenerates over time (AoE2 recharge model).
    public float faith = FaithFull;
    public const float FaithFull = 100f;
    public const float FaithRegenPerSec = 12.5f;     // ~8s to fully recharge
    public bool FaithReady => faith >= FaithFull;
    // Relic carrying (Monk): true while this Monk is hauling a relic to a Monastery.
    public bool isCarryingRelic;

    // ── Construction (villagers) ─────────────────────────────────────────────
    public BuildingEntity constructTarget;

    // ── Garrison ─────────────────────────────────────────────────────────────
    // garrisonTarget: building this unit is walking into (cleared on entry by
    // GarrisonSystem). isGarrisoned: currently sheltered inside (hidden GameObject,
    // skipped by gather/combat/build/selection so it can't act or be targeted).
    public BuildingEntity garrisonTarget;
    public bool isGarrisoned;

    // ── Armor (set by UnitFactory) ────────────────────────────────────────────
    public float meleeArmor;
    public float pierceArmor;

    // ── Naval ─────────────────────────────────────────────────────────────────
    public bool isNaval;
    public int  navalAgentTypeId = -1;

    /// <summary>This unit's per-team <see cref="TechState"/> (null-safe).</summary>
    TechState TeamTech
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null ? gm.teamTech[teamId] : null;
        }
    }

    /// <summary>This unit's civilization bonus (null-safe; returns None-bonus if no GameManager).</summary>
    CivBonus TeamCivBonus
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null ? gm.TeamCivBonus(teamId) : CivilizationDefs.Get(Civilization.None);
        }
    }

    /// <summary>Per-type combat stats. Villager only self-defends weakly; Archer/Trebuchet are ranged.</summary>
    float BaseAttackDamage => type switch
    {
        UnitType.Militia     => 5f,  UnitType.Archer      => 4f,  UnitType.Cavalry    => 8f,
        UnitType.Trebuchet   => 35f, UnitType.Spearman    => 4f,  UnitType.Longbowman => 5f,
        UnitType.Galley      => 8f,  UnitType.Skirmisher  => 3f,  UnitType.Camel      => 7f,
        UnitType.Ram         => 4f,  UnitType.Mangonel    => 25f, UnitType.CavalryArcher => 5f,
        UnitType.FireShip    => 6f,  UnitType.DemoShip    => 40f,
        // M9 unique units
        UnitType.TeutonicKnight => 12f, UnitType.WarElephant => 20f, UnitType.Mangudai => 6f,
        UnitType.Samurai     => 9f,  UnitType.Eagle       => 7f,  UnitType.EliteEagle  => 9f,
        UnitType.King        => 6f,  // Regicide king: fights but is not a front-liner
        // Support units deal no damage: Scout is pure recon (gains attack via Light Cavalry/Hussar
        // tech, applied through TechState.AttackBonus), Medic only heals.
        UnitType.Scout       => 0f,  UnitType.Medic       => 0f,
        UnitType.FishingShip => 0f,  // FISH: civilian gatherer, no attack
        _                    => 2f,
    };
    float BaseAttackRange => type switch
    {
        UnitType.Militia     => 1.3f, UnitType.Archer      => 6.5f, UnitType.Cavalry    => 1.4f,
        UnitType.Trebuchet   => 15f,  UnitType.Spearman    => 1.5f, UnitType.Longbowman => 8.5f,
        UnitType.Galley      => 5.5f, UnitType.Skirmisher  => 5f,   UnitType.Camel      => 1.4f,
        UnitType.Ram         => 1.3f, UnitType.Mangonel    => 9f,   UnitType.CavalryArcher => 4f,
        UnitType.FireShip    => 3f,   UnitType.DemoShip    => 1.5f,
        UnitType.TeutonicKnight => 1.4f, UnitType.WarElephant => 1.4f, UnitType.Mangudai => 5f,
        UnitType.Samurai     => 1.2f, UnitType.Eagle       => 1.3f,  UnitType.EliteEagle => 1.3f,
        _                    => 1.1f,
    };
    /// <summary>Effective damage = base + tech bonus, scaled by civ infantry bonus for infantry types.</summary>
    public float AttackDamage
    {
        get
        {
            float base_ = BaseAttackDamage + (TeamTech?.AttackBonus(type) ?? 0f);
            bool isInfantry = type == UnitType.Militia || type == UnitType.Spearman;
            bool isArcher = type == UnitType.Archer || type == UnitType.Longbowman
                         || type == UnitType.Skirmisher || type == UnitType.CavalryArcher;
            float withCiv = base_;
            if (isInfantry)     withCiv *= TeamCivBonus.infantryAttackMult; // Japanese/Teutons
            else if (isArcher)  withCiv *= TeamCivBonus.archerAttackMult;   // Vikings/Saracens (CIVD)
            return withCiv * VeteranMult;   // veteran rank: +10% per rank
        }
    }
    /// <summary>Effective range = base + tech bonus + civ archer range bonus (Britons).</summary>
    public float AttackRange
    {
        get
        {
            float range = BaseAttackRange + (TeamTech?.RangeBonus(type) ?? 0f);
            bool isArcher = type == UnitType.Archer || type == UnitType.Longbowman;
            return isArcher ? range + TeamCivBonus.archerRangeBonus : range;
        }
    }
    public float AttackInterval => type switch
    {
        UnitType.Militia     => 1.0f, UnitType.Archer      => 1.4f, UnitType.Cavalry    => 1.1f,
        UnitType.Trebuchet   => 5.5f, UnitType.Spearman    => 1.3f, UnitType.Longbowman => 1.6f,
        UnitType.Galley      => 2.0f, UnitType.Skirmisher  => 2.0f, UnitType.Camel      => 1.1f,
        UnitType.Ram         => 3.0f, UnitType.Mangonel    => 4.0f, UnitType.CavalryArcher => 2.0f,
        UnitType.FireShip    => 0.8f, UnitType.DemoShip    => 2.0f,
        UnitType.TeutonicKnight => 2.0f, UnitType.WarElephant => 2.5f, UnitType.Mangudai => 2.0f,
        UnitType.Samurai     => 1.3f, UnitType.Eagle       => 1.5f,  UnitType.EliteEagle => 1.4f,
        _                    => 1.6f,
    };
    /// <summary>Idle auto-acquire radius; 0 means the unit never picks fights on its own.
    /// Scout is passive recon until upgraded to Light Cavalry (then it becomes combat-capable).</summary>
    public float AggroRadius => type switch
    {
        UnitType.Militia     => 7f,  UnitType.Archer      => 9f,   UnitType.Cavalry    => 8f,
        UnitType.Trebuchet   => 15f, UnitType.Spearman    => 7f,   UnitType.Longbowman => 11f,
        UnitType.Galley      => 8f,  UnitType.Skirmisher  => 9f,   UnitType.Camel      => 8f,
        UnitType.Mangonel    => 11f, UnitType.Ram         => 4f,   UnitType.CavalryArcher => 10f,
        UnitType.FireShip    => 8f,  UnitType.DemoShip    => 6f,
        UnitType.TeutonicKnight => 7f, UnitType.WarElephant => 8f, UnitType.Mangudai => 10f,
        UnitType.Samurai     => 8f,  UnitType.Eagle       => 8f,  UnitType.EliteEagle => 8f,
        UnitType.Scout       => (TeamTech?.Has(TechType.LightCavalry) ?? false) ? 8f : 0f,
        _                    => 0f,  // King, Villager, Monk, Medic — never auto-aggro
    };
    /// <summary>Armor classes this unit belongs to (M7/ARMC) — what incoming bonus
    /// damage applies to it. Cavalry-class is shared by Camel/Scout/Cavalry Archer so
    /// the Spearman line counters all of them; Camel also carries its own class.</summary>
    public ArmorClass ArmorClasses => type switch
    {
        UnitType.Militia or UnitType.Spearman                       => ArmorClass.Infantry,
        UnitType.TeutonicKnight or UnitType.Samurai or UnitType.Eagle or UnitType.EliteEagle => ArmorClass.Infantry,
        UnitType.King => ArmorClass.Infantry,
        UnitType.Archer or UnitType.Skirmisher or UnitType.Longbowman => ArmorClass.Archer,
        UnitType.CavalryArcher                                       => ArmorClass.Archer | ArmorClass.Cavalry,
        UnitType.Mangudai                                           => ArmorClass.Archer | ArmorClass.Cavalry,
        UnitType.Cavalry or UnitType.Scout                          => ArmorClass.Cavalry,
        UnitType.WarElephant                                        => ArmorClass.Cavalry,
        UnitType.Camel                                              => ArmorClass.Cavalry | ArmorClass.Camel,
        UnitType.Trebuchet or UnitType.Mangonel or UnitType.Ram     => ArmorClass.Siege,
        UnitType.Galley or UnitType.FireShip or UnitType.DemoShip   => ArmorClass.Ship,
        UnitType.FishingShip                                        => ArmorClass.Ship,
        _                                                          => ArmorClass.None, // Villager, Monk, Medic
    };

    /// <summary>
    /// Additive bonus damage vs a target's armor classes (M7/BNUS), replacing the old
    /// multiplicative anti-cavalry / anti-archer / anti-structure factors. Values are
    /// tuned so a base-stat counter deals the same effective damage as before:
    /// Spearman +8 vs Cavalry-class (was ×3), Camel +7 (×2), Skirmisher +3 vs Archer
    /// (×2), Trebuchet +70 vs Building (×3), Ram +16 (×5).
    /// </summary>
    public float BonusDamageVs(IDamageable target)
    {
        if (target == null) return 0f;
        ArmorClass tc = target.ArmorClasses;
        float bonus = 0f;
        switch (type)
        {
            case UnitType.Spearman:   if ((tc & ArmorClass.Cavalry)  != 0) bonus += 8f;  break;
            case UnitType.Camel:      if ((tc & ArmorClass.Cavalry)  != 0) bonus += 7f;  break;
            case UnitType.Skirmisher: if ((tc & ArmorClass.Archer)   != 0) bonus += 3f;  break;
            case UnitType.Trebuchet:  if ((tc & ArmorClass.Building) != 0) bonus += 70f; break;
            case UnitType.Ram:        if ((tc & ArmorClass.Building) != 0) bonus += 16f; break;
            case UnitType.WarElephant:if ((tc & ArmorClass.Building) != 0) bonus += 30f; break;  // CIVU
            case UnitType.Mangudai:   if ((tc & ArmorClass.Siege)    != 0) bonus += 10f; break;  // CIVU anti-siege
        }
        return bonus;
    }
    /// <summary>Minimum attack range: siege weapons can't fire at point-blank targets.</summary>
    public float MinAttackRange => type switch
    {
        UnitType.Trebuchet => 3f,
        UnitType.Mangonel  => 2f,
        UnitType.Galley    => 1.5f,
        _                  => 0f,
    };
    /// <summary>Area-of-effect splash radius for projectiles (0 = single target).</summary>
    public float SplashRadius => type switch
    {
        UnitType.Mangonel => 1.8f,
        UnitType.DemoShip => 2.5f,   // explosive area attack
        _                 => 0f,
    };
    /// <summary>First melee charge hit by a Cavalry unit deals 2.5× damage.</summary>
    public float ChargeMultiplier => type == UnitType.Cavalry ? 2.5f : 1f;
    /// <summary>Damage class this unit deals.</summary>
    public DamageType DamageKind => type switch
    {
        UnitType.Archer      => DamageType.Pierce,
        UnitType.Longbowman  => DamageType.Pierce,
        UnitType.Skirmisher  => DamageType.Pierce,
        UnitType.CavalryArcher => DamageType.Pierce,
        UnitType.Mangudai    => DamageType.Pierce,
        UnitType.Galley      => DamageType.Pierce,
        UnitType.FireShip    => DamageType.Pierce,
        UnitType.Trebuchet   => DamageType.Siege,
        UnitType.Mangonel    => DamageType.Siege,
        UnitType.Ram         => DamageType.Siege,
        UnitType.DemoShip    => DamageType.Siege,
        _                    => DamageType.Melee,
    };
    /// <summary>Ranged units attack via projectiles instead of melee contact.</summary>
    public bool IsRanged => type == UnitType.Archer || type == UnitType.Trebuchet
        || type == UnitType.Longbowman || type == UnitType.Galley || type == UnitType.Skirmisher
        || type == UnitType.Mangonel || type == UnitType.CavalryArcher
        || type == UnitType.FireShip || type == UnitType.DemoShip
        || type == UnitType.Mangudai;

    // ── Medic healing (driven by CombatSystem.StepHeal) ──────────────────────
    /// <summary>Radius within which a Medic auto-heals friendly units; 0 = not a healer.</summary>
    public float HealRadius => type == UnitType.Medic ? 6f : 0f;
    /// <summary>Hitpoints a Medic restores per second to the chosen ally.</summary>
    public float HealPower  => type == UnitType.Medic ? 3f : 0f;

    NavMeshAgent _agent;
    SelectionRing _ring;

    void Awake()
    {
        _ring = GetComponentInChildren<SelectionRing>(true);
        _animator = GetComponentInChildren<Animator>();   // null for primitive units

        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
        _agent.angularSpeed = 360f;
        _agent.acceleration = 12f;
        _agent.stoppingDistance = 0.25f;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        _agent.avoidancePriority = Random.Range(30, 70);
        _agent.autoRepath = true;

        if (isNaval && navalAgentTypeId >= 0)
        {
            // Galley navigates on the water NavMesh only.
            _agent.agentTypeID = navalAgentTypeId;
            _agent.radius = 0.5f;
            _agent.height = 0.8f;
        }
        else
        {
            _agent.radius = 0.4f;
            _agent.height = 1.8f;
        }
    }

    // Start (not Awake): the factory assigns `type`/`moveSpeed` right after
    // AddComponent, so the per-type speed must be pushed to the agent one step later.
    void Start()
    {
        // Capture the factory-assigned bases before layering bonuses (single source of truth).
        baseMaxHp = maxHp;
        baseMoveSpeed = moveSpeed;

        // Speed: base × civ cavalry bonus (Mongols) × tech (Husbandry/Wheelbarrow), live-recomputable.
        RecomputeSpeed();

        // Derive maxHp from base + researched tech HP + veterancy + civ cavalry HP,
        // and fill current hp to full on spawn.
        RecomputeMaxHp();
    }

    /// <summary>
    /// Recompute <see cref="moveSpeed"/> from <see cref="baseMoveSpeed"/> × civ cavalry
    /// speed (Mongols) × tech speed (Husbandry / Wheelbarrow). Idempotent — always from
    /// base, so research-time recompute never compounds. Pushes the result to the agent.
    /// </summary>
    public void RecomputeSpeed()
    {
        if (baseMoveSpeed <= 0f) baseMoveSpeed = moveSpeed;
        float s = baseMoveSpeed;
        if (type == UnitType.Cavalry) s *= TeamCivBonus.cavalrySpeedMult;
        s *= TeamTech?.MoveSpeedMult(type) ?? 1f;
        moveSpeed = s;
        if (_agent != null) _agent.speed = s;
    }

    /// <summary>
    /// Recompute <see cref="maxHp"/> from <see cref="baseMaxHp"/> plus all live bonuses
    /// (team tech HP, veteran rank, Franks cavalry HP multiplier). Idempotent — always
    /// derived from base, so repeated calls (spawn / research / rank-up) never double-count.
    /// On an increase, current hp rises by the same delta; on a decrease it is clamped.
    /// </summary>
    public void RecomputeMaxHp(bool fillOnIncrease = true)
    {
        // Safety: if a research-triggered recompute fires before this unit's Start()
        // captured the factory base, seed baseMaxHp from the current maxHp now.
        if (baseMaxHp <= 0f) baseMaxHp = maxHp;

        float computed = baseMaxHp
            + (TeamTech?.HpBonus(type) ?? 0f)
            + veteranRank * VetHpPerRank;
        if (type == UnitType.Cavalry) computed *= TeamCivBonus.cavalryHpMult;

        float delta = computed - maxHp;
        maxHp = computed;
        if (delta > 0f && fillOnIncrease) hp = Mathf.Min(hp + delta, maxHp);
        else if (hp > maxHp) hp = maxHp;
    }

    /// <summary>Issue a move order; transitions to <see cref="UnitState.Moving"/>.</summary>
    public void MoveTo(Vector3 pos)
    {
        targetPos = pos;
        state = UnitState.Moving;
        Navigate(pos);
    }

    /// <summary>
    /// Update the nav destination without changing state.
    /// Used by <see cref="GatherSystem"/> when redirecting while already in a non-Moving state.
    /// </summary>
    public void NavigateTo(Vector3 pos)
    {
        targetPos = pos;
        Navigate(pos);
    }

    public void Stop()
    {
        state = UnitState.Idle;
        patrolActive = false;
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }
        if (gatherTarget != null)
            gatherTarget.currentGatherers = Mathf.Max(0, gatherTarget.currentGatherers - 1);
        gatherTarget = null;
        attackTarget = null;
        constructTarget = null;
        carrying.Clear();
    }

    // ── Combat API (driven by CombatSystem) ─────────────────────────────────

    /// <summary>Issue an attack order against an enemy unit or building.</summary>
    public void AttackOrder(IDamageable target)
    {
        if (target == null || !target.IsAlive) return;
        // Drop any gather assignment before switching to combat.
        if (gatherTarget != null)
        {
            gatherTarget.currentGatherers = Mathf.Max(0, gatherTarget.currentGatherers - 1);
            gatherTarget = null;
        }
        carrying.Clear();
        attackTarget = target;
        state = UnitState.MovingToAttack;
    }

    /// <summary>Send this villager to build/repair a construction site.</summary>
    public void BuildOrder(BuildingEntity site)
    {
        if (type != UnitType.Villager || site == null) return;
        if (gatherTarget != null)
        {
            gatherTarget.currentGatherers = Mathf.Max(0, gatherTarget.currentGatherers - 1);
            gatherTarget = null;
        }
        attackTarget = null;
        carrying.Clear();
        constructTarget = site;
        state = UnitState.Moving;
    }

    /// <summary>Send this unit to garrison inside a friendly building (GarrisonSystem
    /// completes the entry once the unit reaches the building).</summary>
    public void GarrisonOrder(BuildingEntity building)
    {
        if (building == null || !building.IsAlive) return;
        if (gatherTarget != null)
        {
            gatherTarget.currentGatherers = Mathf.Max(0, gatherTarget.currentGatherers - 1);
            gatherTarget = null;
        }
        attackTarget = null;
        constructTarget = null;
        carrying.Clear();
        garrisonTarget = building;
        MoveTo(building.transform.position);
    }

    /// <summary>Hide this unit inside a building: no render, collision, agent or orders.
    /// Population is preserved because it stays in <c>GameManager.units</c>.</summary>
    public void EnterGarrison()
    {
        Stop();                                 // release gather slot, clear orders
        var gm = GameManager.Instance;
        gm?.selection?.Selected.Remove(this);
        SetSelected(false, Color.white);
        garrisonTarget = null;
        isGarrisoned = true;
        gameObject.SetActive(false);            // suspends renderer + collider + agent
    }

    /// <summary>Re-emerge from a building at <paramref name="emergePos"/> and walk to
    /// <paramref name="dest"/> (rally point or gate front).</summary>
    public void ExitGarrison(Vector3 emergePos, Vector3 dest)
    {
        transform.position = emergePos;
        gameObject.SetActive(true);
        isGarrisoned = false;
        if (_agent != null) _agent.Warp(emergePos); // re-anchor on the NavMesh
        MoveTo(dest);
    }

    /// <summary>Instantly kill this unit (e.g. the building it garrisoned was destroyed).</summary>
    public void Kill()
    {
        if (hp <= 0f) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true); // so Destroy + events run cleanly
        hp = 0f;
        Die();
    }

    /// <summary>Halt the agent in place without leaving the current (combat) state.</summary>
    public void HaltAgent()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    /// <summary>Rotate to face a world point, ignoring pitch.</summary>
    public void FaceToward(Vector3 worldPos)
    {
        Vector3 d = worldPos - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(d), 12f * Time.deltaTime);
    }

    // ── IDamageable ─────────────────────────────────────────────────────────

    public int TeamId => teamId;
    public bool IsAlive => this != null && hp > 0f;
    public Transform Transform => transform;
    public float Radius => 0.4f;

    // N1: KayKit (skinned) models stand taller than primitive units, so the HP bar sits higher.
    // Cache the check — otherwise the IMGUI HP-bar pass calls GetComponentInChildren over every
    // unit every frame (a real per-frame cost at scale).
    bool? _isKayKit;
    public bool IsKayKitModel => _isKayKit ??= GetComponentInChildren<SkinnedMeshRenderer>() != null;

    public void TakeDamage(float amount, DamageType damageType = DamageType.Melee)
    {
        if (hp <= 0f) return;
        // Base armor (UnitFactory) + live Blacksmith armor research (ChainMail/PlateMail,
        // barding, archer armor, Loom) read from this team's TechState each hit.
        var tech = TeamTech;
        float armor = damageType switch
        {
            DamageType.Pierce => pierceArmor + (tech?.ArmorBonus(type, DamageType.Pierce) ?? 0f),
            DamageType.Melee  => meleeArmor + (tech?.ArmorBonus(type, DamageType.Melee) ?? 0f),
            // N0.1: siege deals melee-class damage reduced by melee armor (was: bypass all
            // armor). Armored units (Paladin/Teutonic Knight) no longer take full siege damage;
            // siege stays strong vs buildings via anti-structure BonusDamageVs, not armor bypass.
            DamageType.Siege  => meleeArmor + (tech?.ArmorBonus(type, DamageType.Melee) ?? 0f),
            _                 => 0f,
        };
        hp -= Mathf.Max(1f, amount - armor);
        if (gameObject.activeInHierarchy) StartCoroutine(HitFlash());
        if (hp <= 0f) Die();
    }

    System.Collections.IEnumerator HitFlash()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            r.material.EnableKeyword("_EMISSION");
            r.material.SetColor("_EmissionColor", Color.white * 1.5f);
        }
        yield return new WaitForSeconds(0.08f);
        foreach (var r in renderers)
            r.material.SetColor("_EmissionColor", Color.black);
    }

    /// <summary>Restore hp up to maxHp (Medic healing). No-op once dead.</summary>
    public void Heal(float amount)
    {
        if (hp <= 0f || amount <= 0f) return;
        hp = Mathf.Min(hp + amount, maxHp);
    }

    void Die()
    {
        hp = 0f;
        PlayDie(); // fire death animation before Destroy
        AudioManager.Play(AudioManager.SoundId.UnitDie, 0.8f);
        // List removal is deferred to GameManager's end-of-frame compaction so we
        // don't mutate gm.units while CombatSystem is iterating it.
        var gm = GameManager.Instance;
        gm?.selection?.Selected.Remove(this);
        if (gatherTarget != null)
            gatherTarget.currentGatherers = Mathf.Max(0, gatherTarget.currentGatherers - 1);
        GameEvents.FireUnitKilled(this, teamId);
        Destroy(gameObject);
    }

    public void SetSelected(bool selected, Color color)
    {
        if (_ring == null) return;
        if (selected) _ring.Show(color);
        else _ring.Hide();
    }

    public bool IsMoving => state == UnitState.Moving || state == UnitState.ReturningToDropoff;

    /// <summary>Record a kill and promote the unit if thresholds are reached
    /// (1 kill = Veteran, 3 kills = Elite). Returns true if rank went up.</summary>
    public bool AddKill()
    {
        killCount++;
        int newRank = killCount >= 3 ? 2 : killCount >= 1 ? 1 : 0;
        if (newRank <= veteranRank) return false;
        veteranRank = newRank;
        RecomputeMaxHp();
        ApplyVeteranTint();
        return true;
    }

    // Veteran tint: recruit = team color, veteran = slight gold shift, elite = gold.
    void ApplyVeteranTint()
    {
        if (veteranRank == 0) return;
        Color gold = veteranRank == 2 ? new Color(1f, 0.85f, 0.1f) : new Color(0.9f, 0.75f, 0.3f);
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", Color.Lerp(Color.white, gold, veteranRank == 2 ? 0.5f : 0.28f));
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.SetPropertyBlock(block);
        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.gameObject.name == "BlobShadow" || mr.gameObject.name.StartsWith("SelectionRing")) continue;
            mr.material.color = Color.Lerp(mr.material.color, gold, 0.2f);
        }
    }

    float _bobPhase;
    Animator _animator;
    static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    static readonly int AnimAttack   = Animator.StringToHash("Attack");
    static readonly int AnimDie      = Animator.StringToHash("Die");

    /// <summary>Fire the attack animation. No-op for primitive units (no Animator).</summary>
    public void PlayAttack() { if (_animator != null) _animator.SetTrigger(AnimAttack); }

    /// <summary>Fire the death animation and freeze locomotion. No-op for primitive units.</summary>
    public void PlayDie()
    {
        if (_animator == null) return;
        _animator.SetTrigger(AnimDie);
        _animator.SetBool(AnimIsMoving, false);
    }

    void Update()
    {
        if (_animator != null)
        {
            // Animated units: drive the idle/walk blend from actual agent motion.
            bool moving = _agent != null && _agent.isOnNavMesh
                          && _agent.velocity.sqrMagnitude > 0.04f;
            _animator.SetBool(AnimIsMoving, moving);
        }
        else
        {
            // Procedural movement bob: primitive unit root bobs up/down while moving.
            bool isMoving = state == UnitState.Moving;
            if (isMoving)
            {
                _bobPhase += Time.deltaTime * 8f;
                float bob = Mathf.Sin(_bobPhase) * 0.04f;
                var pos = transform.localPosition;
                transform.localPosition = new Vector3(pos.x, bob, pos.z);
            }
        }

        // Only auto-idle for plain move orders; gather/build transitions are their systems' job.
        if (state != UnitState.Moving || gatherTarget != null || constructTarget != null) return;
        if (!_agent.isOnNavMesh || _agent.pathPending) return;
        if (_agent.remainingDistance > _agent.stoppingDistance) return;

        state = UnitState.Idle;

        // Patrol: bounce between A and B.
        if (patrolActive)
        {
            var next = patrolB;
            patrolB = patrolA;
            patrolA = next;
            MoveTo(patrolB);
        }
    }

    void Navigate(Vector3 pos)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        _agent.SetDestination(pos);
    }
}
