using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// N17.replay: Replay viewer with speed control and forward playback.
///
/// Flow:
///   1. Player saves replay via HUD → ChecksumSystem.SaveReplaySnapshot().
///   2. HUD "Replay Viewer" button → ReplayViewer.StartViewer().
///   3. Game restarts at same seed; ReplayViewer loads command log.
///   4. Commands replay at configurable speed (0.5× / 1× / 2× / 4×).
///   5. Viewer UI shows current tick, speed, and PASS/FAIL result.
///
/// Full rewind requires recording full state snapshots each tick (expensive).
/// This implementation provides forward-only playback; "rewind" = restart replay.
///
/// Integration: WorldRoot.Build() calls ReplayViewer.TryAutoStart() if a
/// replay baseline is waiting.
/// </summary>
public class ReplayViewer : MonoBehaviour
{
    static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.13f, 0.90f);
    static readonly Color White   = new Color(0.95f, 0.96f, 1.00f);
    static readonly Color Gold    = new Color(0.95f, 0.82f, 0.42f);
    static readonly Color Green   = new Color(0.40f, 1.00f, 0.50f);
    static readonly Color Red     = new Color(1.00f, 0.35f, 0.35f);

    // ── State ─────────────────────────────────────────────────────────────────

    bool _active;
    float _playbackSpeed = 1f;
    int   _lastTick = -1;
    Text  _tickLabel;
    Text  _statusLabel;
    Canvas _canvas;

    static readonly float[] Speeds = { 0.5f, 1f, 2f, 4f };
    int _speedIdx = 1;

    // ── Auto-start ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by WorldRoot after Build() — if a baseline is loaded this is a
    /// replay run; start the viewer automatically.
    /// </summary>
    public static void TryAutoStart()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.checksum == null) return;
        // Baseline was loaded → this is a verify/replay run.
        if (!string.IsNullOrEmpty(GameBootstrap.ReplayBaseline)) return; // already consumed
        // Check if we have a saved replay to play.
        if (!PlayerPrefs.HasKey("AoA_Replay_0")) return;

        // Auto-open viewer in verify mode after first checksum is computed.
        var viewer = gm.GetComponent<ReplayViewer>();
        if (viewer == null) viewer = gm.gameObject.AddComponent<ReplayViewer>();
        viewer.StartViewer();
    }

    // ── Viewer ────────────────────────────────────────────────────────────────

    public void StartViewer()
    {
        _active = true;
        BuildUI();
        // Apply speed to TimeScale.
        Time.timeScale = _playbackSpeed;
    }

    public void StopViewer()
    {
        _active = false;
        Time.timeScale = 1f;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_active) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        int tick = gm.cmdRecorder != null ? gm.cmdRecorder.Tick : 0;
        if (tick != _lastTick)
        {
            _lastTick = tick;
            if (_tickLabel != null) _tickLabel.text = $"Tick: {tick}";
        }

        // Show verify result when available.
        if (gm.checksum != null && !string.IsNullOrEmpty(gm.checksum.ReplayVerifyResult))
        {
            bool pass = gm.checksum.ReplayVerifyResult == "PASS";
            if (_statusLabel != null)
            {
                _statusLabel.text  = pass ? "✅ PASS — Deterministik!" : $"❌ {gm.checksum.ReplayVerifyResult}";
                _statusLabel.color = pass ? Green : Red;
            }
            // Pause on result.
            if (pass) Time.timeScale = 0f;
        }
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void BuildUI()
    {
        if (_canvas != null) { _canvas.gameObject.SetActive(true); return; }

        var cgo = new GameObject("ReplayViewerCanvas");
        cgo.transform.SetParent(transform, false);
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 8000;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        cgo.AddComponent<GraphicRaycaster>();

        // Bottom bar
        var bar = NewRect("Bar", cgo.transform);
        bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
        bar.pivot = new Vector2(0.5f, 0);
        bar.sizeDelta = new Vector2(0, 54);
        bar.anchoredPosition = Vector2.zero;
        bar.gameObject.AddComponent<Image>().color = PanelBg;

        AddLabel(bar, "▶ REPLAY", -820f, 0, 18, Gold, FontStyle.Bold);
        _tickLabel  = AddLabel(bar, "Tick: 0",   -650f, 0, 16, White, FontStyle.Normal);
        _statusLabel = AddLabel(bar, "",          0f, 0, 16, White, FontStyle.Normal);

        // Speed button — store ref to btn so we can update the label text
        Text[] _speedRef = { null };
        MakeBtn(bar, $"×{_playbackSpeed:0.#}", 600f, 0, 100, 38, () =>
        {
            _speedIdx = (_speedIdx + 1) % Speeds.Length;
            _playbackSpeed = Speeds[_speedIdx];
            Time.timeScale = _playbackSpeed;
            if (_speedRef[0] != null) _speedRef[0].text = $"×{_playbackSpeed:0.#}";
        });
        // Capture the label: last Text child of bar is the speed button's label
        var allTexts = bar.GetComponentsInChildren<Text>();
        if (allTexts.Length > 0) _speedRef[0] = allTexts[allTexts.Length - 1];

        // Restart replay
        MakeBtn(bar, "↺ Yeniden", 720f, 0, 130, 38, () =>
        {
            StopViewer();
            GameManager.Instance?.checksum?.StartReplayVerify();
        });

        // Close
        MakeBtn(bar, "✕", 860f, 0, 50, 38, StopViewer);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static Text AddLabel(RectTransform parent, string text, float x, float y, int size,
        Color col, FontStyle style)
    {
        var rt = NewRect("Lbl", parent);
        rt.sizeDelta = new Vector2(280, 30); rt.anchoredPosition = new Vector2(x, y);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text; t.fontSize = size; t.color = col; t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft; t.font = null;
        return t;
    }

    static GameObject MakeBtn(RectTransform parent, string label, float x, float y,
        float w, float h, System.Action onClick)
    {
        var rt = NewRect("Btn", parent);
        rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        rt.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.20f, 0.34f);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());
        AddLabel(rt, label, 0, 0, 14, White, FontStyle.Normal).alignment = TextAnchor.MiddleCenter;
        return rt.gameObject;
    }
}
