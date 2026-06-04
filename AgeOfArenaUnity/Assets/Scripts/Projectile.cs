using UnityEngine;

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
/// Splash weapons always explode at the aim-point regardless of whether the primary
/// target is still there (area denial stays useful).
/// </summary>
public class Projectile : MonoBehaviour
{
    const float Speed      = 22f;   // units/sec
    const float HitOffsetY = 0.8f;  // aim at torso height
    const float MaxLifetime = 3f;   // safety despawn
    const float HitRadius  = 1.5f;  // distance within which the target counts as "hit" on arrival

    IDamageable _target;
    float       _damage;
    DamageType  _damageType = DamageType.Pierce;
    float       _splashRadius;  // 0 = single; >0 = area on impact
    UnitEntity  _attacker;      // N0.4: source for bonus-damage per-victim in splash
    Vector3     _snapPos;       // fixed aim-point (world-space, with Y offset)
    float       _age;

    static readonly System.Collections.Generic.List<UnitEntity> _splashBuf = new();

    static readonly Color ArrowColor = Prims.Hex(0x4a3018);

    /// <summary>Fire a projectile from <paramref name="from"/> at <paramref name="target"/>.
    /// Aim-point is computed once: lead prediction if attacker has Ballistics, else snapshot.
    /// Pass <paramref name="attacker"/> for splash weapons so each secondary victim is hit
    /// with the attacker's own bonus-damage vs that victim's armor class (N0.4).</summary>
    public static void Spawn(Vector3 from, IDamageable target, float damage,
                             DamageType damageType = DamageType.Pierce, float splashRadius = 0f,
                             UnitEntity attacker = null)
    {
        if (target == null || !target.IsAlive) return;
        var go = new GameObject("Arrow");
        go.transform.position = from;
        var mat = Prims.Mat(ArrowColor);
        float s = splashRadius > 0f ? 0.35f : 0.06f;
        Prims.Box(go.transform, Vector3.zero, new Vector3(s, s, splashRadius > 0f ? 0.35f : 0.5f), mat);
        var p         = go.AddComponent<Projectile>();
        p._target     = target;
        p._damage     = damage;
        p._damageType = damageType;
        p._splashRadius = splashRadius;
        p._attacker   = attacker;

        // N6.ballistics: compute fixed aim-point at fire-time.
        Vector3 targetBase = target.Transform.position;
        bool hasBallistics  = attacker?.TeamTech?.Has(TechType.Ballistics) ?? false;
        if (hasBallistics && target is UnitEntity tu)
        {
            // Lead: predict where target will be when the arrow arrives.
            float dist      = Vector3.Distance(from, targetBase + Vector3.up * HitOffsetY);
            float travelSec = dist / Speed;
            targetBase += tu.AgentVelocity * travelSec;
        }
        p._snapPos = targetBase + Vector3.up * HitOffsetY;
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age > MaxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 d    = _snapPos - transform.position;
        float step   = Speed * Time.deltaTime;

        if (d.sqrMagnitude <= step * step)
        {
            // Arrived at aim-point. For splash weapons always explode; for single-target
            // only damage if the target is still alive and within the hit radius.
            bool isSplash = _splashRadius > 0f;
            bool hitTarget = _target != null && _target.IsAlive;
            if (hitTarget && !isSplash)
            {
                // Kiting check: did the target move away from the aim-point?
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

            // N0.4: area splash (Mangonel/Onager/DemoShip) hits EVERY unit in the radius
            // regardless of team — friendly fire and third parties included (AoE2 behaviour).
            // Each secondary takes the attacker's bonus damage vs its own armor class, and its
            // own armor is subtracted inside TakeDamage (no longer the primary's raw damage).
            if (_splashRadius > 0f)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    Vector3 ip = _snapPos;
                    float r2 = _splashRadius * _splashRadius;
                    // N1: query the spatial grid's neighbourhood instead of all units.
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
                        // N6: blast-damage falls off toward the edge (per attacker's blast profile).
                        float frac = _splashRadius > 0f ? Mathf.Sqrt(distSq) / _splashRadius : 0f;
                        float falloff = _attacker != null ? _attacker.SplashFalloffAt(frac) : 1f;
                        float sd = (_attacker != null
                            ? _attacker.AttackDamage + _attacker.BonusDamageVs(o)
                            : _damage) * falloff;
                        o.TakeDamage(sd, _damageType);
                        DamagePopup.Show(op + Vector3.up * 1.5f, Mathf.RoundToInt(sd), true);
                    }
                }
            }
            Destroy(gameObject);
            return;
        }

        transform.position += d.normalized * step;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
    }
}
