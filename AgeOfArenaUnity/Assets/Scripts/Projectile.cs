using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Ranged projectile fired by units and buildings. Travels toward a fixed aim-point
/// computed at fire-time; does NOT home on the target.
///
/// N6.ballistics model:
///   Without Ballistics: aim-point = target's position at the moment of firing (snapshot).
///     If the target has moved outside <see cref="HitRadius"/> when the projectile arrives,
///     the shot misses — kiting works.
///   With Ballistics (attacker has researched the tech): aim-point = predicted intercept
///     (snapshot + target velocity × travel-time) — near-perfect lead, kiting is hard.
///
/// N1.pool: instances come from a static <see cref="ObjectPool{T}"/> so hot-path
/// shots produce zero GC alloc (no Instantiate/Destroy per frame).
/// </summary>
public class Projectile : MonoBehaviour
{
    const float Speed      = 22f;
    const float HitOffsetY = 0.8f;
    const float MaxLifetime = 3f;
    const float HitRadius  = 1.5f;

    IDamageable _target;
    float       _damage;
    DamageType  _damageType = DamageType.Pierce;
    float       _splashRadius;
    float       _elevationMult = 1f;   // attacker's elevation context, applied to splash victims
    UnitEntity  _attacker;
    Vector3     _snapPos;
    float       _age;
    Transform   _meshChild;   // pre-built cube child; resized per shot

    static readonly System.Collections.Generic.List<UnitEntity> _splashBuf = new();
    static readonly Color ArrowColor = Prims.Hex(0x4a3018);

    // N1.pool: shared pool — capacity 64, max 256 live projectiles before falling back.
    static ObjectPool<Projectile> _pool;

    static ObjectPool<Projectile> Pool
    {
        get
        {
            if (_pool == null)
                _pool = new ObjectPool<Projectile>(
                    createFunc:      CreatePooled,
                    actionOnGet:     p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => { if (p != null) Destroy(p.gameObject); },
                    collectionCheck: false,
                    defaultCapacity: 64,
                    maxSize:         256
                );
            return _pool;
        }
    }

    static Projectile CreatePooled()
    {
        var go  = new GameObject("Arrow_Pool");
        var mat = Prims.Mat(ArrowColor);
        // Build with standard arrow size; Spawn() will resize on Get.
        var child = Prims.Box(go.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.5f), mat);
        var p     = go.AddComponent<Projectile>();
        p._meshChild = child.transform;
        go.SetActive(false);
        return p;
    }

    /// <summary>Fire a projectile from <paramref name="from"/> at <paramref name="target"/>.</summary>
    public static void Spawn(Vector3 from, IDamageable target, float damage,
                             DamageType damageType = DamageType.Pierce, float splashRadius = 0f,
                             UnitEntity attacker = null, float elevationMult = 1f)
    {
        if (target == null || !target.IsAlive) return;

        var p = Pool.Get();
        p.transform.position = from;

        // Resize mesh child for splash vs arrow.
        if (p._meshChild != null)
        {
            float s = splashRadius > 0f ? 0.35f : 0.06f;
            float z = splashRadius > 0f ? 0.35f : 0.5f;
            p._meshChild.localScale = new Vector3(s, s, z);
        }

        p._target       = target;
        p._damage       = damage;
        p._damageType   = damageType;
        p._splashRadius = splashRadius;
        p._elevationMult = elevationMult;
        p._attacker     = attacker;
        p._age          = 0f;

        // N6.ballistics: compute fixed aim-point at fire-time.
        Vector3 targetBase  = target.Transform.position;
        bool hasBallistics  = attacker?.TeamTech?.Has(TechType.Ballistics) ?? false;
        if (hasBallistics && target is UnitEntity tu)
        {
            float dist      = Vector3.Distance(from, targetBase + Vector3.up * HitOffsetY);
            float travelSec = dist / Speed;
            targetBase += tu.AgentVelocity * travelSec;
        }
        p._snapPos = targetBase + Vector3.up * HitOffsetY;
    }

    void ReturnToPool()
    {
        _target        = null;
        _attacker      = null;
        _elevationMult = 1f;
        Pool.Release(this);
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age > MaxLifetime)
        {
            ReturnToPool();
            return;
        }

        Vector3 d  = _snapPos - transform.position;
        float step = Speed * Time.deltaTime;

        if (d.sqrMagnitude <= step * step)
        {
            bool isSplash  = _splashRadius > 0f;
            bool hitTarget = _target != null && _target.IsAlive;
            if (hitTarget && !isSplash)
            {
                var tcomp = _target as Component;
                float dist2d = tcomp != null
                    ? Vector3.Distance(tcomp.transform.position, _snapPos)
                    : 0f;
                hitTarget = dist2d <= HitRadius;
            }

            if (hitTarget)
            {
                _target.TakeDamage(_damage, _damageType);
                var tgt = _target as Component;
                if (tgt != null)
                    DamagePopup.Show(tgt.transform.position + Vector3.up * 1.5f,
                        Mathf.RoundToInt(_damage), isSplash);
            }

            if (_splashRadius > 0f)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    Vector3 ip = _snapPos;
                    float r2   = _splashRadius * _splashRadius;
                    _splashBuf.Clear();
                    gm.unitGrid.Query(ip, _splashRadius, _splashBuf);
                    for (int i = 0; i < _splashBuf.Count; i++)
                    {
                        var o = _splashBuf[i];
                        if (o == null || !o.IsAlive || ReferenceEquals(o, _target)) continue;
                        Vector3 op = o.transform.position;
                        float dx = op.x - ip.x, dz = op.z - ip.z;
                        float distSq = dx * dx + dz * dz;
                        if (distSq > r2) continue;
                        float frac    = Mathf.Sqrt(distSq) / _splashRadius;
                        float falloff = _attacker != null ? _attacker.SplashFalloffAt(frac) : 1f;
                        // Apply the same elevation context the main target got, so splash
                        // damage isn't silently un-elevation-adjusted relative to the direct hit.
                        float sd      = (_attacker != null
                            ? _attacker.AttackDamage + _attacker.BonusDamageVs(o)
                            : _damage) * falloff * _elevationMult;
                        o.TakeDamage(sd, _damageType);
                        DamagePopup.Show(op + Vector3.up * 1.5f, Mathf.RoundToInt(sd), true);
                    }
                }
            }
            ReturnToPool();
            return;
        }

        transform.position += d.normalized * step;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
    }
}
