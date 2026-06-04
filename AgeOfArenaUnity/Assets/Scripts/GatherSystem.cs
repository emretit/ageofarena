using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the villager gather loop: walk to a node, harvest in fixed ticks,
/// carry back to the nearest dropoff, deposit into the <see cref="ResourceManager"/>,
/// then repeat or pick the next node of the same kind.
/// Movement is delegated to <see cref="UnitEntity"/>'s NavMeshAgent.
/// </summary>
public class GatherSystem : MonoBehaviour
{
    const int CarryCapacity = 10;
    const float DropoffRange = 2.2f;

    readonly Dictionary<UnitEntity, float> _timers = new();

    GameManager GM => GameManager.Instance;

    static float GatherRangeFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Wood  => 1.8f,
        ResourceKind.Gold  => 2.2f,
        ResourceKind.Stone => 2.2f,
        _                  => 1.4f,
    };

    /// <summary>Resources harvested per gather tick (GRATE). Flat 1 for now; the
    /// per-kind speed difference is carried by <see cref="GatherIntervalFor"/>.</summary>
    static int GatherRateFor(ResourceKind kind) => 1;

    /// <summary>Seconds between gather ticks by resource kind (GRATE): Food fastest,
    /// then Gold/Stone, Wood slowest → effective rate Wood &lt; Gold/Stone &lt; Food.</summary>
    static float GatherIntervalFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Food  => 0.5f,
        ResourceKind.Gold  => 0.6f,
        ResourceKind.Stone => 0.6f,
        ResourceKind.Wood  => 0.7f,
        _                  => 0.6f,
    };

    /// <summary>Per-trip carry capacity (RPCT): base × Wheelbarrow multiplier + flat bonus.</summary>
    int CarryCapacityFor(UnitEntity v)
    {
        var tech = GM != null ? GM.teamTech[v.teamId] : null;
        if (tech == null) return CarryCapacity;
        return Mathf.RoundToInt(CarryCapacity * tech.CarryCapacityMult) + tech.CarryBonus;
    }

    public void AssignGather(UnitEntity v, ResourceNode node)
    {
        if (v == null || node == null || node.Depleted) return;

        if (v.gatherTarget != null && v.gatherTarget != node)
            v.gatherTarget.currentGatherers = Mathf.Max(0, v.gatherTarget.currentGatherers - 1);

        node.currentGatherers++;
        v.gatherTarget = node;
        v.carrying.kind = node.kind;
        _timers[v] = 0f;
        v.MoveTo(ApproachPoint(node, v.transform.position, GatherRangeFor(node.kind) * 0.7f));
    }

    public void Tick(List<UnitEntity> units, float dt)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var v = units[i];
            if (v == null || v.gatherTarget == null) continue;
            StepUnit(v, dt);
        }
    }

    void StepUnit(UnitEntity v, float dt)
    {
        var node = v.gatherTarget;
        float range = GatherRangeFor(node.kind);
        Vector3 pos = v.transform.position;

        switch (v.state)
        {
            case UnitState.Moving:
            {
                // NavMeshAgent navigates automatically; just wait for proximity.
                if (FlatDist(pos, node.transform.position) <= range)
                {
                    v.state = UnitState.Gathering;
                    _timers[v] = 0f;
                }
                break;
            }

            case UnitState.Gathering:
            {
                if (node.Depleted) { BeginReturn(v); break; }

                _timers.TryGetValue(v, out float timer);
                timer += dt;
                float interval = GatherIntervalFor(node.kind);
                if (timer >= interval)
                {
                    timer -= interval;
                    v.carrying.amount += node.Take(GatherRateFor(node.kind));
                    if (v.carrying.amount >= CarryCapacityFor(v) || node.Depleted)
                        BeginReturn(v);
                }
                _timers[v] = timer;
                break;
            }

            case UnitState.ReturningToDropoff:
            {
                // NavMeshAgent is already navigating to dropoff (set in BeginReturn).
                var camp = NearestDropoff(pos, v.carrying.kind, v.teamId);
                if (camp == null) { v.Stop(); break; }   // no valid drop-off left
                float reach = camp.Radius + DropoffRange;
                if (FlatDist(pos, camp.transform.position) <= reach)
                {
                    if (v.carrying.amount > 0)
                    {
                        // Researched gather upgrades (DoubleBitAxe, Wheelbarrow) scale the deposit;
                        // civilization bonus stacks on top (Franks: +20% food, Britons: +15% wood, etc.).
                        float mult = GM.teamTech[v.teamId].GatherMult(v.carrying.kind);
                        mult *= CivGatherMult(v.teamId, v.carrying.kind);
                        // CIVM: team (shared) food bonus stacks on food deposits.
                        if (v.carrying.kind == ResourceKind.Food)
                            mult *= 1f + GM.TeamSharedBonus(v.teamId).gatherFoodBonus;
                        // AICH: difficulty eco multiplier (AI teams only; player = 1×).
                        if (v.teamId > 0 && v.teamId < GM.teamEcoMult.Length)
                            mult *= GM.teamEcoMult[v.teamId];
                        // N14/MODES Turbo: all-team gather yield boost.
                        mult *= GM.turboGatherMult;
                        int gained = Mathf.RoundToInt(v.carrying.amount * mult);
                        GM.teamRes[v.teamId].Gain(v.carrying.kind, gained);
                        // N7.sfx: gather sound (player team only to avoid SFX spam from AI villagers).
                        if (v.teamId == 0) AudioManager.Play(AudioManager.SoundId.Gather, 0.35f);
                    }
                    v.carrying.Clear();

                    if (!node.Depleted && node.HasRoom)
                    {
                        _timers[v] = 0f;
                        v.MoveTo(ApproachPoint(node, pos, range * 0.7f));
                    }
                    else
                    {
                        ReleaseNode(v);
                        var next = FindNearestNode(pos, node.kind);
                        if (next != null) AssignGather(v, next);
                        else v.Stop();
                    }
                }
                break;
            }
        }
    }

    void BeginReturn(UnitEntity v)
    {
        var camp = NearestDropoff(v.transform.position, v.carrying.kind, v.teamId);
        if (camp == null) { v.Stop(); return; }
        var dest = ApproachPoint(null, v.transform.position,
            camp.Radius + DropoffRange * 0.6f, camp.transform.position);
        v.state = UnitState.ReturningToDropoff;
        v.NavigateTo(dest);
    }

    void ReleaseNode(UnitEntity v)
    {
        if (v.gatherTarget != null)
            v.gatherTarget.currentGatherers = Mathf.Max(0, v.gatherTarget.currentGatherers - 1);
        v.gatherTarget = null;
        _timers.Remove(v);
    }

    static Vector3 ApproachPoint(ResourceNode node, Vector3 from, float dist, Vector3? overrideCenter = null)
    {
        Vector3 center = overrideCenter ?? (node != null ? node.transform.position : from);
        Vector3 dir = from - center;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        return center + dir.normalized * dist;
    }

    /// <summary>
    /// Nearest completed, same-team building that accepts <paramref name="kind"/> as a
    /// drop-off (Town Center accepts all; Lumber/Mining camps and Mill are typed).
    /// Returns null if none exists.
    /// </summary>
    BuildingEntity NearestDropoff(Vector3 pos, ResourceKind kind, int teamId)
    {
        var list = GM.buildings;
        BuildingEntity best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b == null || b.underConstruction || b.teamId != teamId) continue;
            if (!BuildingDefs.AcceptsDropoff(b.type, kind)) continue;
            float sq = FlatSq(pos, b.transform.position);
            if (sq < bestSq) { bestSq = sq; best = b; }
        }
        return best;
    }

    ResourceNode FindNearestNode(Vector3 pos, ResourceKind kind)
    {
        ResourceNode best = null;
        float bestSq = float.MaxValue;
        var nodes = GM.nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null || n.Depleted || n.kind != kind || !n.HasRoom) continue;
            float sq = FlatSq(pos, n.transform.position);
            if (sq < bestSq) { bestSq = sq; best = n; }
        }
        return best;
    }

    /// <summary>Returns the civilization gather multiplier for the given team and resource kind.</summary>
    static float CivGatherMult(int teamId, ResourceKind kind)
    {
        var gm = GameManager.Instance;
        if (gm == null) return 1f;
        var b = gm.TeamCivBonus(teamId);
        return kind switch
        {
            ResourceKind.Food  => b.gatherFoodMult,
            ResourceKind.Wood  => b.gatherWoodMult,
            ResourceKind.Gold  => b.gatherGoldMult,
            _                  => 1f,
        };
    }

    static float FlatDist(Vector3 a, Vector3 b) => Mathf.Sqrt(FlatSq(a, b));

    static float FlatSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
