using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives all relics each frame: scans every unit (any team) for proximity to
/// each relic, fills each relic's <see cref="RelicEntity.unitsNearby"/> list, then
/// lets the relic resolve capture + passive income. Any team's units contest a
/// relic just by standing on it — the player and the AI armies both pick relics
/// up opportunistically as they move through the map centre. Ticked from
/// <see cref="GameManager"/>. Also draws a compact "relics held" HUD readout.
/// </summary>
public class RelicSystem : MonoBehaviour
{
    const float CaptureRange   = 3.5f;
    const float CaptureRangeSq = CaptureRange * CaptureRange;

    const float DepositRangeSq = 4f * 4f;  // Monk must reach this close to a Monastery

    public void Tick(List<UnitEntity> units, List<RelicEntity> relics, float dt)
    {
        if (relics.Count == 0) return;
        var gm = GameManager.Instance;

        // 1. Carried relics follow their Monk; drop if the Monk is gone; deposit at a
        //    friendly Monastery (→ permanent control + gold).
        for (int j = 0; j < relics.Count; j++)
        {
            var r = relics[j];
            if (r == null || r.carrier == null) continue;
            var m = r.carrier;
            if (m == null || !m.IsAlive || m.type != UnitType.Monk)
            {
                if (m != null) m.isCarryingRelic = false;
                r.carrier = null;
                continue;
            }
            r.transform.position = m.transform.position;

            if (gm != null)
            {
                for (int b = 0; b < gm.buildings.Count; b++)
                {
                    var bld = gm.buildings[b];
                    if (bld == null || bld.underConstruction
                        || bld.type != BuildingType.Monastery || bld.teamId != m.teamId) continue;
                    float bx = bld.transform.position.x - m.transform.position.x;
                    float bz = bld.transform.position.z - m.transform.position.z;
                    if (bx * bx + bz * bz > DepositRangeSq) continue;
                    r.heldInMonastery = true;
                    r.ForceControl(m.teamId);
                    r.transform.position = bld.transform.position + Vector3.up * 0.5f;
                    m.isCarryingRelic = false;
                    r.carrier = null;
                    break;
                }
            }
        }

        // 2. Proximity scan (only meaningful for available relics).
        for (int j = 0; j < relics.Count; j++)
            if (relics[j] != null) relics[j].unitsNearby.Clear();

        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            Vector3 p = u.transform.position;
            for (int j = 0; j < relics.Count; j++)
            {
                var r = relics[j];
                if (r == null || !r.Available) continue;
                float dx = p.x - r.transform.position.x;
                float dz = p.z - r.transform.position.z;
                if (dx * dx + dz * dz <= CaptureRangeSq) r.unitsNearby.Add(u);
            }
        }

        // 3. Capture (available) / passive gold (held in Monastery).
        for (int j = 0; j < relics.Count; j++)
        {
            var r = relics[j];
            if (r == null) continue;
            if (r.heldInMonastery) r.GrantGold(dt);
            else if (r.Available) r.UpdateCapture(dt);
        }

        // 4. Monk pickup: a Monk standing on an available relic (not already carrying
        //    one) hauls it. Carrying then beats proximity — deliver it to a Monastery.
        for (int j = 0; j < relics.Count; j++)
        {
            var r = relics[j];
            if (r == null || !r.Available) continue;
            for (int k = 0; k < r.unitsNearby.Count; k++)
            {
                var u = r.unitsNearby[k];
                if (u != null && u.type == UnitType.Monk && !u.isCarryingRelic)
                {
                    r.carrier = u;
                    u.isCarryingRelic = true;
                    break;
                }
            }
        }
    }

    /// <summary>How many relics a team currently controls.</summary>
    public int CountControlled(int teamId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return 0;
        int n = 0;
        for (int i = 0; i < gm.relics.Count; i++)
            if (gm.relics[i] != null && gm.relics[i].controllingTeam == teamId) n++;
        return n;
    }
}
