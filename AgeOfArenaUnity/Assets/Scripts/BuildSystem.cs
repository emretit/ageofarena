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

    readonly Dictionary<UnitEntity, float> _repath = new();

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
        if (site == null || !site.underConstruction)
        {
            // Site finished or destroyed — release the builder.
            u.constructTarget = null;
            _repath.Remove(u);
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

        float time = Mathf.Max(0.1f, site.buildTime);
        site.buildProgress = Mathf.Clamp01(site.buildProgress + dt / time);
        site.hp = Mathf.Max(1f, site.maxHp * site.buildProgress);

        if (site.buildProgress >= 1f)
        {
            site.underConstruction = false;
            site.hp = site.maxHp;

            // A finished Farm becomes a gatherable food field — the slice's only
            // food source. Villagers can then be sent to it like any resource node.
            if (site.type == BuildingType.Farm && site.GetComponent<ResourceNode>() == null)
                GM?.RegisterNode(ResourceFactory.FarmField(site.gameObject));

            GM?.RecomputePop();   // a finished House raises popCap; any building now functional
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
