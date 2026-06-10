using System;
using System.Collections.Generic;
using UnityEngine;

// ── Command type set ──────────────────────────────────────────────────────────

/// <summary>N3.cmdlog: All issued command types (player + AI).</summary>
public enum CommandType
{
    Move,         // x/z = destination
    AttackMove,   // x/z = destination
    Attack,       // intParam1 = targetId
    Gather,       // intParam1 = nodeId
    Build,        // intParam1 = buildingId (existing site)
    Garrison,     // intParam1 = buildingId
    Patrol,       // x/z = pointA, floatParam1/z2 = pointB
    SetStance,    // intParam1 = (int)AttackStance
    SetRally,     // x/z = rally position, intParam1 = buildingId
    Train,        // intParam1 = (int)UnitType, intParam2 = buildingId
    Research,     // intParam1 = (int)TechType, intParam2 = buildingId
    Delete,       // units in unitIds[]
    Stop,
    Ping,         // x/z = position
}

// ── Serialisable command record ───────────────────────────────────────────────

[Serializable]
public struct GameCommand
{
    /// <summary>Simulation frame at which this command was issued.</summary>
    public int tick;
    /// <summary>Team that issued the command (0 = local player).</summary>
    public int playerId;
    public CommandType type;
    /// <summary>IDs of units receiving the order (unitEntity.unitId).</summary>
    public int[] unitIds;
    /// <summary>First integer param (target ID, enum cast, kind, etc.).</summary>
    public int intParam1;
    public int intParam2;
    /// <summary>World-space X coordinate (move dest / ping pos / etc.).</summary>
    public float x;
    /// <summary>World-space Z coordinate.</summary>
    public float z;
    /// <summary>Optional second float (patrol point B, resource amount, etc.).</summary>
    public float floatParam1;
    public float floatParam2;
}

// ── Recorder MonoBehaviour ────────────────────────────────────────────────────

/// <summary>
/// N3.cmdlog: Records every issued GameCommand with a monotonic tick counter.
/// The log feeds N15.checksum replay verification: same seed + same log
/// should reproduce the same state checksum on headless re-simulation.
///
/// SP version: records without fixed-step (tick = frame count). Full
/// lockstep scheduling (T+inputDelay) lives in N16.lockstep.
/// </summary>
public class CommandRecorder : MonoBehaviour
{
    readonly List<GameCommand> _log = new();
    int _tick;

    // ── Tick counter ──────────────────────────────────────────────────────────

    /// <summary>Current simulation frame number.</summary>
    public int Tick => _tick;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm?.match != null && !gm.match.IsOver)
            _tick++;
    }

    // ── Recording API ─────────────────────────────────────────────────────────

    /// <summary>Log a command from the current player team.</summary>
    public void Record(CommandType type, int[] unitIds,
        int intParam1 = 0, int intParam2 = 0,
        float x = 0f, float z = 0f,
        float floatParam1 = 0f, float floatParam2 = 0f)
    {
        var cmd = new GameCommand
        {
            tick        = _tick,
            playerId    = GameBootstrap.LocalTeam,
            type        = type,
            unitIds     = unitIds,
            intParam1   = intParam1,
            intParam2   = intParam2,
            x           = x,
            z           = z,
            floatParam1 = floatParam1,
            floatParam2 = floatParam2,
        };
        _log.Add(cmd);

        // MP-5: multiplayer'da her yerel komutu sunucuya ilet.
        if (GameBootstrap.IsMultiplayer)
        {
            var gm = GameManager.Instance;
            gm?.transport?.SendCommand(cmd);
        }
    }

    /// <summary>MP-5: uzak oyuncudan gelen komutu logla (ağa geri gönderme).</summary>
    public void RecordRemote(GameCommand cmd)
    {
        cmd.tick = _tick;
        _log.Add(cmd);
    }

    // ── Snapshot / Load ───────────────────────────────────────────────────────

    public List<GameCommand> Snapshot() => new(_log);

    public void LoadSnapshot(List<GameCommand> saved)
    {
        _log.Clear();
        if (saved != null) _log.AddRange(saved);
    }

    public void Clear() { _log.Clear(); _tick = 0; }

    /// <summary>All commands issued at exactly the given tick.</summary>
    public IEnumerable<GameCommand> ForTick(int tick)
    {
        foreach (var c in _log)
            if (c.tick == tick) yield return c;
    }

    /// <summary>Total number of recorded commands.</summary>
    public int Count => _log.Count;
}
