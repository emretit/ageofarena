using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N16.path: Deterministic grid-based A* pathfinder — NavMesh-free alternative
/// for multiplayer determinism. Uses integer arithmetic throughout; no floating-
/// point in the search loop.
///
/// Grid coordinate convention: (col, row) where col = X axis, row = Z axis.
/// World positions are mapped: col = (x + HALF) / CELL, row = (z + HALF) / CELL.
///
/// Usage (SP — units still use NavMesh; this is the MP-mode pathfinder):
///   bool ok = GridPathfinder.Instance.FindPath(startXZ, goalXZ, out var waypoints);
///   // waypoints: List of world XZ Vector2 waypoints from start to goal.
///
/// The grid is built once in WorldRoot after BakeNavMesh; updated when buildings
/// are placed/destroyed.
/// </summary>
public sealed class GridPathfinder
{
    // ── Grid parameters ───────────────────────────────────────────────────────

    public const float CELL     = 1.5f;  // world units per grid cell
    public const float HALF     = 100f;  // half-extent (grid covers -100..+100)
    public const int   SIZE     = (int)((HALF * 2f) / CELL + 1); // ~134 cells/axis

    // Flat bool array: true = passable.
    readonly bool[] _passable = new bool[SIZE * SIZE];

    public static GridPathfinder Instance { get; private set; }

    // ── Initialisation ────────────────────────────────────────────────────────

    public static GridPathfinder Build(float landRadius)
    {
        var g = new GridPathfinder();
        g.InitFromRadius(landRadius);
        Instance = g;
        return g;
    }

    void InitFromRadius(float landRadius)
    {
        float halfCell = CELL * 0.5f;
        for (int r = 0; r < SIZE; r++)
        {
            for (int c = 0; c < SIZE; c++)
            {
                float wx = c * CELL - HALF + halfCell;
                float wz = r * CELL - HALF + halfCell;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);
                _passable[r * SIZE + c] = dist < landRadius - 1f;
            }
        }
    }

    /// <summary>Mark a grid cell blocked (building placed) or passable (removed).</summary>
    public void SetBlocked(Vector3 worldPos, bool blocked)
    {
        if (!WorldToGrid(worldPos, out int c, out int r)) return;
        _passable[r * SIZE + c] = !blocked;
    }

    // ── A* ────────────────────────────────────────────────────────────────────

    // Node pool to avoid per-search allocations.
    readonly int[]  _gCost  = new int[SIZE * SIZE];
    readonly int[]  _parent = new int[SIZE * SIZE];
    readonly bool[] _closed = new bool[SIZE * SIZE];

    // 8-directional neighbours: straight cost 10, diagonal cost 14.
    static readonly (int dc, int dr, int cost)[] Neighbours =
    {
        ( 1,  0, 10), (-1,  0, 10), ( 0,  1, 10), ( 0, -1, 10),
        ( 1,  1, 14), (-1,  1, 14), ( 1, -1, 14), (-1, -1, 14),
    };

    /// <summary>
    /// Find a path from <paramref name="start"/> to <paramref name="goal"/> (world XZ).
    /// Returns true and populates <paramref name="waypoints"/> with world XZ positions.
    /// Uses integer octile-distance heuristic — fully deterministic.
    /// </summary>
    public bool FindPath(Vector2 start, Vector2 goal, out List<Vector2> waypoints)
    {
        waypoints = new List<Vector2>();

        if (!WorldToGrid(new Vector3(start.x, 0, start.y), out int sc, out int sr)) return false;
        if (!WorldToGrid(new Vector3(goal.x,  0, goal.y),  out int gc, out int gr)) return false;

        // Snap goal to nearest passable cell if blocked.
        if (!_passable[gr * SIZE + gc])
        {
            if (!FindNearestPassable(ref gc, ref gr)) return false;
        }

        int startIdx = sr * SIZE + sc;
        int goalIdx  = gr * SIZE + gc;

        if (startIdx == goalIdx) return true; // already there

        // Reset search arrays.
        Array.Clear(_gCost,  0, SIZE * SIZE);
        Array.Clear(_parent, 0, SIZE * SIZE);
        Array.Clear(_closed, 0, SIZE * SIZE);
        for (int i = 0; i < SIZE * SIZE; i++) _gCost[i] = int.MaxValue;
        _gCost[startIdx] = 0;

        // Simple min-heap via sorted list (adequate for RTS unit count).
        var open = new SortedList<long, int>();
        open.Add(MakeKey(Heuristic(sc, sr, gc, gr), startIdx), startIdx);
        _parent[startIdx] = -1;

        while (open.Count > 0)
        {
            int idx = open.Values[0];
            open.RemoveAt(0);

            if (_closed[idx]) continue;
            _closed[idx] = true;

            if (idx == goalIdx) break;

            int row = idx / SIZE;
            int col = idx % SIZE;

            foreach (var (dc, dr, cost) in Neighbours)
            {
                int nc = col + dc, nr = row + dr;
                if (nc < 0 || nc >= SIZE || nr < 0 || nr >= SIZE) continue;
                int nIdx = nr * SIZE + nc;
                if (_closed[nIdx] || !_passable[nIdx]) continue;

                int ng = _gCost[idx] + cost;
                if (ng >= _gCost[nIdx]) continue;

                _gCost[nIdx]  = ng;
                _parent[nIdx] = idx;
                int h = Heuristic(nc, nr, gc, gr);
                // Use unique key: combine f-cost and index to avoid collisions.
                open[MakeKey(ng + h, nIdx)] = nIdx;
            }
        }

        if (!_closed[goalIdx]) return false; // unreachable

        // Reconstruct path (reversed).
        var path = new List<int>();
        int cur = goalIdx;
        while (cur != -1 && cur != startIdx)
        {
            path.Add(cur);
            cur = _parent[cur];
        }
        path.Reverse();

        // Convert grid indices to world XZ waypoints.
        foreach (int i in path)
        {
            int r = i / SIZE, c = i % SIZE;
            waypoints.Add(GridToWorld(c, r));
        }
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static int Heuristic(int ac, int ar, int bc, int br)
    {
        int dx = Math.Abs(ac - bc), dz = Math.Abs(ar - br);
        return 10 * (dx + dz) - 4 * Math.Min(dx, dz); // octile
    }

    static long MakeKey(int fCost, int idx) => ((long)fCost << 20) | (uint)idx;

    bool WorldToGrid(Vector3 w, out int col, out int row)
    {
        col = Mathf.RoundToInt((w.x + HALF) / CELL);
        row = Mathf.RoundToInt((w.z + HALF) / CELL);
        bool valid = col >= 0 && col < SIZE && row >= 0 && row < SIZE;
        if (!valid) { col = row = 0; }
        return valid;
    }

    static Vector2 GridToWorld(int col, int row)
    {
        return new Vector2(col * CELL - HALF + CELL * 0.5f,
                           row * CELL - HALF + CELL * 0.5f);
    }

    bool FindNearestPassable(ref int c, ref int r)
    {
        for (int d = 1; d < 10; d++)
        {
            for (int dc = -d; dc <= d; dc++)
            {
                for (int dr = -d; dr <= d; dr++)
                {
                    int nc = c + dc, nr = r + dr;
                    if (nc < 0 || nc >= SIZE || nr < 0 || nr >= SIZE) continue;
                    if (_passable[nr * SIZE + nc]) { c = nc; r = nr; return true; }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Quick self-test: path from (-50,0) to (50,0) on an open disc.
    /// Returns true if a path was found. Call from CombatMath.SelfTest().
    /// </summary>
    public static bool SelfTest()
    {
        var g = Build(92f);
        bool ok = g.FindPath(new Vector2(-50f, 0f), new Vector2(50f, 0f), out var wp);
        Instance = null; // don't pollute real runtime with test instance
        return ok && wp.Count > 0;
    }
}
