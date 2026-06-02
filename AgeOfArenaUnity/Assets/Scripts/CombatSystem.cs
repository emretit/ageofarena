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
            if (u == null || u.isGarrisoned) continue;

            if (u.attackCooldown > 0f) u.attackCooldown -= dt;

            // Cavalry charge timer builds while not actively swinging.
            if (u.type == UnitType.Cavalry && u.state != UnitState.Attacking)
                u.chargeTimer += dt;

            // Medic continuously heals the most-wounded ally in range while idle.
            if (u.type == UnitType.Medic) StepHeal(u, units, dt);
            if (u.type == UnitType.Monk)  StepConvert(u, units, dt);

            if (u.attackTarget != null)
                StepCombat(u);
            else if (u.attackMove)
                StepAttackMove(u, scanAggro);
            else if (scanAggro && u.state == UnitState.Idle && u.gatherTarget == null
                     && u.AggroRadius > 0f && u.stance != AttackStance.NoAttack)
                TryAcquire(u);
        }
    }

    /// <summary>
    /// Attack-move: engage any enemy that enters aggro range (then resume after the
    /// fight via <see cref="StepCombat"/>), otherwise keep advancing to the ordered
    /// destination. Finishes — clearing the flag — once the destination is reached.
    /// </summary>
    void StepAttackMove(UnitEntity u, bool scanAggro)
    {
        if (u.AggroRadius > 0f && scanAggro)
        {
            var enemy = FindNearestEnemy(u, u.AggroRadius);
            if (enemy != null) { u.AttackOrder(enemy); return; }   // attackMove stays set
        }

        if (FlatDist(u.transform.position, u.attackMoveDest) <= 1.2f)
            u.attackMove = false;                                  // arrived
        else if (u.state == UnitState.Idle)
            u.MoveTo(u.attackMoveDest);                            // resume the advance
    }

    void StepCombat(UnitEntity u)
    {
        // Support units (Scout/Medic) never fight: drop any attack order and idle.
        if (u.type == UnitType.Scout || u.type == UnitType.Medic)
        {
            u.attackTarget = null;
            if (u.state == UnitState.MovingToAttack || u.state == UnitState.Attacking) u.Stop();
            return;
        }

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
            // StandGround: don't pursue — drop the target and idle.
            if (u.stance == AttackStance.StandGround)
            {
                u.attackTarget = null;
                _repath.Remove(u);
                if (u.state == UnitState.MovingToAttack) u.Stop();
                return;
            }
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
                float dmg = u.AttackDamage;

                // Spearman anti-cavalry bonus (only vs Cavalry UnitEntity targets).
                if (u.type == UnitType.Spearman &&
                    u.attackTarget is UnitEntity tu && tu.type == UnitType.Cavalry)
                    dmg *= u.AntiCavalryMultiplier;

                // Anti-structure bonus (Trebuchet) when hitting a building.
                if (u.attackTarget is BuildingEntity)
                    dmg *= u.AntiStructureMultiplier;

                if (u.IsRanged)
                {
                    Projectile.Spawn(u.transform.position + Vector3.up * 1.0f, target, dmg, u.DamageKind);
                    AudioManager.Play(AudioManager.SoundId.Arrow, 0.6f);
                }
                else
                {
                    // Cavalry charge: first swing after 4s of non-combat deals 2.5× damage.
                    bool isCharge = u.ChargeReady;
                    if (isCharge) { dmg *= u.ChargeMultiplier; u.chargeTimer = 0f; }
                    target.TakeDamage(dmg, u.DamageKind);
                    AudioManager.Play(AudioManager.SoundId.Sword, isCharge ? 1.0f : 0.7f);
                    var tgt = target as Component;
                    if (tgt != null)
                        DamagePopup.Show(tgt.transform.position + Vector3.up * 1.5f,
                            Mathf.RoundToInt(dmg), isCharge);
                }
                u.attackCooldown = u.AttackInterval;
            }
        }
    }

    void TryAcquire(UnitEntity u)
    {
        var best = FindNearestEnemy(u, u.AggroRadius);
        if (best != null) u.AttackOrder(best);
    }

    /// <summary>Nearest enemy unit or building within <paramref name="radius"/>, or null.</summary>
    IDamageable FindNearestEnemy(UnitEntity u, float radius)
    {
        IDamageable best = null;
        float bestSq = radius * radius;
        Vector3 pos = u.transform.position;

        var units = GM.units;
        for (int i = 0; i < units.Count; i++)
        {
            var o = units[i];
            if (o == null || o.teamId == u.teamId || o.isGarrisoned) continue;
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

        return best;
    }

    /// <summary>
    /// Medic behaviour: while idle, restore hp to the most-wounded friendly unit
    /// within <see cref="UnitEntity.HealRadius"/>. Positional (does not chase) so
    /// the player keeps the medic near the front line. Movement orders take priority.
    /// </summary>
    void StepHeal(UnitEntity medic, List<UnitEntity> units, float dt)
    {
        if (medic.state != UnitState.Idle) return; // don't interrupt a move order

        Vector3 pos = medic.transform.position;
        float radiusSq = medic.HealRadius * medic.HealRadius;
        UnitEntity best = null;
        float bestRatio = 1f;
        for (int i = 0; i < units.Count; i++)
        {
            var o = units[i];
            if (o == null || o == medic || o.teamId != medic.teamId) continue;
            if (o.hp <= 0f || o.hp >= o.maxHp) continue;       // dead or already full
            if (FlatSq(pos, o.transform.position) > radiusSq) continue;
            float r = o.hp / o.maxHp;
            if (r < bestRatio) { bestRatio = r; best = o; }
        }

        if (best != null)
        {
            medic.FaceToward(best.transform.position);
            best.Heal(medic.HealPower * dt);
        }
    }

    /// <summary>
    /// Monk conversion: close in and channel for <see cref="UnitEntity.ConvertTime"/> seconds.
    /// On completion the target unit switches to the Monk's team (new colour).
    /// Cancels if the target dies or moves beyond 2× range.
    /// </summary>
    void StepConvert(UnitEntity monk, List<UnitEntity> units, float dt)
    {
        if (monk.attackTarget == null) return;
        var tgt = monk.attackTarget as UnitEntity;
        if (tgt == null || !tgt.IsAlive || tgt.teamId == monk.teamId)
        {
            monk.attackTarget = null;
            monk.convertProgress = 0f;
            return;
        }

        const float ConvertRange = 2.5f;
        float dist = FlatDist(monk.transform.position, tgt.transform.position);
        if (dist > ConvertRange * 2f) { monk.convertProgress = 0f; return; } // too far

        if (dist > ConvertRange)
        {
            monk.state = UnitState.MovingToAttack;
            _repath.TryGetValue(monk, out float rt);
            rt -= dt;
            if (rt <= 0f)
            {
                monk.NavigateTo(ApproachPoint(tgt.transform.position, monk.transform.position, ConvertRange * 0.7f));
                rt = RepathInterval;
            }
            _repath[monk] = rt;
            return;
        }

        monk.HaltAgent();
        monk.FaceToward(tgt.transform.position);
        monk.state = UnitState.Attacking;
        monk.convertProgress += dt;

        if (monk.convertProgress >= UnitEntity.ConvertTime)
        {
            monk.convertProgress = 0f;
            monk.attackTarget = null;
            // Switch team: update teamId and tint renderers with new team colour.
            var gm = GM;
            int newTeam = monk.teamId;
            Color newColor = gm != null && newTeam < 4
                ? new Color[] { Prims.Hex(0x1e5fcc), Prims.Hex(0xd42020), Prims.Hex(0x1e9e40), Prims.Hex(0xf0a010) }[newTeam]
                : Color.white;
            tgt.teamId = newTeam;
            foreach (var r in tgt.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name == "BlobShadow" || r.gameObject.name.StartsWith("SelectionRing")) continue;
                // tint the primary (first) material with the new team colour
                var mat = r.material;
                mat.color = Color.Lerp(mat.color, newColor, 0.5f);
            }
            monk.Stop();
        }
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
