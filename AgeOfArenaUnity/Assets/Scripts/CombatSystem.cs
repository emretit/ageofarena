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
    readonly List<UnitEntity> _queryBuf = new();   // N1: reused spatial-grid query buffer
    float _aggroTimer;

    GameManager GM => GameManager.Instance;

    // N1.hpbar: IMGUI bar textures removed — world-space WorldHpBar handles rendering.

    // N7.music: track whether any player unit is in combat for music ducking.
    float _combatSignalTimer;

    public void Tick(List<UnitEntity> units, float dt)
    {
        bool scanAggro = (_aggroTimer -= dt) <= 0f;
        if (scanAggro) _aggroTimer = AggroInterval;

        // Signal combat activity every 2s to AudioManager for duck/unduck.
        _combatSignalTimer -= dt;
        if (_combatSignalTimer <= 0f)
        {
            _combatSignalTimer = 2f;
            bool anyCombat = false;
            for (int i = 0; i < units.Count && !anyCombat; i++)
            {
                var u = units[i];
                if (u != null && u.teamId == 0 && u.attackTarget != null) anyCombat = true;
            }
            AudioManager.SetCombatActive(anyCombat);
        }

        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.isGarrisoned) continue;

            if (u.attackCooldown > 0f) u.attackCooldown -= dt;

            // Cavalry charge timer builds while not actively swinging.
            if (u.type == UnitType.Cavalry && u.state != UnitState.Attacking)
                u.chargeTimer += dt;

            // N4/CIVU: Vikings' Berserk slowly regenerates its own HP.
            if (u.SelfRegenPerSecond > 0f && u.hp < u.maxHp)
                u.Heal(u.SelfRegenPerSecond * dt);

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
        // N14/MODES Treaty: attacks are blocked during the peace period.
        if (GM != null && GM.treatyEndTime > 0f && Time.time < GM.treatyEndTime)
        { u.attackTarget = null; return; }

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
            // Siege weapons (min range) can't fire at point-blank targets.
            if (u.attackCooldown <= 0f && FlatDist(pos, tpos) >= u.MinAttackRange)
            {
                u.PlayAttack();   // fire the attack animation (no-op for primitive units)
                // BNUS: AoE2 additive bonus-damage model. dmg = base + class bonus, then the
                // target's armor is subtracted once inside TakeDamage (max 1). Replaces the old
                // multiplicative anti-cavalry/anti-archer/anti-structure factors. Bonus is keyed
                // off the target's ArmorClass flags (ARMC), so it works for units and buildings.
                float dmg = u.AttackDamage + u.BonusDamageVs(target);

                if (u.IsRanged)
                {
                    Projectile.Spawn(u.transform.position + Vector3.up * 1.0f, target, dmg, u.DamageKind, u.SplashRadius, u);
                    AudioManager.Play(AudioManager.SoundId.Arrow, 0.6f);
                }
                else
                {
                    // Cavalry charge: first swing after 4s of non-combat deals 2.5× damage.
                    bool isCharge = u.ChargeReady;
                    if (isCharge) { dmg *= u.ChargeMultiplier; u.chargeTimer = 0f; }
                    // N0.5: the old CBX +25% positional "flank" bonus was removed — AoE2 has no
                    // facing-based damage, and it stacked multiplicatively with the charge bonus,
                    // distorting counter math. (A morale/flank system could return later as an
                    // explicit, documented opt-in, not a silent always-on melee multiplier.)
                    bool wasAlive = target.IsAlive;
                    target.TakeDamage(dmg, u.DamageKind);
                    if (wasAlive && !target.IsAlive) u.AddKill(); // veterancy
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

        // N1: query the spatial grid's cell neighbourhood instead of all units.
        _queryBuf.Clear();
        GM.unitGrid.Query(pos, radius, _queryBuf);
        for (int i = 0; i < _queryBuf.Count; i++)
        {
            var o = _queryBuf[i];
            if (o == null || o.isGarrisoned) continue;
            if (!GM.IsEnemy(u.teamId, o.teamId)) continue;
            float sq = FlatSq(pos, o.transform.position);
            if (sq < bestSq) { bestSq = sq; best = o; }
        }

        // Buildings stay a linear scan — only a few dozen exist, so a second grid isn't worth it.
        var buildings = GM.buildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || !GM.IsEnemy(u.teamId, b.teamId)) continue;
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
        // N1: only consider units in the heal-radius cell neighbourhood.
        _queryBuf.Clear();
        GM.unitGrid.Query(pos, medic.HealRadius, _queryBuf);
        for (int i = 0; i < _queryBuf.Count; i++)
        {
            var o = _queryBuf[i];
            if (o == null || o == medic || o.teamId != medic.teamId) continue;
            if (o.hp <= 0f || o.hp >= o.maxHp) continue;       // dead or already full
            if (FlatSq(pos, o.transform.position) > radiusSq) continue;
            float r = o.hp / o.maxHp;
            if (r < bestRatio) { bestRatio = r; best = o; }
        }

        if (best != null)
        {
            medic.FaceToward(best.transform.position);
            // Byzantines heal +50% (healRateMult); other civs ×1.0.
            float healMult = GameManager.Instance?.TeamCivBonus(medic.teamId).healRateMult ?? 1f;
            best.Heal(medic.HealPower * healMult * dt);
        }
    }

    /// <summary>
    /// Monk conversion: close in and channel for <see cref="UnitEntity.ConvertTime"/> seconds.
    /// On completion the target unit switches to the Monk's team (new colour).
    /// Cancels if the target dies or moves beyond 2× range.
    /// </summary>
    void StepConvert(UnitEntity monk, List<UnitEntity> units, float dt)
    {
        // Recharge faith over time whenever not at full (so a monk can convert again).
        if (monk.faith < UnitEntity.FaithFull)
            monk.faith = Mathf.Min(UnitEntity.FaithFull, monk.faith + UnitEntity.FaithRegenPerSec * dt);

        if (monk.attackTarget == null) return;
        var tgt = monk.attackTarget as UnitEntity;
        if (tgt == null || !tgt.IsAlive || tgt.teamId == monk.teamId)
        {
            monk.attackTarget = null;
            monk.convertProgress = 0f;
            return;
        }

        // Can't begin/continue a conversion without full faith (must recharge first).
        if (!monk.FaithReady) { monk.convertProgress = 0f; return; }

        // MONK: conversion range grows with Block Printing (Monastery tech).
        var mtech = GM != null ? GM.teamTech[monk.teamId] : null;
        float convertRange = mtech?.MonkConvertRange ?? 2.5f;
        float dist = FlatDist(monk.transform.position, tgt.transform.position);
        if (dist > convertRange * 2f) { monk.convertProgress = 0f; return; } // too far

        if (dist > convertRange)
        {
            monk.state = UnitState.MovingToAttack;
            _repath.TryGetValue(monk, out float rt);
            rt -= dt;
            if (rt <= 0f)
            {
                monk.NavigateTo(ApproachPoint(tgt.transform.position, monk.transform.position, convertRange * 0.7f));
                rt = RepathInterval;
            }
            _repath[monk] = rt;
            return;
        }

        monk.HaltAgent();
        monk.FaceToward(tgt.transform.position);
        monk.state = UnitState.Attacking;

        // CONV: roll a probabilistic (variable) conversion time when the channel begins;
        // Theocracy shortens it. The convert completes when progress passes the rolled threshold.
        bool theocracy = mtech?.MonkHasTheocracy ?? false;
        if (monk.convertProgress <= 0f)
            monk.convertThreshold = SimRandom.Range(UnitEntity.ConvertMinTime, UnitEntity.ConvertMaxTime) // N3: sim RNG
                                  * (theocracy ? 0.6f : 1f);
        monk.convertProgress += dt;

        if (monk.convertProgress >= monk.convertThreshold)
        {
            monk.convertProgress = 0f;
            // Theocracy: only partial faith is spent (group convert efficiency) → recharges faster.
            monk.faith = theocracy ? UnitEntity.FaithFull * 0.5f : 0f;
            monk.attackTarget = null;
            // Switch team: update teamId and tint renderers with new team colour.
            var gm = GM;
            int newTeam = monk.teamId;
            Color newColor = gm != null && newTeam >= 0
                ? TeamPalette.For(newTeam)
                : Color.white;
            tgt.teamId = newTeam;
            // Tint primitive units (MeshRenderer)
            foreach (var r in tgt.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name == "BlobShadow" || r.gameObject.name.StartsWith("SelectionRing")) continue;
                r.material.color = Color.Lerp(r.material.color, newColor, 0.5f);
            }
            // Tint KayKit models (SkinnedMeshRenderer) via MaterialPropertyBlock
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", Color.Lerp(Color.white, newColor, 0.28f));
            foreach (var r in tgt.GetComponentsInChildren<SkinnedMeshRenderer>())
                r.SetPropertyBlock(block);
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

    // ── HP bars (N1.hpbar: now world-space billboards via WorldHpBar, not IMGUI) ──────

    void LateUpdate()
    {
        if (GM == null) return;
        var sel = GM.selection;

        var units = GM.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            var bar = u.GetComponent<WorldHpBar>();
            if (bar == null) continue;
            bool selected = sel != null && sel.Selected.Contains(u);
            bool show = u.hp < u.maxHp || selected;
            bar.Refresh(u.hp / u.maxHp, show);
        }

        var buildings = GM.buildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null) continue;
            var bar = b.GetComponent<WorldHpBar>();
            if (bar == null) continue;
            bar.Refresh(b.hp / b.maxHp, b.hp < b.maxHp);
        }
    }

    static float FlatDist(Vector3 a, Vector3 b) => Mathf.Sqrt(FlatSq(a, b));

    static float FlatSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
