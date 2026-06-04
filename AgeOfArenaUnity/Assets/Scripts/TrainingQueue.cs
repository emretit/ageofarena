using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-building unit training queue. Deducts resources on enqueue, advances a
/// timer each frame, and spawns the unit at the building's gate when done.
/// Port of the Three.js TrainingQueue.ts, adapted for the Unity scene.
/// </summary>
public class TrainingQueue : MonoBehaviour
{
    const int MaxQueueSize = 5;

    class TrainingItem
    {
        public UnitType unitType;
        public float totalTime;
        public float elapsed;
        public int food, wood, gold;
    }

    readonly Dictionary<BuildingEntity, List<TrainingItem>> _queues = new();

    GameManager GM => GameManager.Instance;

    // N0.9: the resource/pop ledger of the building's OWNER, not always team 0. Previously the
    // queue spent/refunded team 0's resources regardless of who owned the building — a latent
    // multi-team bug (masked only because the AI bypasses the queue). N5 adds per-team pop cap.
    ResourceManager Res(BuildingEntity b)
        => GM.teamRes != null && b != null && b.teamId >= 0 && b.teamId < GM.teamRes.Length
            ? GM.teamRes[b.teamId] : GM.resources;

    /// <summary>
    /// Attempt to enqueue a unit. Returns false if resources are insufficient or
    /// the queue is full.
    /// </summary>
    public bool Enqueue(BuildingEntity b, UnitTrainable def)
    {
        var rm = Res(b);                                   // N0.9: owner's ledger
        // N0.7: this civ may be denied the unit (tech-tree subtraction, e.g. Aztecs no cavalry).
        if (CivilizationDefs.IsUnitDenied(GM.teamCivs[b.teamId], def.unitType)) return false;
        // Population cap: count already-queued (reserved) units too, so a full queue
        // can't overshoot popCap (e.g. queueing 5 at pop 4/5 → 9/5). Each queued unit
        // reserves 1 pop. ReservedPop() skips destroyed buildings, so it never leaks.
        if (rm.pop + ReservedPop(b.teamId) >= rm.popCap) return false;
        if (!rm.CanAfford(def.food, def.wood, def.gold, 0)) return false;

        if (!_queues.TryGetValue(b, out var q)) _queues[b] = q = new List<TrainingItem>();
        if (q.Count >= MaxQueueSize) return false;

        rm.Deduct(def.food, def.wood, def.gold, 0);
        // N3.cmdlog: record train command (player team only)
        if (b.teamId == 0)
            GM.cmdRecorder?.Record(CommandType.Train, null,
                intParam1: (int)def.unitType, intParam2: b.GetInstanceID());
        // Blacksmith aura: 20% faster training for military buildings within 14u.
        float time = def.trainTime;
        if (BlacksmithNearby(b, 14f)) time *= 0.80f;
        time *= GM.TeamCivBonus(b.teamId).unitTrainTimeMult;   // CIVD: Mongols/Aztecs train faster
        q.Add(new TrainingItem
        {
            unitType  = def.unitType,
            totalTime = time,
            food = def.food, wood = def.wood, gold = def.gold,
        });
        return true;
    }

    /// <returns>0–1 progress of the current item, or -1 if queue is empty.</returns>
    public float GetProgress(BuildingEntity b)
    {
        if (!_queues.TryGetValue(b, out var q) || q.Count == 0) return -1f;
        var it = q[0];
        return it.elapsed / it.totalTime;
    }

    public int GetQueueCount(BuildingEntity b)
        => _queues.TryGetValue(b, out var q) ? q.Count : 0;

    /// <summary>Total queued (reserved) population across a team's buildings. Each
    /// queued unit reserves 1 pop. Destroyed buildings (null key) are skipped, so the
    /// reservation self-corrects and never leaks.</summary>
    int ReservedPop(int teamId)
    {
        int n = 0;
        foreach (var kvp in _queues)
        {
            var b = kvp.Key;
            if (b == null || b.teamId != teamId) continue;
            n += kvp.Value.Count;
        }
        return n;
    }

    /// <summary>
    /// Read-only snapshot of a building's queue for the HUD queue strip: the
    /// front item carries its live 0–1 progress, the rest report 0.
    /// </summary>
    public List<(UnitType type, float progress)> GetQueueView(BuildingEntity b)
    {
        var view = new List<(UnitType, float)>();
        if (!_queues.TryGetValue(b, out var q)) return view;
        for (int i = 0; i < q.Count; i++)
        {
            var it = q[i];
            view.Add((it.unitType, i == 0 ? it.elapsed / it.totalTime : 0f));
        }
        return view;
    }

    /// <summary>
    /// Cancel the queued item at <paramref name="index"/> and refund its cost.
    /// Population isn't refunded because enqueue doesn't reserve it.
    /// </summary>
    public void Cancel(BuildingEntity b, int index)
    {
        if (!_queues.TryGetValue(b, out var q) || index < 0 || index >= q.Count) return;
        var it = q[index];
        var rm = Res(b);                                   // N0.9: refund to the owner
        rm.Gain(ResourceKind.Food, it.food);
        rm.Gain(ResourceKind.Wood, it.wood);
        rm.Gain(ResourceKind.Gold, it.gold);
        q.RemoveAt(index);
    }

    public void Tick(float dt)
    {
        var gm = GM;
        foreach (var kvp in _queues)
        {
            var q = kvp.Value;
            if (q.Count == 0) continue;

            var item = q[0];
            item.elapsed += dt;
            if (item.elapsed >= item.totalTime)
            {
                q.RemoveAt(0);
                SpawnUnit(kvp.Key, item.unitType, gm);
            }
        }
    }

    void SpawnUnit(BuildingEntity b, UnitType unitType, GameManager gm)
    {
        var unitsRoot = GameObject.Find("Units")?.transform ?? b.transform.parent;
        int tid = b.teamId;

        // Ships spawn outward toward the open sea (the map is an island ringed by ocean)
        // so they land on the naval NavMesh; everyone else spawns at the building's gate.
        bool naval = unitType is UnitType.Galley or UnitType.FireShip
                              or UnitType.DemoShip or UnitType.FishingShip;
        int navalId = -1;
        Vector3 spawnPos;
        if (naval)
        {
            var wr = Object.FindAnyObjectByType<WorldRoot>();
            navalId = wr != null ? wr.NavalAgentTypeId : -1;
            Vector3 dockPos = b.transform.position;
            Vector3 dir = dockPos; dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            spawnPos = dockPos + dir * 6f;
        }
        else
        {
            spawnPos = b.transform.position + new Vector3(0, 0, -3.5f);
        }

        // Central dispatch — sets teamId correctly for every type (fixes AI Cavalry
        // joining team 0 via the no-teamId factory methods).
        UnitEntity unit = UnitFactory.Spawn(unitType, unitsRoot, spawnPos, tid, navalId);

        gm.RegisterUnit(unit);
        gm.RecomputePop();
        AudioManager.Play(AudioManager.SoundId.UnitTrained, 0.8f);

        // If the building has a rally point, the fresh unit walks there instead of
        // idling at the gate (AoE behaviour).
        if (b.hasRally && unit != null) unit.MoveTo(b.rallyPoint);
    }

    static bool BlacksmithNearby(BuildingEntity b, float radius)
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        float r2 = radius * radius;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var s = gm.buildings[i];
            if (s == null || s.teamId != b.teamId || s.type != BuildingType.Blacksmith) continue;
            Vector3 d = s.transform.position - b.transform.position; d.y = 0;
            if (d.sqrMagnitude <= r2) return true;
        }
        return false;
    }
}
