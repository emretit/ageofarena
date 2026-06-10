// NativeWebSocket — WebGL + Editor/Standalone compatible WebSocket wrapper.
// WebGL: delegates to WebSocketBridge.jslib (browser WebSocket API).
// Editor/Standalone: uses System.Net.WebSockets via a background thread.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading.Tasks;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR

public class NativeWebSocket : IDisposable
{
    public event Action          OnOpen;
    public event Action<string>  OnMessage;
    public event Action          OnError;
    public event Action<int>     OnClose;

    int _id;
    static NativeWebSocket[] _instances = new NativeWebSocket[256];
    readonly Queue<Action> _mainQueue = new Queue<Action>();

    [DllImport("__Internal")] static extern int  WS_Create(string url);
    [DllImport("__Internal")] static extern void WS_Send(int id, byte[] data, int len);
    [DllImport("__Internal")] static extern void WS_Close(int id, int code);
    [DllImport("__Internal")] static extern int  WS_State(int id);

    [DllImport("__Internal")] static extern void WS_SetCallbacks(
        Action<int> onOpen,
        Action<int, IntPtr, int> onMessage,
        Action<int> onError,
        Action<int, int> onClose);

    [AOT.MonoPInvokeCallback(typeof(Action<int>))]
    static void OnOpenCb(int id)       { _instances[id]?.MainThread(() => _instances[id].OnOpen?.Invoke()); }
    [AOT.MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
    static void OnMessageCb(int id, IntPtr ptr, int len)
    {
        var s = Marshal.PtrToStringAnsi(ptr, len);
        _instances[id]?.MainThread(() => _instances[id].OnMessage?.Invoke(s));
    }
    [AOT.MonoPInvokeCallback(typeof(Action<int>))]
    static void OnErrorCb(int id)      { _instances[id]?.MainThread(() => _instances[id].OnError?.Invoke()); }
    [AOT.MonoPInvokeCallback(typeof(Action<int, int>))]
    static void OnCloseCb(int id, int code) { _instances[id]?.MainThread(() => _instances[id].OnClose?.Invoke(code)); }

    static NativeWebSocket()
    {
        WS_SetCallbacks(OnOpenCb, OnMessageCb, OnErrorCb, OnCloseCb);
    }

    public NativeWebSocket(string url)
    {
        _id = WS_Create(url);
        if (_id < _instances.Length) _instances[_id] = this;
    }

    public bool IsOpen => WS_State(_id) == 1;

    public void Send(string msg)
    {
        var b = Encoding.UTF8.GetBytes(msg);
        WS_Send(_id, b, b.Length);
    }

    public void Close(int code = 1000) => WS_Close(_id, code);

    public void DispatchMessageQueue()
    {
        lock (_mainQueue)
            while (_mainQueue.Count > 0)
                _mainQueue.Dequeue()?.Invoke();
    }

    void MainThread(Action a) { lock (_mainQueue) _mainQueue.Enqueue(a); }

    public void Dispose() { Close(); if (_id < _instances.Length) _instances[_id] = null; }
}

#else

// ── Editor / Standalone fallback using ClientWebSocket ────────────────────

public class NativeWebSocket : IDisposable
{
    public event Action          OnOpen;
    public event Action<string>  OnMessage;
    public event Action          OnError;
    public event Action<int>     OnClose;

    ClientWebSocket _ws;
    CancellationTokenSource _cts = new CancellationTokenSource();
    readonly Queue<Action> _mainQueue = new Queue<Action>();

    public bool IsOpen => _ws?.State == WebSocketState.Open;

    public NativeWebSocket(string url)
    {
        _ws = new ClientWebSocket();
        _ = ConnectAsync(url);
    }

    async Task ConnectAsync(string url)
    {
        try
        {
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            MainThread(() => OnOpen?.Invoke());
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WS] Connect failed: {e.Message}");
            MainThread(() => OnError?.Invoke());
        }
    }

    async Task ReceiveLoop()
    {
        var buf = new byte[8192];
        var sb  = new StringBuilder();
        while (_ws.State == WebSocketState.Open)
        {
            try
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    MainThread(() => OnClose?.Invoke((int)result.CloseStatus));
                    break;
                }
                var msg = sb.ToString();
                MainThread(() => OnMessage?.Invoke(msg));
            }
            catch { break; }
        }
    }

    public void Send(string msg)
    {
        if (!IsOpen) return;
        var b = Encoding.UTF8.GetBytes(msg);
        _ = _ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, _cts.Token);
    }

    public void Close(int code = 1000)
    {
        _cts.Cancel();
        if (_ws?.State == WebSocketState.Open)
            _ = _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    public void DispatchMessageQueue()
    {
        lock (_mainQueue)
            while (_mainQueue.Count > 0)
                _mainQueue.Dequeue()?.Invoke();
    }

    void MainThread(Action a) { lock (_mainQueue) _mainQueue.Enqueue(a); }

    public void Dispose() { Close(); _ws?.Dispose(); }
}

#endif
