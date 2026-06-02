using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Garrison lifecycle: units ordered into a friendly building walk up, enter
/// (hidden but population-preserved since they stay in <c>GameManager.units</c>),
/// heal over time while sheltered, and leave on command. Entry + healing are
/// ticked by <see cref="GameManager"/> after building combat. The extra defensive
/// arrows that garrisoned units provide live in <see cref="BuildingCombatSystem"/>.
/// </summary>
public class GarrisonSystem : MonoBehaviour
{
    const float HealRate = 5f;   // hp/sec restored to each sheltered unit
    const float GateBack = 3.5f; // emerge offset in front of the building (matches TrainingQueue spawn)
    const float EnterPad = 1f;   // extra distance past the footprint that still counts as "arrived"

    public void Tick(List<UnitEntity> units, List<BuildingEntity> buildings, float dt)
    {
        // 1) Entry — a unit walking toward its garrisonTarget enters once it arrives.
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.isGarrisoned || u.garrisonTarget == null) continue;

            var b = u.garrisonTarget;
            if (b == null || !b.IsAlive || !b.HasGarrisonSpace)
            {
                u.garrisonTarget = null;            // building gone or full → drop the order
                continue;
            }

            float reach = b.Radius + EnterPad;
            if (FlatSq(u.transform.position, b.transform.position) > reach * reach) continue;

            b.garrison.Add(u);
            u.EnterGarrison();                      // hides unit; pop preserved (still in gm.units)
        }

        // 2) Heal — sheltered units recover toward maxHp.
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.GarrisonCount == 0) continue;
            for (int g = 0; g < b.garrison.Count; g++)
                b.garrison[g]?.Heal(HealRate * dt);
        }
    }

    /// <summary>Eject every unit sheltered in <paramref name="b"/> to its rally point
    /// (or the gate front), restoring control.</summary>
    public void UngarrisonAll(BuildingEntity b)
    {
        if (b == null) return;
        Vector3 gate = b.transform.position + new Vector3(0f, 0f, -GateBack);
        Vector3 dest = b.hasRally ? b.rallyPoint : gate;
        for (int i = 0; i < b.garrison.Count; i++)
        {
            var u = b.garrison[i];
            if (u == null) continue;
            // Fan emerge points so they don't all stack on the gate.
            Vector3 emerge = gate + new Vector3((i % 4) * 1.1f - 1.65f, 0f, -(i / 4) * 1.1f);
            u.ExitGarrison(emerge, dest);
        }
        b.garrison.Clear();
    }

    /// <summary>The building was destroyed: every sheltered unit dies with it.</summary>
    public void OnBuildingDestroyed(BuildingEntity b)
    {
        if (b == null) return;
        for (int i = 0; i < b.garrison.Count; i++)
            b.garrison[i]?.Kill();
        b.garrison.Clear();
    }

    static float FlatSq(Vector3 a, Vector3 c)
    {
        float dx = a.x - c.x, dz = a.z - c.z;
        return dx * dx + dz * dz;
    }
}
