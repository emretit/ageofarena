using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// N13.camp: Campaign mission selection screen.
/// Accessible from the HUD pause menu ("Kampanya" button).
/// Shows the 3-mission chain with locked/unlocked/completed state.
/// Clicking an available mission sets CampaignSystem.ActiveMissionId,
/// then restarts the game; WorldRoot.SetupGameplay calls CampaignSystem.Setup(gm).
/// </summary>
public class CampaignScreen : MonoBehaviour
{
    static readonly Color BgCol        = new Color(0.04f, 0.06f, 0.12f, 0.96f);
    static readonly Color CardNormal   = new Color(0.10f, 0.16f, 0.28f, 1f);
    static readonly Color CardLocked   = new Color(0.12f, 0.12f, 0.16f, 0.8f);
    static readonly Color CardDone     = new Color(0.06f, 0.24f, 0.10f, 1f);
    static readonly Color Gold         = new Color(0.95f, 0.82f, 0.42f);
    static readonly Color White        = new Color(0.95f, 0.96f, 1.00f);
    static readonly Color Dim          = new Color(0.55f, 0.60f, 0.70f);
    static readonly Color LockedGrey   = new Color(0.40f, 0.42f, 0.48f);
    static readonly Color StartGreen   = new Color(0.16f, 0.38f, 0.18f);
    static readonly Color StartHover   = new Color(0.24f, 0.54f, 0.26f);

    Canvas _canvas;

    public void Show()
    {
        if (_canvas == null) Build();
        _canvas.gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    void Build()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("CampaignES");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var cgo = new GameObject("CampaignCanvas");
        cgo.transform.SetParent(transform, false);
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 7000;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        cgo.AddComponent<GraphicRaycaster>();

        // Backdrop
        var bg = NewRect("Bg", cgo.transform);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
        bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<Image>().color = BgCol;

        // Title
        AddLabel(bg, "⚔ Kampanya", 0, 430, 48, Gold, FontStyle.Bold);
        AddLabel(bg, "Görev zincirini tamamla — her zafer sıradakini açar.", 0, 378, 20, Dim, FontStyle.Normal);
        AddLine(bg, 0, 356, 900, 2);

        // Mission cards
        var missions = CampaignSystem.Missions;
        float cardY = 260f;
        float cardGap = 170f;
        for (int i = 0; i < missions.Length; i++)
        {
            BuildMissionCard(bg, missions[i], cardY - i * cardGap);
        }

        // Reset + Close buttons
        AddBtn(bg, "Sıfırla", -100f, -400f, 140, 42, Gold, () =>
        {
            CampaignSystem.ResetProgress();
            // Rebuild UI to reflect reset
            Object.Destroy(cgo);
            _canvas = null;
            Build();
            _canvas.gameObject.SetActive(true);
        });
        AddBtn(bg, "Kapat", 100f, -400f, 140, 42, Dim, () => Hide());
    }

    void BuildMissionCard(RectTransform parent, CampaignSystem.Mission m, float y)
    {
        bool unlocked = CampaignSystem.IsMissionUnlocked(m.id);
        bool done     = CampaignSystem.IsMissionComplete(m.id);

        var card = NewRect($"Card_{m.id}", parent);
        card.sizeDelta        = new Vector2(880, 150);
        card.anchoredPosition = new Vector2(0, y);
        var img = card.gameObject.AddComponent<Image>();
        img.color = done ? CardDone : unlocked ? CardNormal : CardLocked;

        // Mission number badge
        var numCol = done ? new Color(0.5f, 1f, 0.55f) : unlocked ? Gold : LockedGrey;
        AddLabel(card, $"{m.id + 1}.", -390f, 0, 36, numCol, FontStyle.Bold);

        // Title + briefing
        var titleCol = done ? new Color(0.6f, 1f, 0.65f) : unlocked ? White : LockedGrey;
        AddLabel(card, m.name, -100f, 42, 24, titleCol, FontStyle.Bold);

        var briefCol = done ? new Color(0.55f, 0.85f, 0.60f) : unlocked ? Dim : LockedGrey;
        var bText = card.gameObject.transform.Find("Brf") ? null
                  : AddLabel(card, m.briefing, -100f, 0, 15, briefCol, FontStyle.Normal);
        if (bText != null) bText.name = "Brf";

        // Status badge
        string statusStr = done ? "✅ Tamamlandı" : unlocked ? "🔓 Hazır" : "🔒 Kilitli";
        var statusCol    = done ? new Color(0.4f, 1f, 0.5f) : unlocked ? Gold : LockedGrey;
        AddLabel(card, statusStr, 280f, 42, 18, statusCol, FontStyle.Normal);

        // Start button (only if unlocked)
        if (unlocked && !done)
        {
            int captured = m.id;
            var startBtn = NewRect("StartBtn", card);
            startBtn.sizeDelta        = new Vector2(130, 46);
            startBtn.anchoredPosition = new Vector2(360f, -20f);
            var sImg = startBtn.gameObject.AddComponent<Image>();
            sImg.color = StartGreen;
            var sBtn = startBtn.gameObject.AddComponent<Button>();
            var sCols = sBtn.colors;
            sCols.normalColor      = Color.white;
            sCols.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            sCols.pressedColor     = new Color(1.6f, 1.6f, 1.6f);
            sCols.fadeDuration     = 0.07f;
            sBtn.colors = sCols;
            AddLabel(startBtn, "▶ Başla", 0, 0, 20, new Color(0.9f, 1f, 0.9f), FontStyle.Bold);
            sBtn.onClick.AddListener(() => StartMission(captured));
        }
        else if (done)
        {
            // Replay button
            int captured = m.id;
            var replayBtn = NewRect("ReplayBtn", card);
            replayBtn.sizeDelta        = new Vector2(130, 46);
            replayBtn.anchoredPosition = new Vector2(360f, -20f);
            replayBtn.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.30f, 0.12f, 1f);
            var rBtn = replayBtn.gameObject.AddComponent<Button>();
            AddLabel(replayBtn, "↺ Tekrar", 0, 0, 18, new Color(0.7f, 1f, 0.75f), FontStyle.Normal);
            rBtn.onClick.AddListener(() => StartMission(captured));
        }
    }

    static void StartMission(int id)
    {
        CampaignSystem.ActiveMissionId = id;
        Time.timeScale = 1f;
        GameBootstrap.Restart();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static Text AddLabel(RectTransform parent, string text, float x, float y,
        int size, Color col, FontStyle style)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 34);
        rt.anchoredPosition = new Vector2(x, y);
        var t = go.AddComponent<Text>();
        t.text      = text;
        t.fontSize  = size;
        t.color     = col;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft;
        t.font      = null;
        return t;
    }

    static void AddLine(RectTransform parent, float x, float y, float w, float h)
    {
        var go = NewRect("Line", parent);
        go.sizeDelta = new Vector2(w, h);
        go.anchoredPosition = new Vector2(x, y);
        go.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.82f, 0.42f, 0.4f);
    }

    static void AddBtn(RectTransform parent, string label, float x, float y,
        float w, float h, Color col, System.Action onClick)
    {
        var go = NewRect("Btn_" + label, parent);
        go.sizeDelta = new Vector2(w, h);
        go.anchoredPosition = new Vector2(x, y);
        var img = go.gameObject.AddComponent<Image>();
        img.color = new Color(col.r * 0.4f, col.g * 0.4f, col.b * 0.4f, 0.8f);
        var btn = go.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());
        AddLabel(go, label, 0, 0, 18, col, FontStyle.Normal).alignment = TextAnchor.MiddleCenter;
    }
}
