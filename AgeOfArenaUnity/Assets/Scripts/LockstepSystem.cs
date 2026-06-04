using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⚠️ NOT WIRED INTO THE LIVE SIM (2026-06 audit). StartLockstep() has no caller, so IsActive
/// stays false and OnSimTick() returns immediately. This is MP scaffolding for the future MP2
/// work; the shipping game is single-player on NavMesh + Time.deltaTime. See NetworkMode.cs.
///
/// N16.lockstep: Local in-process 2-player lockstep harness.
///
/// Lockstep model:
///   - Sim advances in fixed steps (FIXED_DT via GameManager.FixedStepEnabled).
///   - Each "player" (P0 = human, P1 = second local player or AI mirror) issues
///     commands at frame F; those commands execute at frame F + INPUT_DELAY.
///   - No tick advances until ALL players have submitted their command batch for
///     that tick — the "lock" in lockstep.
///   - Every tick: ChecksumSystem computes a hash. If P0 and P1 hashes diverge
///     → DesyncHandler fires.
///
/// Current scope: LOCAL 2-player (both on same machine). Network transport lives
/// in TransportLayer (N17.transport); plugging in the transport replaces the
/// in-process queue with a socket read.
///
/// Integration:
///   • GameManager.FixedStepEnabled = true when lockstep is active.
///   • Commands issued via CommandRecorder are forwarded here (EnqueueLocal).
///   • EnemyAI commands (team 1) are forwarded via EnqueueRemote / EnqueueAI.
///   • LockstepSystem.Tick() is called at the end of each fixed sim step.
/// </summary>
public class LockstepSystem : MonoBehaviour
{
    public const int INPUT_DELAY = 2; // frames between issue and execution

    // Per-player command queues indexed by [playerId][tick]
    readonly Dictionary<int, Queue<GameCommand>> _p0Queue = new();
    readonly Dictionary<int, Queue<GameCommand>> _p1Queue = new();

    int  _simTick;
    bool _waitingForP1;

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Number of local players (1 = solo + AI mirror, 2 = split-screen).</summary>
    public int PlayerCount = 1;

    public bool IsActive { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void StartLockstep()
    {
        IsActive = true;
        _simTick = 0;
        _p0Queue.Clear();
        _p1Queue.Clear();
        var gm = GameManager.Instance;
        if (gm != null) gm.FixedStepEnabled = true;
    }

    public void StopLockstep()
    {
        IsActive = false;
        var gm = GameManager.Instance;
        if (gm != null) gm.FixedStepEnabled = false;
    }

    // ── Command ingestion ─────────────────────────────────────────────────────

    /// <summary>Enqueue a local (P0) command; it will execute at tick+INPUT_DELAY.</summary>
    public void EnqueueLocal(GameCommand cmd)
    {
        int execTick = _simTick + INPUT_DELAY;
        cmd.tick = execTick;
        if (!_p0Queue.TryGetValue(execTick, out var q)) _p0Queue[execTick] = q = new Queue<GameCommand>();
        q.Enqueue(cmd);
    }

    /// <summary>Enqueue a remote / AI-mirror (P1) command for a specific exec tick.</summary>
    public void EnqueueRemote(GameCommand cmd)
    {
        if (!_p1Queue.TryGetValue(cmd.tick, out var q)) _p1Queue[cmd.tick] = q = new Queue<GameCommand>();
        q.Enqueue(cmd);
    }

    /// <summary>
    /// Mirror P0's command to P1 (AI mirror mode: AI plays same commands as player,
    /// offset by INPUT_DELAY, to verify determinism on the same machine).
    /// </summary>
    public void MirrorToP1(GameCommand cmd)
    {
        var mirrored = cmd;
        mirrored.playerId = 1;
        EnqueueRemote(mirrored);
    }

    // ── Tick ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by GameManager after each fixed sim step.
    /// Drains commands for the current tick, verifies checksums, advances tick.
    /// </summary>
    public void OnSimTick()
    {
        if (!IsActive) return;

        // In solo+mirror mode (PlayerCount==1), P1 follows P0 — auto-mirror queued cmds.
        if (PlayerCount == 1)
            AutoMirrorP0ToP1();

        // Execute P0 commands scheduled for this tick.
        ApplyQueue(_p0Queue, _simTick, 0);
        // Execute P1 commands (AI or remote).
        ApplyQueue(_p1Queue, _simTick, 1);

        // Verify checksum after commands applied.
        var gm = GameManager.Instance;
        if (gm?.checksum != null && gm.transport != null && gm.transport.IsConnected)
        {
            uint h = gm.checksum.LatestChecksum;
            // Broadcast local checksum; TransportLayer echoes remote checksum back.
            gm.transport.SendChecksum(_simTick, h);
            // OnChecksumReceived is wired to DesyncHandler.CheckTick in TransportLayer.
        }

        _simTick++;
    }

    void ApplyQueue(Dictionary<int, Queue<GameCommand>> queues, int tick, int playerId)
    {
        if (!queues.TryGetValue(tick, out var q)) return;
        var gm = GameManager.Instance;
        while (q.Count > 0)
        {
            var cmd = q.Dequeue();
            // Forward to CommandRecorder so ChecksumSystem replay sees all commands.
            gm?.cmdRecorder?.Record(cmd.type, cmd.unitIds,
                cmd.intParam1, cmd.intParam2, cmd.x, cmd.z,
                cmd.floatParam1, cmd.floatParam2);
        }
        queues.Remove(tick);
    }

    void AutoMirrorP0ToP1()
    {
        // Look ahead INPUT_DELAY frames in P0 queue and copy to P1.
        foreach (var kv in _p0Queue)
        {
            int execTick = kv.Key;
            if (!_p1Queue.ContainsKey(execTick))
            {
                var q = new Queue<GameCommand>(kv.Value);
                foreach (var cmd in q)
                {
                    var m = cmd; m.playerId = 1;
                    if (!_p1Queue.TryGetValue(execTick, out var mq)) _p1Queue[execTick] = mq = new Queue<GameCommand>();
                    mq.Enqueue(m);
                }
            }
        }
    }
}
