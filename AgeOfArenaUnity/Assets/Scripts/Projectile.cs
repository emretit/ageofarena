using UnityEngine;

/// <summary>
/// A homing arrow/bolt fired by ranged units. Travels toward its target each
/// frame and applies the carried damage on arrival. Fizzles harmlessly if the
/// target dies (or is destroyed) mid-flight — the <see cref="IDamageable.IsAlive"/>
/// guard handles the Unity fake-null.
/// </summary>
public class Projectile : MonoBehaviour
{
    const float Speed = 22f;       // units/sec
    const float HitOffsetY = 0.8f; // aim at torso height
    const float MaxLifetime = 3f;  // safety despawn

    IDamageable _target;
    float _damage;
    DamageType _damageType = DamageType.Pierce;
    float _age;

    static readonly Color ArrowColor = Prims.Hex(0x4a3018);

    /// <summary>Fire an arrow from <paramref name="from"/> at <paramref name="target"/>.</summary>
    public static void Spawn(Vector3 from, IDamageable target, float damage,
                             DamageType damageType = DamageType.Pierce)
    {
        if (target == null || !target.IsAlive) return;
        var go = new GameObject("Arrow");
        go.transform.position = from;
        var mat = Prims.Mat(ArrowColor);
        Prims.Box(go.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.5f), mat);
        var p = go.AddComponent<Projectile>();
        p._target = target;
        p._damage = damage;
        p._damageType = damageType;
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
                    Mathf.RoundToInt(_damage), false);
            Destroy(gameObject);
            return;
        }

        transform.position += d.normalized * step;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
    }
}
