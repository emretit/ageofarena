using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives villagers assigned to a construction site (<see cref="UnitEntity.constructTarget"/>):
/// walk to the site, then raise its <see cref="BuildingEntity.buildProgress"/> while
/// in range. Multiple builders stack (each adds its share per second), so the
/// site finishes in buildTime / builderCount. On completion the building becomes
/// functional (full hp, can train, counts toward popCap). Mirrors the tick shape
/// of <see cref="GatherSystem"/> / <see cref="CombatSystem"/>.
/// </summary>
public class BuildSystem : MonoBehaviour
{
    const float RepathInterval = 0.25f;
    const float BuildReach = 1.4f;   // added to the site footprint radius
    const float RepairCostFactor = 0.5f;  // repair costs half the build price per hp

    readonly Dictionary<UnitEntity, float> _repath = new();
    // Fractional resource debt accrued while repairing a building (keyed per building
    // so cost scales with hp restored, not with the number of builders). [f,w,g,s].
    readonly Dictionary<BuildingEntity, float[]> _repairOwed = new();

    GameManager GM => GameManager.Instance;

    public void Tick(List<UnitEntity> units, float dt)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.constructTarget == null) continue;
            StepBuilder(u, dt);
        }
    }

    void StepBuilder(UnitEntity u, float dt)
    {
        var site = u.constructTarget;
        bool needsBuild  = site != null && site.underConstruction;
        bool needsRepair = site != null && !site.underConstruction && site.hp < site.maxHp;
        if (site == null || (!needsBuild && !needsRepair))
        {
            // Site finished/destroyed, or fully repaired — release the builder.
            u.constructTarget = null;
            _repath.Remove(u);
            if (site != null) _repairOwed.Remove(site);
            if (u.state == UnitState.Moving || u.state == UnitState.Constructing)
                u.Stop();
            return;
        }

        Vector3 pos = u.transform.position;
        Vector3 sp = site.transform.position;
        float reach = site.Radius + BuildReach;

        if (FlatDist(pos, sp) > reach)
        {
            u.state = UnitState.Moving;
            _repath.TryGetValue(u, out float t);
            t -= dt;
            if (t <= 0f)
            {
                u.NavigateTo(ApproachPoint(sp, pos, reach * 0.9f));
                t = RepathInterval;
            }
            _repath[u] = t;
            return;
        }

        // In range: hammer away.
        u.state = UnitState.Constructing;
        u.HaltAgent();
        u.FaceToward(sp);

        if (needsBuild) StepConstruction(site, dt);
        else            StepRepair(site, dt);
    }

    void StepConstruction(BuildingEntity site, float dt)
    {
        float time = Mathf.Max(0.1f, site.buildTime);
        site.buildProgress = Mathf.Clamp01(site.buildProgress + dt / time);
        site.hp = Mathf.Max(1f, site.maxHp * site.buildProgress);

        // Grow the building visually from 0→1 on the Y axis as construction progresses.
        float scaleY = Mathf.Lerp(0.05f, 1f, site.buildProgress);
        site.transform.localScale = new Vector3(1f, scaleY, 1f);

        if (site.buildProgress >= 1f)
        {
            site.underConstruction = false;
            site.hp = site.maxHp;
            site.transform.localScale = Vector3.one;

            // A finished Farm becomes a gatherable food field — the slice's only
            // food source. Villagers can then be sent to it like any resource node.
            if (site.type == BuildingType.Farm && site.GetComponent<ResourceNode>() == null)
                GM?.RegisterNode(ResourceFactory.FarmField(site.gameObject));

            GM?.RecomputePop();   // a finished House raises popCap; any building now functional
            if (site.teamId == 0)
                AudioManager.Play(AudioManager.SoundId.BuildComplete, 0.9f);
        }
    }

    /// <summary>
    /// Restore hp to a finished but damaged building. The player (team 0) pays
    /// resources proportional to the fraction repaired (half the build price);
    /// if a payment can't be afforded the repair stalls until resources return.
    /// </summary>
    void StepRepair(BuildingEntity site, float dt)
    {
        float rate = site.maxHp / Mathf.Max(0.1f, site.buildTime);  // hp/sec, per builder
        float heal = rate * dt;
        float frac = heal / Mathf.Max(1f, site.maxHp);

        if (site.teamId == 0)
        {
            var def = BuildingDefs.Get(site.type);
            if (!_repairOwed.TryGetValue(site, out var owed)) _repairOwed[site] = owed = new float[4];
            owed[0] += def.food  * frac * RepairCostFactor;
            owed[1] += def.wood  * frac * RepairCostFactor;
            owed[2] += def.gold  * frac * RepairCostFactor;
            owed[3] += def.stone * frac * RepairCostFactor;

            int pf = (int)owed[0], pw = (int)owed[1], pg = (int)owed[2], ps = (int)owed[3];
            if (pf + pw + pg + ps > 0)
            {
                var rm = GM.resources;
                if (!rm.CanAfford(pf, pw, pg, ps)) return;  // stall: keep state, heal nothing
                rm.Deduct(pf, pw, pg, ps);
                owed[0] -= pf; owed[1] -= pw; owed[2] -= pg; owed[3] -= ps;
            }
        }

        site.hp = Mathf.Min(site.maxHp, site.hp + heal);
        if (site.hp >= site.maxHp)
        {
            _repairOwed.Remove(site);
            if (site.teamId == 0) AudioManager.Play(AudioManager.SoundId.Repair, 0.7f); // N7.sfx
        }
    }

    static Vector3 ApproachPoint(Vector3 center, Vector3 from, float dist)
    {
        Vector3 dir = from - center;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        return center + dir.normalized * dist;
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
