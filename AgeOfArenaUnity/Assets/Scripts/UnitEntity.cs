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

    // ── Construction (villagers) ─────────────────────────────────────────────
    public BuildingEntity constructTarget;

    /// <summary>Per-type combat stats. Villager only self-defends weakly; Archer is ranged.</summary>
    public float AttackDamage => type switch
    {
        UnitType.Militia => 5f, UnitType.Archer => 4f, UnitType.Cavalry => 8f, _ => 2f,
    };
    public float AttackRange => type switch
    {
        UnitType.Militia => 1.3f, UnitType.Archer => 6.5f, UnitType.Cavalry => 1.4f, _ => 1.1f,
    };
    public float AttackInterval => type switch
    {
        UnitType.Militia => 1.0f, UnitType.Archer => 1.4f, UnitType.Cavalry => 1.1f, _ => 1.6f,
    };
    /// <summary>Idle auto-acquire radius; 0 means the unit never picks fights on its own.</summary>
    public float AggroRadius => type switch
    {
        UnitType.Militia => 7f, UnitType.Archer => 9f, UnitType.Cavalry => 8f, _ => 0f,
    };
    /// <summary>Archers attack from range via projectiles instead of melee contact.</summary>
    public bool IsRanged => type == UnitType.Archer;

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

    public void TakeDamage(float amount)
    {
        if (hp <= 0f) return;
        hp -= amount;
        if (hp <= 0f) Die();
    }

    void Die()
    {
        hp = 0f;
        // List removal is deferred to GameManager's end-of-frame compaction so we
        // don't mutate gm.units while CombatSystem is iterating it.
        var gm = GameManager.Instance;
        gm?.selection?.Selected.Remove(this);
        if (gatherTarget != null)
            gatherTarget.currentGatherers = Mathf.Max(0, gatherTarget.currentGatherers - 1);
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
