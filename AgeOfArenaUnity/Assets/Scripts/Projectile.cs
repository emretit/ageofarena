using UnityEngine;

/// <summary>
/// A homing arrow/bolt fired by ranged units. Travels toward its target each
/// frame and applies the carried damage on arrival. Fizzles harmlessly if the
/// target dies (or is destroyed) mid-flight — the <see cref="IDamageable.IsAlive"/>
/// guard handles the Unity fake-null.
///
/// N0.8 honesty note: projectiles are currently perfectly homing (100% hit on a live
/// target), so the University tech "Ballistics" presently has NO gameplay effect — it is a
/// researchable no-op. The real model (pre-Ballistics miss vs moving targets fired at a
/// snapshot point, lead-firing only after Ballistics, so kiting works) is deferred to N6
/// (combat fidelity). Do not advertise Ballistics as accuracy-improving until N6 lands.
/// </summary>
public class Projectile : MonoBehaviour
{
    const float Speed = 22f;       // units/sec
    const float HitOffsetY = 0.8f; // aim at torso height
    const float MaxLifetime = 3f;  // safety despawn

    IDamageable _target;
    float _damage;
    DamageType _damageType = DamageType.Pierce;
    float _splashRadius;   // 0 = single target; >0 = area damage on impact
    UnitEntity _attacker;  // N0.4: source unit, so splash recomputes bonus damage per victim
    float _age;

    static readonly Color ArrowColor = Prims.Hex(0x4a3018);

    /// <summary>Fire an arrow from <paramref name="from"/> at <paramref name="target"/>.
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
        // Siege projectiles (splash) render as a bigger boulder.
        float s = splashRadius > 0f ? 0.35f : 0.06f;
        Prims.Box(go.transform, Vector3.zero, new Vector3(s, s, splashRadius > 0f ? 0.35f : 0.5f), mat);
        var p = go.AddComponent<Projectile>();
        p._target = target;
        p._damage = damage;
        p._damageType = damageType;
        p._splashRadius = splashRadius;
        p._attacker = attacker;
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age > MaxLifetime || _target == null || !_target.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 tp = _target.Transform.position + Vector3.up * HitOffsetY;
        Vector3 d = tp - transform.position;
        float step = Speed * Time.deltaTime;

        if (d.sqrMagnitude <= step * step)
        {
            _target.TakeDamage(_damage, _damageType);
            var tgt = _target as Component;
            if (tgt != null)
                DamagePopup.Show(tgt.transform.position + Vector3.up * 1.5f,
                    Mathf.RoundToInt(_damage), _splashRadius > 0f);

            // N0.4: area splash (Mangonel/Onager/DemoShip) hits EVERY unit in the radius
            // regardless of team — friendly fire and third parties included (AoE2 behaviour).
            // Each secondary takes the attacker's bonus damage vs its own armor class, and its
            // own armor is subtracted inside TakeDamage (no longer the primary's raw damage).
            if (_splashRadius > 0f)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    Vector3 ip = tp;
                    float r2 = _splashRadius * _splashRadius;
                    var units = gm.units;
                    for (int i = 0; i < units.Count; i++)
                    {
                        var o = units[i];
                        if (o == null || !o.IsAlive || ReferenceEquals(o, _target)) continue;
                        Vector3 op = o.transform.position;
                        float dx = op.x - ip.x, dz = op.z - ip.z;
                        if (dx * dx + dz * dz > r2) continue;
                        float sd = _attacker != null
                            ? _attacker.AttackDamage + _attacker.BonusDamageVs(o)
                            : _damage;
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
