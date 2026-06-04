using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N1 (ROADMAP-V2): uniform spatial hash over the XZ plane. Replaces the O(n) linear scans in
/// combat aggro / heal / convert / projectile-splash with a cell-neighbourhood query, collapsing
/// the per-frame cost from O(n²) toward O(n). Rebuilt once per frame from the live unit list by
/// <see cref="GameManager.Update"/>; cell lists are reused across frames to keep GC near zero.
/// Buildings are NOT indexed here (only a few dozen exist — a linear scan over them is cheaper
/// than maintaining a second grid).
/// </summary>
public class SpatialGrid
{
    readonly float _cell;
    readonly Dictionary<long, List<UnitEntity>> _cells = new();

    public SpatialGrid(float cellSize = 8f) { _cell = Mathf.Max(1f, cellSize); }

    static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
    int Coord(float v) => Mathf.FloorToInt(v / _cell);

    /// <summary>Re-index all live units. Existing cell lists are cleared and reused.</summary>
    public void Rebuild(List<UnitEntity> units)
    {
        foreach (var kv in _cells) kv.Value.Clear();
        if (units == null) return;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            var p = u.transform.position;
            long k = Key(Coord(p.x), Coord(p.z));
            if (!_cells.TryGetValue(k, out var list)) _cells[k] = list = new List<UnitEntity>(8);
            list.Add(u);
        }
    }

    /// <summary>Append every indexed unit in the cell neighbourhood covering
    /// <paramref name="radius"/> around <paramref name="center"/> into <paramref name="buffer"/>.
    /// The caller still does the exact distance/team/alive filtering — this only prunes the set.</summary>
    public void Query(Vector3 center, float radius, List<UnitEntity> buffer)
    {
        int minX = Coord(center.x - radius), maxX = Coord(center.x + radius);
        int minZ = Coord(center.z - radius), maxZ = Coord(center.z + radius);
        for (int cx = minX; cx <= maxX; cx++)
            for (int cz = minZ; cz <= maxZ; cz++)
                if (_cells.TryGetValue(Key(cx, cz), out var list))
                    buffer.AddRange(list);
    }
}
