using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WebSocket transport için oyun mesajlaşma katmanı.
/// JSON mesajlar NativeWebSocket üzerinden sunucuya gönderilir/alınır.
/// WebGL + Editor/Standalone uyumlu.
/// </summary>
public sealed class TransportLayer : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<GameCommand>  OnCommandReceived;
    public event Action<uint>         OnChecksumReceived;
    public event Action               OnConnected;
    public event Action<string>       OnDisconnected;
    public event Action<ServerMsg>    OnRawMessage;      // lobi UI için

    // ── State ─────────────────────────────────────────────────────────────────
    public bool   IsConnected  { get; private set; }
    public string PlayerId     { get; private set; }
    public string RoomCode     { get; private set; }
    public int    LocalTeam    { get; private set; } = -1;

    NativeWebSocket _ws;
    string _serverUrl;

    // ── Bağlantı ──────────────────────────────────────────────────────────────
    public void Connect(string serverUrl)
    {
        _serverUrl = serverUrl;
        _ws = new NativeWebSocket(serverUrl);
        _ws.OnOpen    += HandleOpen;
        _ws.OnMessage += HandleMessage;
        _ws.OnError   += HandleError;
        _ws.OnClose   += HandleClose;
    }

    void Update() => _ws?.DispatchMessageQueue();

    void OnDestroy() => _ws?.Dispose();

    // ── Lobi ──────────────────────────────────────────────────────────────────
    public void CreateRoom(string playerName)
        => SendJson(new { type = "create_room", playerName });

    public void JoinRoom(string roomCode, string playerName)
        => SendJson(new { type = "join_room", roomCode, playerName });

    public void SendReady()
        => SendJson(new { type = "ready" });

    public void SendChat(string message)
        => SendJson(new { type = "chat", message });

    // ── Oyun içi ──────────────────────────────────────────────────────────────
    public void SendCommand(GameCommand cmd)
    {
        if (!IsConnected) return;
        SendJson(new { type = "input", tick = cmd.tick, commands = new[] { CommandToObj(cmd) } });
    }

    public void SendChecksum(int tick, uint hash)
    {
        if (!IsConnected) return;
        SendJson(new { type = "checksum", tick, hash });
    }

    // ── Mesaj gönder ──────────────────────────────────────────────────────────
    void SendJson(object obj)
    {
        if (_ws == null || !IsConnected) return;
        _ws.Send(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
    }

    // ── Mesaj al ─────────────────────────────────────────────────────────────
    void HandleOpen()
    {
        IsConnected = true;
        Debug.Log("[Transport] bağlandı");
        OnConnected?.Invoke();
    }

    void HandleMessage(string raw)
    {
        ServerMsg msg;
        try { msg = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerMsg>(raw); }
        catch { return; }

        OnRawMessage?.Invoke(msg);

        switch (msg.type)
        {
            case "room_created":
                PlayerId  = msg.playerId;
                RoomCode  = msg.roomCode;
                LocalTeam = 0;
                break;

            case "room_joined":
                PlayerId  = msg.playerId;
                RoomCode  = msg.roomCode;
                LocalTeam = msg.team;
                break;

            case "game_start":
                ApplyGameStart(msg);
                break;

            case "tick_inputs":
                ApplyTickInputs(msg);
                break;

            case "checksum":
                if (msg.hash != 0) OnChecksumReceived?.Invoke((uint)msg.hash);
                break;

            case "desync":
                Debug.LogError($"[Transport] DESYNC tick={msg.tick}");
                var gm2 = GameManager.Instance;
                if (gm2?.desync != null)
                    gm2.desync.CheckTick(msg.tick, 0u, 1u); // farklı hash → desync tetikle
                break;
        }
    }

    void HandleError()
    {
        Debug.LogWarning("[Transport] WebSocket hatası");
        IsConnected = false;
    }

    void HandleClose(int code)
    {
        IsConnected = false;
        Debug.Log($"[Transport] kapatıldı code={code}");
        OnDisconnected?.Invoke($"code={code}");
    }

    // ── Oyun başlangıcı ───────────────────────────────────────────────────────
    void ApplyGameStart(ServerMsg msg)
    {
        int seed = msg.seed;
        int playerCount = msg.players?.Count ?? 2;
        Debug.Log($"[Transport] game_start seed={seed} team={LocalTeam} players={playerCount}");

        GameBootstrap.IsMultiplayer      = true;
        GameBootstrap.LocalTeam          = LocalTeam;
        GameBootstrap.OnlinePlayerCount  = playerCount;
        GameBootstrap.NextMapType        = MapType.Arena;
        GameBootstrap.NextDifficulty     = Difficulty.Normal;
        GameBootstrap.Restart(seed);
    }

    // ── Tick input dağıtımı ───────────────────────────────────────────────────
    void ApplyTickInputs(ServerMsg msg)
    {
        if (msg.inputs == null) return;
        foreach (var inp in msg.inputs)
        {
            if (inp.commands == null) continue;
            foreach (var cmd in inp.commands)
            {
                if (cmd is Newtonsoft.Json.Linq.JObject jo)
                {
                    var gc = ObjToCommand(jo, inp.tick);
                    OnCommandReceived?.Invoke(gc);
                }
            }
        }
    }

    // ── Serializasyon yardımcıları ────────────────────────────────────────────
    static object CommandToObj(GameCommand cmd) => new
    {
        type       = (int)cmd.type,
        playerId   = cmd.playerId,
        unitIds    = cmd.unitIds,
        x          = cmd.x,
        z          = cmd.z,
        intParam1  = cmd.intParam1,
        intParam2  = cmd.intParam2,
        floatParam1= cmd.floatParam1,
        floatParam2= cmd.floatParam2,
    };

    static GameCommand ObjToCommand(Newtonsoft.Json.Linq.JObject d, int tick)
    {
        return new GameCommand
        {
            tick        = tick,
            type        = (CommandType)(int)d["type"],
            playerId    = (int)d["playerId"],
            unitIds     = d["unitIds"]?.ToObject<int[]>() ?? Array.Empty<int>(),
            x           = (float)d["x"],
            z           = (float)d["z"],
            intParam1   = (int)d["intParam1"],
            intParam2   = (int)d["intParam2"],
            floatParam1 = (float)d["floatParam1"],
            floatParam2 = (float)d["floatParam2"],
        };
    }
}

// ── Sunucu mesaj modeli (JSON deserialization) ─────────────────────────────
[Serializable]
public class ServerMsg
{
    public string type;
    public string roomCode;
    public string playerId;
    public int    team;
    public int    seed;
    public int    tick;
    public uint   hash;
    public string name;
    public string message;
    public List<ServerPlayer> players;
    public List<ServerPlayer> playerList;
    public List<TickInput>    inputs;
}

[Serializable]
public class ServerPlayer
{
    public string id;
    public string name;
    public int    team;
    public bool   ready;
}

[Serializable]
public class TickInput
{
    public string     playerId;
    public int        tick;
    public List<object> commands;
}
