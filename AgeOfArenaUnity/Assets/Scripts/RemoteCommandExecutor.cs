using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MP-5: Ağ üzerinden gelen GameCommand'leri oyun durumuna uygular.
/// Yerel komutlarla aynı mantığı kullanır; TransportLayer.OnCommandReceived'a bağlıdır.
/// </summary>
public static class RemoteCommandExecutor
{
    /// <summary>Bir uzak GameCommand'i yerel oyun durumuna uygula.</summary>
    public static void Apply(GameCommand cmd)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var units = FindUnits(gm, cmd.unitIds);
        var dest  = new Vector3(cmd.x, 0f, cmd.z);

        switch (cmd.type)
        {
            case CommandType.Move:
            case CommandType.AttackMove: // simplified: same as move for now
                foreach (var u in units)
                    u?.MoveTo(dest);
                break;

            case CommandType.Attack:
                var attackTarget = FindDamageableNear(gm, dest, cmd.intParam1);
                if (attackTarget != null)
                    foreach (var u in units)
                        u?.AttackOrder(attackTarget);
                else
                    foreach (var u in units)
                        u?.MoveTo(dest);
                break;

            case CommandType.Gather:
                var node = FindNodeNear(gm, dest);
                if (node != null)
                    foreach (var u in units)
                        if (u?.type == UnitType.Villager || u?.type == UnitType.FishingShip)
                            gm.gather?.AssignGather(u, node);
                break;

            case CommandType.Build:
                var buildTarget = FindBuildingByHash(gm, cmd.intParam1);
                if (buildTarget != null)
                    foreach (var u in units)
                        if (u?.type == UnitType.Villager)
                            u.BuildOrder(buildTarget);
                break;

            case CommandType.Garrison:
                var garrisonBld = FindBuildingByHash(gm, cmd.intParam1);
                if (garrisonBld != null)
                    foreach (var u in units)
                        u?.GarrisonOrder(garrisonBld);
                break;

            case CommandType.Stop:
                foreach (var u in units)
                    u?.Stop();
                break;

            case CommandType.SetStance:
                var stance = (AttackStance)cmd.intParam1;
                foreach (var u in units)
                    if (u != null) u.stance = stance;
                break;

            case CommandType.SetRally:
                var rallyBld = FindBuildingByHash(gm, cmd.intParam1);
                if (rallyBld != null) rallyBld.rallyPoint = dest;
                break;

            case CommandType.Train:
                var trainBld = FindBuildingByHash(gm, cmd.intParam2);
                if (trainBld != null)
                {
                    var targetType = (UnitType)cmd.intParam1;
                    var trainables = trainBld.GetTrainables();
                    foreach (var t in trainables)
                        if (t.unitType == targetType) { gm.trainingQueue?.Enqueue(trainBld, t); break; }
                }
                break;

            case CommandType.Research:
                var resBld = FindBuildingByHash(gm, cmd.intParam2);
                if (resBld != null)
                {
                    var techType = (TechType)cmd.intParam1;
                    var defs = TechDefs.All();
                    foreach (var def in defs)
                        if (def.type == techType) { gm.research?.Enqueue(resBld, def); break; }
                }
                break;

            case CommandType.Delete:
                foreach (var u in units)
                    if (u != null) Object.Destroy(u.gameObject);
                break;

            case CommandType.Patrol:
                var patrolB = new Vector3(cmd.floatParam1, 0f, cmd.floatParam2);
                // Simplified: move to patrol point A then B alternately — just move to A.
                foreach (var u in units)
                    u?.MoveTo(dest);
                break;

            case CommandType.Ping:
                // Görsel efekt, sim durumu etkilenmez.
                break;
        }

        // Uzak komutları sadece log'a ekle (ağa tekrar gönderme — döngü olur).
        if (gm.cmdRecorder != null)
            gm.cmdRecorder.RecordRemote(cmd);
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    static List<UnitEntity> FindUnits(GameManager gm, int[] ids)
    {
        var result = new List<UnitEntity>();
        if (ids == null) return result;
        foreach (int id in ids)
            for (int i = 0; i < gm.units.Count; i++)
                if (gm.units[i] != null && gm.units[i].unitId == id) { result.Add(gm.units[i]); break; }
        return result;
    }

    static IDamageable FindDamageableNear(GameManager gm, Vector3 pos, int targetId)
    {
        for (int i = 0; i < gm.units.Count; i++)
            if (gm.units[i] != null && gm.units[i].unitId == targetId) return gm.units[i];
        float best = 4f;
        UnitEntity found = null;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null) continue;
            float d = (u.transform.position - pos).sqrMagnitude;
            if (d < best) { best = d; found = u; }
        }
        return found;
    }

    static ResourceNode FindNodeNear(GameManager gm, Vector3 pos)
    {
        var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        float best = 9f;
        ResourceNode found = null;
        foreach (var n in nodes)
        {
            float d = (n.transform.position - pos).sqrMagnitude;
            if (d < best && !n.Depleted) { best = d; found = n; }
        }
        return found;
    }

    static BuildingEntity FindBuildingByHash(GameManager gm, int hash)
    {
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.GetEntityId().GetHashCode() == hash) return b;
        }
        return null;
    }
}
