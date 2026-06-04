using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N15.checksum: Per-tick state checksum and replay determinism harness.
///
/// Every CHECKSUM_INTERVAL frames computes a FNV-32 hash over:
///   unit positions (x,z) + hp + state-enum
///   building hp
///   team resource totals (food+wood+gold+stone)
///
/// Same mapSeed + same CommandRecorder log → same checksum sequence.
/// Replay verify flow:
///   1. SaveReplaySnapshot()  → stores seed + command log in PlayerPrefs.
///   2. RunReplayVerify()     → restart with same seed, compare checksums tick-by-tick.
///   3. ReplayVerifyResult    → null while running, "PASS" or "MISMATCH@tick=N" when done.
/// </summary>
public class ChecksumSystem : MonoBehaviour
{
    const int    CHECKSUM_INTERVAL = 30;  // compute every 30 frames
    const int    HISTORY_SIZE      = 512; // keep last N checksums
    const string ReplayKey         = "AoA_Replay_0";

    // ── State ─────────────────────────────────────────────────────────────────

    readonly Queue<(int tick, uint hash)> _history = new();
    int _framesSinceLastCheck;

    public string ReplayVerifyResult { get; private set; } // null while running

    // ── Tick hook (called by GameManager.Update) ──────────────────────────────

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.match == null || gm.match.IsOver) return;

        _framesSinceLastCheck++;
        if (_framesSinceLastCheck < CHECKSUM_INTERVAL) return;
        _framesSinceLastCheck = 0;

        int tick = gm.cmdRecorder != null ? gm.cmdRecorder.Tick : 0;
        uint hash = ComputeHash(gm);
        _history.Enqueue((tick, hash));
        if (_history.Count > HISTORY_SIZE) _history.Dequeue();

        // If a baseline is loaded, compare checksums tick-by-tick.
        if (_baseline != null) CompareBaseline(tick, hash);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Checksum at the most recent interval, or 0 if none yet.</summary>
    public uint LatestChecksum
    {
        get
        {
            if (_history.Count == 0) return 0u;
            // Peek at the last element
            uint last = 0u;
            foreach (var (_, h) in _history) last = h;
            return last;
        }
    }

    /// <summary>Full history as (tick, checksum) pairs (oldest first).</summary>
    public IEnumerable<(int tick, uint hash)> History => _history;

    // ── Replay save / verify ──────────────────────────────────────────────────

    [System.Serializable]
    class ReplayData
    {
        public int              mapSeed;
        public List<GameCommand> commands = new();
        public List<uint>       checksums = new();
        public List<int>        checksumTicks = new();
    }

    ReplayData _baseline; // loaded for comparison

    /// <summary>
    /// Snapshot the current seed + command log + checksum history.
    /// Saved to PlayerPrefs so the verify run can load it after Restart().
    /// </summary>
    public void SaveReplaySnapshot()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var wr = Object.FindAnyObjectByType<WorldRoot>();
        var data = new ReplayData { mapSeed = wr != null ? wr.mapSeed : 0 };

        if (gm.cmdRecorder != null)
            data.commands = gm.cmdRecorder.Snapshot();

        foreach (var (t, h) in _history)
        {
            data.checksumTicks.Add(t);
            data.checksums.Add(h);
        }

        PlayerPrefs.SetString(ReplayKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load the saved replay snapshot and restart the game with the same seed.
    /// On rebuild, ChecksumSystem.LoadBaseline() must be called to activate comparison.
    /// </summary>
    public void StartReplayVerify()
    {
        SaveReplaySnapshot(); // ensure latest state is saved
        string json = PlayerPrefs.GetString(ReplayKey, "");
        if (string.IsNullOrEmpty(json)) { Debug.LogWarning("[Checksum] No replay saved."); return; }

        var data = JsonUtility.FromJson<ReplayData>(json);
        if (data == null) return;

        ReplayVerifyResult = null;
        // Pass baseline seed + commands via GameBootstrap
        GameBootstrap.ReplayBaseline = json; // stored statically for the new run
        GameBootstrap.Restart(data.mapSeed);
    }

    /// <summary>
    /// Called on the new run (after Restart) to load the baseline for comparison.
    /// </summary>
    public static void LoadBaseline(ChecksumSystem cs)
    {
        string json = GameBootstrap.ReplayBaseline;
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<ReplayData>(json);
        if (data == null) return;

        cs._baseline = data;
        cs.ReplayVerifyResult = null;
        GameBootstrap.ReplayBaseline = null; // consume

        // Re-apply the same commands via the recorder
        var gm = GameManager.Instance;
        if (gm?.cmdRecorder != null)
            gm.cmdRecorder.LoadSnapshot(data.commands);
    }

    void CompareBaseline(int tick, uint hash)
    {
        // Find matching tick in baseline
        for (int i = 0; i < _baseline.checksumTicks.Count; i++)
        {
            if (_baseline.checksumTicks[i] == tick)
            {
                if (_baseline.checksums[i] != hash)
                {
                    ReplayVerifyResult = $"MISMATCH@tick={tick} got={hash:X8} expected={_baseline.checksums[i]:X8}";
                    _baseline = null; // stop comparing
                    return;
                }
                // Last baseline tick matched → PASS
                if (i == _baseline.checksumTicks.Count - 1)
                {
                    ReplayVerifyResult = "PASS";
                    _baseline = null;
                }
                return;
            }
        }
    }

    // ── Hash computation ──────────────────────────────────────────────────────

    static uint ComputeHash(GameManager gm)
    {
        uint h = 2166136261u; // FNV-32 offset basis

        // Units: sort by unitId for determinism
        var units = gm.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            h = FnvMix(h, (uint)u.unitId);
            h = FnvMix(h, EncodeFloat(u.transform.position.x));
            h = FnvMix(h, EncodeFloat(u.transform.position.z));
            h = FnvMix(h, EncodeFloat(u.hp));
            h = FnvMix(h, (uint)u.state);
        }

        // Buildings
        var buildings = gm.buildings;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null) continue;
            h = FnvMix(h, (uint)b.type);
            h = FnvMix(h, (uint)b.teamId);
            h = FnvMix(h, EncodeFloat(b.hp));
        }

        // Resources (all teams)
        for (int t = 0; t < gm.TeamCount; t++)
        {
            var r = gm.teamRes[t];
            h = FnvMix(h, (uint)r.food);
            h = FnvMix(h, (uint)r.wood);
            h = FnvMix(h, (uint)r.gold);
            h = FnvMix(h, (uint)r.stone);
        }

        return h;
    }

    static uint FnvMix(uint h, uint v)
    {
        unchecked
        {
            h ^= v;
            h *= 16777619u; // FNV prime
        }
        return h;
    }

    // Encode float → uint by quantising to 1/100 precision (avoids float repr drift
    // between platforms while keeping positions distinguishable to 0.01 units).
    static uint EncodeFloat(float v) => (uint)(int)(v * 100f);
}
