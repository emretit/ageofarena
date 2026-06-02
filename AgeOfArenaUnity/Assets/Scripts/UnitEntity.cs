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
    public float moveSpeed = 3.5f;

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

    // Monk conversion: time spent channeling on the current target.
    public float convertProgress;
    public const float ConvertTime = 4f;  // seconds to convert an enemy unit

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

    /// <summary>This unit's per-team <see cref="TechState"/> (null-safe).</summary>
    TechState TeamTech
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null ? gm.teamTech[teamId] : null;
        }
    }

    /// <summary>Per-type combat stats. Villager only self-defends weakly; Archer/Trebuchet are ranged.</summary>
    float BaseAttackDamage => type switch
    {
        UnitType.Militia   => 5f,  UnitType.Archer    => 4f,  UnitType.Cavalry  => 8f,
        UnitType.Trebuchet => 35f, UnitType.Spearman  => 4f,
        // Support units deal no damage: Scout is pure recon, Medic only heals.
        UnitType.Scout     => 0f,  UnitType.Medic     => 0f,
        _                  => 2f,
    };
    float BaseAttackRange => type switch
    {
        UnitType.Militia   => 1.3f, UnitType.Archer   => 6.5f, UnitType.Cavalry  => 1.4f,
        UnitType.Trebuchet => 15f,  UnitType.Spearman => 1.5f, _                 => 1.1f,
    };
    /// <summary>Effective damage = base + researched tech bonus (read live each swing).</summary>
    public float AttackDamage => BaseAttackDamage + (TeamTech?.AttackBonus(type) ?? 0f);
    public float AttackRange  => BaseAttackRange  + (TeamTech?.RangeBonus(type)  ?? 0f);
    public float AttackInterval => type switch
    {
        UnitType.Militia   => 1.0f, UnitType.Archer   => 1.4f, UnitType.Cavalry  => 1.1f,
        UnitType.Trebuchet => 5.5f, UnitType.Spearman => 1.3f, _                 => 1.6f,
    };
    /// <summary>Idle auto-acquire radius; 0 means the unit never picks fights on its own.</summary>
    public float AggroRadius => type switch
    {
        UnitType.Militia   => 7f, UnitType.Archer   => 9f,  UnitType.Cavalry  => 8f,
        UnitType.Trebuchet => 15f, UnitType.Spearman => 7f, _                 => 0f,
    };
    /// <summary>Trebuchet deals 3× damage vs buildings; others have no multiplier.</summary>
    public float AntiStructureMultiplier => type == UnitType.Trebuchet ? 3f : 1f;
    /// <summary>First melee charge hit by a Cavalry unit deals 2.5× damage.</summary>
    public float ChargeMultiplier => type == UnitType.Cavalry ? 2.5f : 1f;
    /// <summary>Spearman deals 3× damage vs Cavalry (anti-cavalry counter edge).</summary>
    public float AntiCavalryMultiplier => type == UnitType.Spearman ? 3.0f : 1f;
    /// <summary>Damage class this unit deals.</summary>
    public DamageType DamageKind => type switch
    {
        UnitType.Archer    => DamageType.Pierce,
        UnitType.Trebuchet => DamageType.Siege,
        _                  => DamageType.Melee,
    };
    /// <summary>Ranged units attack via projectiles instead of melee contact.</summary>
    public bool IsRanged => type == UnitType.Archer || type == UnitType.Trebuchet;

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

        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
        _agent.angularSpeed = 360f;
        _agent.acceleration = 12f;
        _agent.stoppingDistance = 0.25f;
        _agent.radius = 0.4f;
        _agent.height = 1.8f;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        _agent.avoidancePriority = Random.Range(30, 70);
        _agent.autoRepath = true;
    }

    // Start (not Awake): the factory assigns `type`/`moveSpeed` right after
    // AddComponent, so the per-type speed must be pushed to the agent one step later.
    void Start()
    {
        if (_agent != null) _agent.speed = moveSpeed;

        // Newly trained/spawned units inherit their team's researched hp upgrades.
        float hpBonus = TeamTech?.HpBonus(type) ?? 0f;
        if (hpBonus > 0f) { maxHp += hpBonus; hp += hpBonus; }
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

    public void TakeDamage(float amount, DamageType damageType = DamageType.Melee)
    {
        if (hp <= 0f) return;
        float armor = damageType switch
        {
            DamageType.Pierce => pierceArmor,
            DamageType.Melee  => meleeArmor,
            _                 => 0f,  // Siege bypasses armor
        };
        hp -= Mathf.Max(1f, amount - armor);
        StartCoroutine(HitFlash());
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

    void Update()
    {
        // Only auto-idle for plain move orders; gather/build transitions are their systems' job.
        if (state != UnitState.Moving || gatherTarget != null || constructTarget != null) return;
        if (!_agent.isOnNavMesh || _agent.pathPending) return;
        if (_agent.remainingDistance <= _agent.stoppingDistance)
            state = UnitState.Idle;
    }

    void Navigate(Vector3 pos)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        _agent.SetDestination(pos);
    }
}
