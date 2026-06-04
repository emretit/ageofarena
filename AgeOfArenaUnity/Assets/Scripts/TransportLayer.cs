using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N17.transport: WebSocket-based command transport and lobby layer (stub).
///
/// Architecture:
///   - Each peer runs a Unity WebGL build (or native player).
///   - A lightweight relay server (Node.js / Cloudflare Worker) forwards
///     opaque command packets between peers.
///   - Only commands cross the wire (seed + command log), never full state.
///   - Lobby: one peer generates the seed and civ selections; broadcast via
///     the relay; all peers call GameBootstrap.Restart(seed) simultaneously.
///
/// This stub provides the interface that LockstepSystem calls. Full WebSocket
/// implementation requires a native/WebGL socket plugin (e.g. NativeWebSocket).
///
/// Status: interface defined + loopback (in-process) transport provided.
/// Real WebSocket wiring = plug in NativeWebSocket in Connect() and Send().
/// </summary>
public sealed class TransportLayer : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<GameCommand> OnCommandReceived;
    public event Action<uint>        OnChecksumReceived; // remote peer checksum
    public event Action              OnConnected;
    public event Action<string>      OnDisconnected;

    // ── State ─────────────────────────────────────────────────────────────────

    public bool IsConnected { get; private set; }
    public string PeerId    { get; private set; }
    public string RemoteId  { get; private set; }

    TransportMode _mode = TransportMode.Loopback;

    public enum TransportMode
    {
        Loopback,   // in-process: P0 and P1 on same machine (testing)
        WebSocket,  // real WebSocket relay (production)
    }

    // ── Connection ────────────────────────────────────────────────────────────

    /// <summary>
    /// Open a connection to the relay server.
    /// In Loopback mode, immediately fires OnConnected.
    /// In WebSocket mode, connect to <paramref name="relayUrl"/> and wait for handshake.
    /// </summary>
    public void Connect(string relayUrl = null, TransportMode mode = TransportMode.Loopback)
    {
        _mode = mode;
        PeerId = Guid.NewGuid().ToString("N").Substring(0, 8);

        if (mode == TransportMode.Loopback)
        {
            IsConnected = true;
            RemoteId    = "loopback";
            OnConnected?.Invoke();
            return;
        }

        // WebSocket stub — requires NativeWebSocket or similar package.
        // TODO: new WebSocket(relayUrl) → OnOpen/OnMessage/OnClose callbacks.
        Debug.LogWarning("[Transport] WebSocket transport not yet implemented. Use Loopback for local testing.");
    }

    public void Disconnect()
    {
        IsConnected = false;
        OnDisconnected?.Invoke("local disconnect");
    }

    // ── Send / Receive ────────────────────────────────────────────────────────

    /// <summary>
    /// Send a command to all remote peers.
    /// In Loopback mode the command is echoed back to OnCommandReceived.
    /// </summary>
    public void SendCommand(GameCommand cmd)
    {
        if (!IsConnected) return;
        if (_mode == TransportMode.Loopback)
        {
            // Echo to simulate remote player sending the same command.
            OnCommandReceived?.Invoke(cmd);
            return;
        }
        // TODO: serialize cmd to bytes and send via WebSocket.
    }

    /// <summary>
    /// Broadcast local checksum for the given tick to all peers.
    /// </summary>
    public void SendChecksum(int tick, uint hash)
    {
        if (!IsConnected) return;
        if (_mode == TransportMode.Loopback)
        {
            // Self-echo (loopback peer has same state → no desync).
            OnChecksumReceived?.Invoke(hash);
            return;
        }
        // TODO: serialize and send via WebSocket.
    }

    // ── Lobby ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Broadcast lobby setup (seed, civ, difficulty, map type) to all peers.
    /// All peers call GameBootstrap.Restart(seed) on receipt.
    /// </summary>
    public void SendLobbySetup(int seed, int civId, int difficulty, int mapType)
    {
        if (!IsConnected) return;
        if (_mode == TransportMode.Loopback)
        {
            ApplyLobbySetup(seed, civId, difficulty, mapType);
            return;
        }
        // TODO: serialize and broadcast.
    }

    void ApplyLobbySetup(int seed, int civId, int difficulty, int mapType)
    {
        GameBootstrap.NextMapType     = (MapType)mapType;
        GameBootstrap.NextDifficulty  = (Difficulty)difficulty;
        GameBootstrap.PlayerCiv       = (Civilization)civId;
        GameBootstrap.Restart(seed);
    }
}
