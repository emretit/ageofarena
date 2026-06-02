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

    /// <summary>
    /// Attempt to enqueue a unit. Returns false if resources are insufficient or
    /// the queue is full.
    /// </summary>
    public bool Enqueue(BuildingEntity b, UnitTrainable def)
    {
        var rm = GM.resources;
        if (rm.pop >= rm.popCap) return false;             // population cap reached
        if (!rm.CanAfford(def.food, def.wood, def.gold, 0)) return false;

        if (!_queues.TryGetValue(b, out var q)) _queues[b] = q = new List<TrainingItem>();
        if (q.Count >= MaxQueueSize) return false;

        rm.Deduct(def.food, def.wood, def.gold, 0);
        q.Add(new TrainingItem
        {
            unitType  = def.unitType,
            totalTime = def.trainTime,
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
        var rm = GM.resources;
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
        // Spawn slightly in front of the building (toward the arena's south gate).
        Vector3 spawnPos = b.transform.position + new Vector3(0, 0, -3.5f);

        var teamColor = Prims.Hex(0x2a5db0);
        UnitEntity unit = unitType switch
        {
            UnitType.Villager   => UnitFactory.Villager(unitsRoot, spawnPos, teamColor),
            UnitType.Militia    => UnitFactory.Militia(unitsRoot, spawnPos, teamColor),
            UnitType.Archer     => UnitFactory.Archer(unitsRoot, spawnPos, teamColor),
            UnitType.Cavalry    => UnitFactory.Cavalry(unitsRoot, spawnPos, teamColor),
            UnitType.Trebuchet  => UnitFactory.Trebuchet(unitsRoot, spawnPos, teamColor),
            UnitType.Scout      => UnitFactory.Scout(unitsRoot, spawnPos, teamColor),
            UnitType.Medic      => UnitFactory.Medic(unitsRoot, spawnPos, teamColor),
            UnitType.Spearman   => UnitFactory.Spearman(unitsRoot, spawnPos, teamColor),
            UnitType.Monk       => UnitFactory.Monk(unitsRoot, spawnPos, teamColor),
            _                   => UnitFactory.Villager(unitsRoot, spawnPos, teamColor),
        };

        gm.RegisterUnit(unit);
        gm.RecomputePop();
        AudioManager.Play(AudioManager.SoundId.UnitTrained, 0.8f);

        // If the building has a rally point, the fresh unit walks there instead of
        // idling at the gate (AoE behaviour).
        if (b.hasRally && unit != null) unit.MoveTo(b.rallyPoint);
    }
}
