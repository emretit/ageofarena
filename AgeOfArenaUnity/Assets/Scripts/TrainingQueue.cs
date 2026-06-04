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
        if (rm.pop >= rm.popCap) return false;             // population cap reached
        if (!rm.CanAfford(def.food, def.wood, def.gold, 0)) return false;

        if (!_queues.TryGetValue(b, out var q)) _queues[b] = q = new List<TrainingItem>();
        if (q.Count >= MaxQueueSize) return false;

        rm.Deduct(def.food, def.wood, def.gold, 0);
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
        // Spawn slightly in front of the building (toward the arena's south gate).
        Vector3 spawnPos = b.transform.position + new Vector3(0, 0, -3.5f);

        int tid = b.teamId;
        Color teamColor = TeamPalette.For(tid);
        UnitEntity unit = unitType switch
        {
            UnitType.Villager    => UnitFactory.Villager(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Militia     => UnitFactory.Militia(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Archer      => UnitFactory.Archer(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Cavalry     => UnitFactory.Cavalry(unitsRoot, spawnPos, teamColor),
            UnitType.Trebuchet   => UnitFactory.Trebuchet(unitsRoot, spawnPos, teamColor),
            UnitType.Scout       => UnitFactory.Scout(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Medic       => UnitFactory.Medic(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Spearman    => UnitFactory.Spearman(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Monk        => UnitFactory.Monk(unitsRoot, spawnPos, teamColor, tid),
            UnitType.TradeCart   => UnitFactory.TradeCart(unitsRoot, spawnPos, teamColor),
            UnitType.Longbowman  => UnitFactory.Longbowman(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Skirmisher  => UnitFactory.Skirmisher(unitsRoot, spawnPos, teamColor, tid),
            UnitType.Camel       => UnitFactory.Camel(unitsRoot, spawnPos, teamColor),
            UnitType.Ram         => UnitFactory.Ram(unitsRoot, spawnPos, teamColor),
            UnitType.Mangonel    => UnitFactory.Mangonel(unitsRoot, spawnPos, teamColor),
            UnitType.CavalryArcher => UnitFactory.CavalryArcher(unitsRoot, spawnPos, teamColor),
            UnitType.Galley      => SpawnNaval(b, unitsRoot, teamColor, UnitType.Galley),
            UnitType.FireShip    => SpawnNaval(b, unitsRoot, teamColor, UnitType.FireShip),
            UnitType.DemoShip    => SpawnNaval(b, unitsRoot, teamColor, UnitType.DemoShip),
            UnitType.FishingShip => SpawnNaval(b, unitsRoot, teamColor, UnitType.FishingShip),
            // M9 unique units (Castle) + Eagle (Barracks)
            UnitType.TeutonicKnight => UnitFactory.TeutonicKnight(unitsRoot, spawnPos, teamColor),
            UnitType.WarElephant => UnitFactory.WarElephant(unitsRoot, spawnPos, teamColor),
            UnitType.Mangudai    => UnitFactory.Mangudai(unitsRoot, spawnPos, teamColor),
            UnitType.Samurai     => UnitFactory.Samurai(unitsRoot, spawnPos, teamColor),
            UnitType.ThrowingAxeman => UnitFactory.ThrowingAxeman(unitsRoot, spawnPos, teamColor),
            UnitType.Cataphract  => UnitFactory.Cataphract(unitsRoot, spawnPos, teamColor),
            UnitType.Berserk     => UnitFactory.Berserk(unitsRoot, spawnPos, teamColor),
            UnitType.Mameluke    => UnitFactory.Mameluke(unitsRoot, spawnPos, teamColor),
            UnitType.WoadRaider  => UnitFactory.WoadRaider(unitsRoot, spawnPos, teamColor),
            UnitType.ChuKoNu     => UnitFactory.ChuKoNu(unitsRoot, spawnPos, teamColor),
            UnitType.Huskarl     => UnitFactory.Huskarl(unitsRoot, spawnPos, teamColor),
            UnitType.Janissary   => UnitFactory.Janissary(unitsRoot, spawnPos, teamColor),
            UnitType.Eagle       => UnitFactory.Eagle(unitsRoot, spawnPos, teamColor),
            _                    => UnitFactory.Villager(unitsRoot, spawnPos, teamColor, tid),
        };

        gm.RegisterUnit(unit);
        gm.RecomputePop();
        AudioManager.Play(AudioManager.SoundId.UnitTrained, 0.8f);

        // If the building has a rally point, the fresh unit walks there instead of
        // idling at the gate (AoE behaviour).
        if (b.hasRally && unit != null) unit.MoveTo(b.rallyPoint);
    }

    // Spawn a Galley toward the open sea so it lands on the naval NavMesh. The map is an
    // island ringed by ocean, so "water" is simply outward (away from the map centre).
    static UnitEntity SpawnNaval(BuildingEntity dock, Transform unitsRoot, Color teamColor, UnitType type)
    {
        var wr = Object.FindAnyObjectByType<WorldRoot>();
        int navalId = wr != null ? wr.NavalAgentTypeId : -1;

        // Offset outward from the island centre so the spawn lands in the surrounding sea.
        Vector3 dockPos = dock.transform.position;
        Vector3 dir = dockPos; dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        Vector3 spawnPos = dockPos + dir * 6f;

        return type switch
        {
            UnitType.FireShip    => UnitFactory.FireShip(unitsRoot, spawnPos, teamColor, navalId),
            UnitType.DemoShip    => UnitFactory.DemoShip(unitsRoot, spawnPos, teamColor, navalId),
            UnitType.FishingShip => UnitFactory.FishingShip(unitsRoot, spawnPos, teamColor, navalId),
            _                    => UnitFactory.Galley(unitsRoot, spawnPos, teamColor, navalId),
        };
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
