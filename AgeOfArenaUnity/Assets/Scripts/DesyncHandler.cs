using UnityEngine;

/// <summary>
/// N17.desync: Desync detection and recovery.
///
/// In multiplayer, each peer sends its per-tick checksum to all others
/// (via TransportLayer). If any peer's checksum diverges, the game halts
/// and a state dump is collected for offline diff.
///
/// Currently wired to ChecksumSystem.LatestChecksum; full cross-peer exchange
/// is the job of TransportLayer (N17.transport).
///
/// Solo-mode replay verify: ChecksumSystem.StartReplayVerify() / LoadBaseline()
/// already provides the PASS/MISMATCH result — this handler extends that to
/// multi-peer scenarios.
/// </summary>
public class DesyncHandler : MonoBehaviour
{
    public bool  DesyncDetected { get; private set; }
    public string DesyncReport  { get; private set; }

    /// <summary>
    /// Called by LockstepSystem or TransportLayer each tick with this peer's
    /// local checksum and the remote peer's checksum for the same tick.
    /// </summary>
    public void CheckTick(int tick, uint localHash, uint remoteHash)
    {
        if (DesyncDetected) return;
        if (localHash == remoteHash) return;

        DesyncDetected = true;
        DesyncReport = $"DESYNC tick={tick} local={localHash:X8} remote={remoteHash:X8}";

        var gm = GameManager.Instance;
        gm?.hud?.ShowSubtitle($"⚠ DESYNC @t={tick} — oyun durduruldu.", 10f);

        // Collect a state dump so offline diff can diagnose root cause.
        CollectStateDump(gm, tick, localHash);

        // Halt simulation.
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Reset — called when starting a new game or replay.
    /// </summary>
    public void Reset()
    {
        DesyncDetected = false;
        DesyncReport   = null;
    }

    void CollectStateDump(GameManager gm, int tick, uint hash)
    {
        if (gm == null) return;
        // Re-use ChecksumSystem.SaveReplaySnapshot to persist a snapshot that
        // includes the command log and checksum history for offline comparison.
        gm.checksum?.SaveReplaySnapshot();
        Debug.LogError($"[DesyncHandler] {DesyncReport}. State dump saved to PlayerPrefs.");
    }
}
