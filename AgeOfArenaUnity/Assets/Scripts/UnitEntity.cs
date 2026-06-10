using System.Collections.Generic;
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

    // Shift-queue: extra waypoints appended with Shift+right-click. Processed in order;
    // when the queue empties the unit idles. Cleared by any non-queued order (Stop included).
    [System.NonSerialized] public readonly Queue<Vector3> moveQueue = new Queue<Vector3>();

    // Patrol: unit bounces between patrolA and patrolB. Cleared on any player order.
    public bool patrolActive;
    public Vector3 patrolA, patrolB;

    // Trade cart round-trip state (driven by TradingSystem, NOT the patrol auto-flip —
    // a separate flag so Update's patrol bounce doesn't double-drive the cart). patrolA =
    // home market, patrolB = target market; tradeReturning = on the home-bound leg.
    [System.NonSerialized] public bool tradeActive;
    [System.NonSerialized] public bool tradeReturning;

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

    // N8.siege: pre-loaded demolished wreck prefab; spawned at world position on death
    public GameObject demolishedPrefab;

    /// <summary>This unit's per-team <see cref="TechState"/> (null-safe).</summary>
    public TechState TeamTech
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
    float BaseAttackDamage => UnitRegistry.Get(type).baseAtk;
    float BaseAttackRange  => UnitRegistry.Get(type).baseRange;
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
    public float AttackInterval => UnitRegistry.Get(type).attackInterval * (TeamTech?.AttackIntervalMult(type) ?? 1f);
    /// <summary>Idle auto-acquire radius; 0 means the unit never picks fights on its own.
    /// Scout is passive recon until upgraded to Light Cavalry (then it becomes combat-capable).</summary>
    public float AggroRadius
    {
        get
        {
            if (type == UnitType.Scout)
                return (TeamTech?.Has(TechType.LightCavalry) ?? false) ? 8f : 0f;
            return UnitRegistry.Get(type).aggroRadius;
        }
    }
    /// <summary>Armor classes this unit belongs to (M7/ARMC) — what incoming bonus
    /// damage applies to it. Cavalry-class is shared by Camel/Scout/Cavalry Archer so
    /// the Spearman line counters all of them; Camel also carries its own class.</summary>
    public ArmorClass ArmorClasses => UnitRegistry.Get(type).armorClasses;

    /// <summary>
    /// Additive bonus damage vs a target's armor classes (M7/BNUS → N6/BONUS stacking).
    /// Each bonus applies independently per matching armor class, so a target that carries
    /// multiple classes (e.g. CavalryArcher = Archer|Cavalry; Mameluke = Cavalry|Camel)
    /// receives ALL applicable bonuses from a single attacker (AoE2 stacking rule).
    /// </summary>
    public float BonusDamageVs(IDamageable target)
    {
        if (target == null) return 0f;
        ArmorClass tc  = target.ArmorClasses;
        float      sum = 0f;
        var entries = UnitRegistry.Get(type).bonusVs;
        if (entries != null)
            foreach (var e in entries)
                if ((tc & e.cls) != 0) sum += e.bonus;
        sum += TeamTech?.BonusTechDamage(type, target) ?? 0f;
        return sum;
    }
    /// <summary>Minimum attack range: siege weapons can't fire at point-blank targets.</summary>
    public float MinAttackRange => UnitRegistry.Get(type).minAttackRange;
    /// <summary>Area-of-effect splash radius for projectiles (0 = single target).</summary>
    public float SplashRadius => UnitRegistry.Get(type).splashRadius;
    /// <summary>N6: blast-damage falloff for a secondary victim at fractional distance
    /// <paramref name="t"/>∈[0,1] from the impact point. DemoShip is a full-power explosion
    /// everywhere in the radius; Mangonel-style siege deals full damage in the inner half of
    /// the blast and 50% in the outer half (AoE2 "blast level" rings, simplified).</summary>
    public float SplashFalloffAt(float t)
        => type == UnitType.DemoShip ? 1f : (t <= 0.5f ? 1f : 0.5f);
    /// <summary>First melee charge hit by a Cavalry unit deals 2.5× damage.</summary>
    public float ChargeMultiplier => type == UnitType.Cavalry ? 2.5f : 1f;
    /// <summary>Damage class this unit deals.</summary>
    public DamageType DamageKind => UnitRegistry.Get(type).damageKind;
    /// <summary>Ranged units attack via projectiles instead of melee contact.</summary>
    public bool IsRanged => UnitRegistry.Get(type).isRanged;

    // ── Medic healing (driven by CombatSystem.StepHeal) ──────────────────────
    /// <summary>Radius within which a Medic auto-heals friendly units; 0 = not a healer.</summary>
    public float HealRadius => UnitRegistry.Get(type).healRadius;
    /// <summary>Hitpoints a Medic restores per second to the chosen ally.</summary>
    public float HealPower  => UnitRegistry.Get(type).healPower;

    /// <summary>N4/CIVU: HP this unit regenerates on its own each second (Vikings'
    /// Berserk signature trait); 0 = no self-regen. Berserkergang (N4/CIVT) doubles it.
    /// Applied in CombatSystem's tick.</summary>
    public float SelfRegenPerSecond
    {
        get
        {
            float base_ = UnitRegistry.Get(type).selfRegen;
            if (base_ <= 0f) return 0f;
            return base_ * ((TeamTech?.Has(TechType.Berserkergang) ?? false) ? 2f : 1f);
        }
    }

    NavMeshAgent _agent;
    SelectionRing _ring;

    /// <summary>N6.ballistics: current world-space velocity for projectile lead computation.</summary>
    public Vector3 AgentVelocity => _agent != null ? _agent.velocity : Vector3.zero;

    void Awake()
    {
        _ring = GetComponentInChildren<SelectionRing>(true);
        _animator = FindDrivenAnimator();   // null for primitive/static-model units

        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
        _agent.angularSpeed = 360f;
        _agent.acceleration = 12f;
        _agent.stoppingDistance = 0.25f;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        _agent.avoidancePriority = SimRandom.Range(30, 70); // N3: sim RNG (deterministic)
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
        // Naval agent type CAN'T be set in Awake: the factory assigns isNaval one line AFTER
        // AddComponent (which already ran Awake with isNaval still false), so boats were born
        // on the LAND agent type and never bound to the water mesh. Fix it here, then Warp to
        // rebind the agent onto the naval NavMesh at its (already-on-water) spawn position.
        if (isNaval && navalAgentTypeId >= 0 && _agent != null && _agent.agentTypeID != navalAgentTypeId)
        {
            _agent.agentTypeID = navalAgentTypeId;
            _agent.radius = 0.5f;
            _agent.height = 0.8f;
            _agent.Warp(transform.position);
        }

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
        attackMove   = false;   // stop cancels attack-move mode (like patrolActive)
        tradeActive  = false;   // a manual order cancels an in-progress trade route
        moveQueue.Clear();
        if (_agent != null && _agent.isOnNavMesh)
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
        // Reject buildings that can't garrison anyone (e.g. Bombard Tower, capacity 0) up front,
        // instead of walking there forever and silently dropping the order on arrival.
        if (building.GarrisonCapacity <= 0) return;
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
        if (_agent == null || !_agent.isOnNavMesh) return;
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

    // Cached HP-bar reference (set at RegisterUnit) so CombatSystem's LateUpdate doesn't
    // GetComponent<WorldHpBar>() for every unit every frame.
    [System.NonSerialized] public WorldHpBar hpBar;

    // Drop-off chosen at BeginReturn, reused while returning so GatherSystem doesn't
    // re-scan all buildings every tick for every returning villager.
    [System.NonSerialized] public BuildingEntity dropoffTarget;

    // Material tinting via MaterialPropertyBlock (NO per-hit material instancing/leak).
    // Renderers are cached once; the block is read-modified-written so HitFlash (_EmissionColor)
    // and ApplyVeteranTint (_Color) coexist without clobbering each other.
    MeshRenderer[] _flashRenderers;
    MaterialPropertyBlock _mpb;
    static readonly int ColorId    = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public void TakeDamage(float amount, DamageType damageType = DamageType.Melee)
    {
        if (hp <= 0f) return;
        // Base armor (UnitFactory) + live Blacksmith armor research (ChainMail/PlateMail,
        // barding, archer armor, Loom) read from this team's TechState each hit.
        var tech = TeamTech;
        // N2: base armor (incl. the N0.1 siege=melee mapping) + net-damage formula come from the
        // pure CombatMath; the live Blacksmith/tech armor bonus is entity-specific, read here.
        // Melee & Siege both benefit from melee armor research (N0.1).
        float baseArmor = CombatMath.ArmorFor(damageType, meleeArmor, pierceArmor);
        float techArmor = damageType == DamageType.Pierce
            ? (tech?.ArmorBonus(type, DamageType.Pierce) ?? 0f)
            : (tech?.ArmorBonus(type, DamageType.Melee)  ?? 0f);
        hp -= CombatMath.NetDamage(amount, baseArmor + techArmor);
        if (gameObject.activeInHierarchy) StartCoroutine(HitFlash());
        if (hp <= 0f) Die();
    }

    System.Collections.IEnumerator HitFlash()
    {
        _flashRenderers ??= GetComponentsInChildren<MeshRenderer>();   // cache once, no per-hit alloc
        _mpb ??= new MaterialPropertyBlock();
        for (int i = 0; i < _flashRenderers.Length; i++)
        {
            var r = _flashRenderers[i];
            if (r == null) continue;
            // Keyword toggled on the SHARED material (idempotent, no instancing); emission
            // color driven per-renderer via the property block so nothing leaks.
            if (r.sharedMaterial != null) r.sharedMaterial.EnableKeyword("_EMISSION");
            r.GetPropertyBlock(_mpb);                       // preserve any _Color (veteran tint)
            _mpb.SetColor(EmissionId, Color.white * 1.5f);
            r.SetPropertyBlock(_mpb);
        }
        yield return new WaitForSeconds(0.08f);
        for (int i = 0; i < _flashRenderers.Length; i++)
        {
            var r = _flashRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionId, Color.black);
            r.SetPropertyBlock(_mpb);
        }
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
        if (demolishedPrefab != null)
        {
            var wreck = Object.Instantiate(demolishedPrefab, transform.position, transform.rotation);
            Object.Destroy(wreck, 5f);
        }
        PlayDie(); // fire death animation before Destroy
        AudioManager.PlayAt(AudioManager.SoundId.UnitDie, transform.position, 0.8f);
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
        MetaSystem.OnKill(teamId); // N13.meta: achievement tracking
        int newRank = killCount >= 3 ? 2 : killCount >= 1 ? 1 : 0;
        if (newRank <= veteranRank) return false;
        veteranRank = newRank;
        RecomputeMaxHp();
        ApplyVeteranTint();
        MetaSystem.OnVeteranRankUp(this); // N13.meta
        return true;
    }

    // Veteran tint: recruit = team color, veteran = slight gold shift, elite = gold.
    void ApplyVeteranTint()
    {
        if (veteranRank == 0) return;
        Color gold = veteranRank == 2 ? new Color(1f, 0.85f, 0.1f) : new Color(0.9f, 0.75f, 0.3f);
        _mpb ??= new MaterialPropertyBlock();
        Color tint = Color.Lerp(Color.white, gold, veteranRank == 2 ? 0.5f : 0.28f);
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, tint);
            smr.SetPropertyBlock(_mpb);
        }
        // MeshRenderer (prim) path: use the property block instead of mr.material (which
        // instantiates a leaked material per renderer). Lerp from the shared base color.
        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.gameObject.name == "BlobShadow" || mr.gameObject.name.StartsWith("SelectionRing")) continue;
            Color baseCol = mr.sharedMaterial != null ? mr.sharedMaterial.color : Color.white;
            mr.GetPropertyBlock(_mpb);                       // preserve any _EmissionColor (hit flash)
            _mpb.SetColor(ColorId, Color.Lerp(baseCol, gold, veteranRank == 2 ? 0.4f : 0.2f));
            mr.SetPropertyBlock(_mpb);
        }
    }

    float _bobPhase;
    Animator _animator;
    static readonly int AnimIsMoving  = Animator.StringToHash("IsMoving");
    static readonly int AnimAttack    = Animator.StringToHash("Attack");
    static readonly int AnimDie       = Animator.StringToHash("Die");

    Animator FindDrivenAnimator()
    {
        foreach (var animator in GetComponentsInChildren<Animator>())
        {
            if (animator.runtimeAnimatorController != null)
                return animator;
        }
        return null;
    }

    /// <summary>Fire the attack animation. No-op for primitive units (no Animator).</summary>
    public void PlayAttack() { if (_animator != null) _animator.SetTrigger(AnimAttack); }

    /// <summary>N8.anim: Fire a gather/build "swing" animation (reuses Attack trigger).</summary>
    public void PlayWorkSwing() { if (_animator != null) _animator.SetTrigger(AnimAttack); }

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
            // Procedural animation for primitive (non-KayKit) units.
            bool isMoving = state == UnitState.Moving;
            if (isMoving)
            {
                // Bob up/down while walking.
                _bobPhase += Time.deltaTime * 8f;
                float bob = Mathf.Sin(_bobPhase) * 0.04f;
                var pos = transform.localPosition;
                transform.localPosition = new Vector3(pos.x, bob, pos.z);
            }
            // N8.anim: attack swing — brief Y rotation oscillation on Attacking.
            if (state == UnitState.Attacking)
            {
                _bobPhase += Time.deltaTime * 16f;
                float swing = Mathf.Sin(_bobPhase) * 18f;
                var euler = transform.localEulerAngles;
                transform.localEulerAngles = new Vector3(euler.x, euler.y + swing, euler.z);
            }
        }

        // Only auto-idle for plain move orders; gather/build transitions are their systems' job.
        if (state != UnitState.Moving || gatherTarget != null || constructTarget != null) return;
        if (_agent == null || !_agent.isOnNavMesh || _agent.pathPending) return;
        if (_agent.remainingDistance > _agent.stoppingDistance) return;

        state = UnitState.Idle;

        // Shift-queue: move to the next queued waypoint if one exists.
        if (moveQueue.Count > 0)
        {
            MoveTo(moveQueue.Dequeue());
            return;
        }

        // Patrol: bounce between A and B.
        if (patrolActive)
        {
            var next = patrolB;
            patrolB = patrolA;
            patrolA = next;
            MoveTo(patrolB);
        }
    }

    // Max distance to pull an off-mesh move target onto the nearest walkable point. The
    // "Ground" collider spans the WHOLE terrain disc, but walls, the coastal-forest ring,
    // steep terrain and the map rim are carved OUT of the NavMesh. A right-click there hands
    // SetDestination an off-mesh point that silently fails — the unit just never moves. (Gather
    // never hit this because resource approach points are always inside the walkable area.)
    const float NavSampleRadius = 6f;

    void Navigate(Vector3 pos)
    {
        if (_agent == null || !_agent.isOnNavMesh) return;
        // Snap the destination onto THIS agent's NavMesh (areaMask keeps land units off the
        // naval mesh) so off-mesh clicks resolve to the nearest reachable point.
        if (NavMesh.SamplePosition(pos, out var navHit, NavSampleRadius, _agent.areaMask))
            pos = navHit.position;
        _agent.isStopped = false;
        _agent.SetDestination(pos);
    }
}
