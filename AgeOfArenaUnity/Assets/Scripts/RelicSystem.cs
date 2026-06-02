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

    public void Tick(List<UnitEntity> units, List<RelicEntity> relics, float dt)
    {
        if (relics.Count == 0) return;

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
                if (r == null) continue;
                float dx = p.x - r.transform.position.x;
                float dz = p.z - r.transform.position.z;
                if (dx * dx + dz * dz <= CaptureRangeSq) r.unitsNearby.Add(u);
            }
        }

        for (int j = 0; j < relics.Count; j++)
            if (relics[j] != null) relics[j].UpdateCapture(dt);
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
