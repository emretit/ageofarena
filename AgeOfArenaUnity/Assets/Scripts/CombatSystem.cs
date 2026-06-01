using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives melee combat for every unit: chase the ordered target, stop in range,
/// swing on a cooldown, and kill it at 0 hp. Idle militia auto-acquire the
/// nearest enemy inside their aggro radius. Also draws floating hp bars so the
/// fight reads at a glance (AoE-style). Targets are unified via
/// <see cref="IDamageable"/> so units and buildings are hit the same way.
/// </summary>
public class CombatSystem : MonoBehaviour
{
    const float RepathInterval = 0.25f; // throttle SetDestination while chasing
    const float AggroInterval  = 0.5f;  // throttle idle enemy scans

    readonly Dictionary<UnitEntity, float> _repath = new();
    float _aggroTimer;

    GameManager GM => GameManager.Instance;

    Texture2D _barBg, _barHp, _barEnemy;

    public void Tick(List<UnitEntity> units, float dt)
    {
        bool scanAggro = (_aggroTimer -= dt) <= 0f;
        if (scanAggro) _aggroTimer = AggroInterval;

        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;

            if (u.attackCooldown > 0f) u.attackCooldown -= dt;

            if (u.attackTarget != null)
                StepCombat(u);
            else if (scanAggro && u.state == UnitState.Idle && u.gatherTarget == null && u.AggroRadius > 0f)
                TryAcquire(u);
        }
    }

    void StepCombat(UnitEntity u)
    {
        var target = u.attackTarget;
        if (target == null || !target.IsAlive)
        {
            u.attackTarget = null;
            _repath.Remove(u);
            if (u.state == UnitState.MovingToAttack || u.state == UnitState.Attacking)
                u.Stop();
            return;
        }

        Vector3 pos = u.transform.position;
        Vector3 tpos = target.Transform.position;
        float reach = u.AttackRange + target.Radius;

        if (FlatDist(pos, tpos) > reach + 0.15f)
        {
            u.state = UnitState.MovingToAttack;
            _repath.TryGetValue(u, out float t);
            t -= Time.deltaTime;
            if (t <= 0f)
            {
                u.NavigateTo(ApproachPoint(tpos, pos, reach * 0.85f));
                t = RepathInterval;
            }
            _repath[u] = t;
        }
        else
        {
            u.state = UnitState.Attacking;
            u.HaltAgent();
            u.FaceToward(tpos);
            if (u.attackCooldown <= 0f)
            {
                if (u.IsRanged)
                    Projectile.Spawn(u.transform.position + Vector3.up * 1.0f, target, u.AttackDamage);
                else
                    target.TakeDamage(u.AttackDamage);
                u.attackCooldown = u.AttackInterval;
            }
        }
    }

    void TryAcquire(UnitEntity u)
    {
        IDamageable best = null;
        float bestSq = u.AggroRadius * u.AggroRadius;
        Vector3 pos = u.transform.position;

        var units = GM.units;
        for (int i = 0; i < units.Count; i++)
        {
            var o = units[i];
            if (o == null || o.teamId == u.teamId) continue;
            float sq = FlatSq(pos, o.transform.position);
            if (sq < bestSq) { bestSq = sq; best = o; }
        }

        var buildings = GM.buildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.teamId == u.teamId) continue;
            float sq = FlatSq(pos, b.transform.position);
            if (sq < bestSq) { bestSq = sq; best = b; }
        }

        if (best != null) u.AttackOrder(best);
    }

    static Vector3 ApproachPoint(Vector3 center, Vector3 from, float dist)
    {
        Vector3 dir = from - center;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        return center + dir.normalized * dist;
    }

    // ── HP bars ──────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (GM == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        EnsureBarTextures();

        var units = GM.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.hp >= u.maxHp) continue;
            DrawBar(cam, u.transform.position + Vector3.up * 1.4f,
                u.hp / u.maxHp, u.teamId == 0, 26f);
        }

        var buildings = GM.buildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.maxHp <= 0f || b.hp >= b.maxHp) continue;
            DrawBar(cam, b.transform.position + Vector3.up * 3.4f,
                b.hp / b.maxHp, b.teamId == 0, 46f);
        }
    }

    void DrawBar(Camera cam, Vector3 worldPos, float frac, bool friendly, float width)
    {
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0f) return;
        float x = sp.x - width * 0.5f;
        float y = Screen.height - sp.y;
        const float h = 4f;
        GUI.DrawTexture(new Rect(x - 1, y - 1, width + 2, h + 2), _barBg);
        GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(frac), h),
            friendly ? _barHp : _barEnemy);
    }

    void EnsureBarTextures()
    {
        if (_barBg != null) return;
        _barBg    = Solid(new Color(0f, 0f, 0f, 0.7f));
        _barHp    = Solid(new Color(0.3f, 0.85f, 0.35f, 1f));
        _barEnemy = Solid(new Color(0.85f, 0.25f, 0.2f, 1f));
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    static float FlatDist(Vector3 a, Vector3 b) => Mathf.Sqrt(FlatSq(a, b));

    static float FlatSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
