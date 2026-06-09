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
                intParam1: (int)def.unitType, intParam2: b.GetEntityId().GetHashCode());
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

    /// <summary>Type of the queued item at <paramref name="index"/> (non-allocating —
    /// the HUD reads count + per-index type + front progress instead of allocating a
    /// List&lt;tuple&gt; every frame). Returns Villager for an out-of-range index.</summary>
    public UnitType GetQueuedType(BuildingEntity b, int index)
    {
        if (!_queues.TryGetValue(b, out var q) || index < 0 || index >= q.Count) return UnitType.Villager;
        return q[index].unitType;
    }

    // ── Save / load ─────────────────────────────────────────────────────────────

    /// <summary>Export every non-empty queue (keyed by building position) for the save file.</summary>
    public void ExportTo(List<SaveSystem.QueueSnap> into)
    {
        if (into == null) return;
        foreach (var kv in _queues)
        {
            var b = kv.Key; var q = kv.Value;
            if (b == null || q.Count == 0) continue;
            var snap = new SaveSystem.QueueSnap {
                x = b.transform.position.x, z = b.transform.position.z,
                frontElapsed = q[0].elapsed,
            };
            for (int i = 0; i < q.Count; i++) snap.types.Add((int)q[i].unitType);
            into.Add(snap);
        }
    }

    /// <summary>Re-create a queue on load WITHOUT deducting resources (the saved ledger already
    /// reflects the spend). Train time is recomputed the same way Enqueue does.</summary>
    public void RestoreQueue(BuildingEntity b, List<int> types, float frontElapsed)
    {
        if (b == null || types == null || types.Count == 0) return;
        var trainables = b.GetTrainables();
        if (!_queues.TryGetValue(b, out var q)) _queues[b] = q = new List<TrainingItem>();
        for (int i = 0; i < types.Count; i++)
        {
            var ut = (UnitType)types[i];
            float baseTime = 10f;
            for (int k = 0; k < trainables.Length; k++)
                if (trainables[k].unitType == ut) { baseTime = trainables[k].trainTime; break; }
            float time = baseTime;
            if (BlacksmithNearby(b, 14f)) time *= 0.80f;
            time *= GM.TeamCivBonus(b.teamId).unitTrainTimeMult;
            q.Add(new TrainingItem {
                unitType = ut, totalTime = Mathf.Max(0.1f, time),
                elapsed  = i == 0 ? frontElapsed : 0f,
            });
        }
    }

    /// <summary>
    /// Cancel the queued item at <paramref name="index"/> and refund its cost.
    /// Queued population is implicit: removing the item lowers ReservedPop().
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
        List<BuildingEntity> dead = null;
        foreach (var kvp in _queues)
        {
            // A producing building destroyed mid-train leaves its queue entry in the
            // dictionary (only gm.buildings is pruned on death). Without this guard,
            // completing an item would call SpawnUnit on a fake-null building and throw
            // MissingReferenceException every frame, aborting the whole training tick.
            // (ResearchSystem.Tick already guards this way; TrainingQueue did not.)
            if (kvp.Key == null) { (dead ??= new()).Add(kvp.Key); continue; }

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
        if (dead != null)
            for (int i = 0; i < dead.Count; i++) _queues.Remove(dead[i]);
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
