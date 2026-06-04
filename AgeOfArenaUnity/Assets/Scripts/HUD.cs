using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime-built uGUI HUD. Top bar shows resources + age. The bottom is an
/// Age of Empires-style command bar: a left info panel (selected building/units
/// + hp bar) and a right command card of clickable buttons. Selecting a building
/// shows its train/research/age buttons; selecting villagers shows the build menu.
/// Buttons call the same systems as the keyboard hotkeys (both stay valid), and
/// dim out when the action is unaffordable.
/// </summary>
public class HUD : MonoBehaviour
{
    Font _font;
    ResourceManager _res;
    Transform _canvasRoot;
    bool _gameOverShown;
    GameObject _pauseMenu;

    // Top bar
    Text _foodText, _woodText, _goldText, _stoneText, _popText, _ageText, _relicText;
    Button _idleButton; Text _idleText;
    Text _victoryText; RectTransform _victoryRect;
    Text _difficultyText;
    Text _civText;

    // ── Command bar ──────────────────────────────────────────────────────────
    RectTransform _cmdBar;
    // Left info panel
    Text _infoName, _infoSub, _hpText;
    RectTransform _hpBarBg, _hpBarFill;
    Image _hpBarFillImg;
    // Right command card
    RectTransform _cardRoot, _gridRoot, _progressFill;
    Image _progressFillImg;
    Text _queueText;
    // Shared hover tooltip (floats above the command bar).
    RectTransform _tooltip;
    Text _tipTitle, _tipBody;
    // Training queue strip (clickable unit icons; click cancels + refunds).
    RectTransform _queueStrip;
    readonly List<GameObject> _queueIcons = new();
    Image _queueFrontFill;
    string _queueSig = "";

    // Clickable command button + its enable predicate (re-evaluated each frame).
    class CommandSlot
    {
        public Button btn;
        public Image bg;
        public Color baseColor;
        public System.Func<bool> affordable;
        public bool? lastOk;
    }
    readonly List<CommandSlot> _slots = new();

    // Selection signature for rebuild detection.
    BuildingEntity _lastBld;
    int _lastUnitCount = -1;
    bool _lastHasVillager;
    int _lastTechVer = -1;
    int _lastQueueCount = -1;

    const int   Cols   = 5;
    const int   Rows   = 3;            // fixed AoE-style slot grid (Cols×Rows)
    const float BtnW   = 60f, BtnH = 60f, Gap = 6f;
    const float BarH   = 220f;
    const float LeftW  = 240f;         // info panel width
    const float CmdZoneW = 352f;       // left command-grid zone (14 + 5×60+4×6 + 14)
    const float MinW   = 230f;         // right minimap zone width (diamond sits here)

    // Right minimap zone inside the bottom bar — MinimapSystem parents its diamond here.
    RectTransform _minimapZone;
    public RectTransform MinimapZone => _minimapZone;

    // Command-button category colors.
    static readonly Color TrainCol  = Prims.Hex(0x3a6ea5);
    static readonly Color UpgCol    = Prims.Hex(0x7d5ba6);
    static readonly Color AgeCol    = Prims.Hex(0xc8a13a);
    static readonly Color BuildCol  = Prims.Hex(0x3f8f4f);
    static readonly Color MarketCol = Prims.Hex(0x2e8b8b);
    static readonly Color CmdCol    = Prims.Hex(0x5a6270);
    static readonly Color GarrisonCol = Prims.Hex(0x9a6b3f);

    public void Init(ResourceManager res)
    {
        _res = res;
        _font = ResolveFont();
        BuildCanvas();
        Refresh();
        if (_ageText != null) _ageText.text = "Çağ: " + AgeName(Age.Dark);
        _res.OnChanged += Refresh;
        GameEvents.OnAgeAdvanced += OnAgeAdvanced;
    }

    void OnDestroy()
    {
        if (_res != null) _res.OnChanged -= Refresh;
        GameEvents.OnAgeAdvanced -= OnAgeAdvanced;
    }

    void OnAgeAdvanced(int team, Age newAge)
    {
        if (team != 0 || _ageText == null) return;
        _ageText.text = "Çağ: " + AgeName(newAge);
        if (_canvasRoot != null)
            StartCoroutine(ShowAgePopup(newAge));
    }

    IEnumerator ShowAgePopup(Age newAge)
    {
        var popup = NewRect("AgePopup", _canvasRoot);
        popup.anchorMin = new Vector2(0.5f, 1f);
        popup.anchorMax = new Vector2(0.5f, 1f);
        popup.pivot     = new Vector2(0.5f, 1f);
        popup.sizeDelta = new Vector2(600, 60);
        popup.anchoredPosition = new Vector2(0, -70);

        var bg = popup.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);

        var t = AddText(popup, AgeName(newAge).ToUpper() + " ÇAĞI!", TextAnchor.MiddleCenter);
        t.fontSize   = 30;
        t.fontStyle  = FontStyle.Bold;
        t.color      = Prims.Hex(0xf2d59b);

        const float hold = 2.5f;
        const float fade = 0.5f;
        yield return new WaitForSeconds(hold);

        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            float a = 1f - elapsed / fade;
            bg.color = new Color(0f, 0f, 0f, 0.65f * a);
            t.color  = new Color(t.color.r, t.color.g, t.color.b, a);
            yield return null;
        }
        Destroy(popup.gameObject);
    }

    static Font ResolveFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        // Font.CreateDynamicFontFromOSFont is not supported on WebGL; returning null
        // causes Unity's Text component to fall back to its internal default font.
        return f;
    }

    void BuildCanvas()
    {
        var canvasGo = new GameObject("HUDCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvasRoot = canvasGo.transform;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // Balance width/height matching so the bar keeps its proportions across
        // ultrawide and 4:3 alike (match=0 would only track width).
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // uGUI buttons need a raycaster on the canvas + a scene EventSystem.
        canvasGo.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(transform, false);
            esGo.AddComponent<EventSystem>();
            // StandaloneInputModule works because activeInputHandler=2 (Both) keeps
            // legacy UnityEngine.Input enabled. This project's InputManager.asset has
            // no "Submit"/"Cancel" axes, so the module's nav polling would throw every
            // frame — route it through SafeBaseInput which swallows those exceptions.
            var module = esGo.AddComponent<StandaloneInputModule>();
            module.inputOverride = esGo.AddComponent<SafeBaseInput>();
        }

        BuildTopBar(canvasGo.transform);
        BuildCommandBar(canvasGo.transform);
    }

    void BuildTopBar(Transform parent)
    {
        var bar = NewRect("TopBar", parent);
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot     = new Vector2(0.5f, 1);
        bar.sizeDelta = new Vector2(0, 64);
        bar.anchoredPosition = Vector2.zero;
        var topBg = bar.gameObject.AddComponent<Image>();
        topBg.color = new Color(0f, 0f, 0f, 0.55f);            // flat fallback
        UiSkin.SkinPanel(topBg, UiSkin.BarBg, Color.white);    // wooden frame when kit present

        float x = 24f;
        _foodText  = AddEntry(bar, ref x, Prims.Hex(0xd64545), "Food");
        _woodText  = AddEntry(bar, ref x, Prims.Hex(0x8a5a2b), "Wood");
        _goldText  = AddEntry(bar, ref x, Prims.Hex(0xf2c14e), "Gold");
        _stoneText = AddEntry(bar, ref x, Prims.Hex(0xb9b9b9), "Stone");
        _popText   = AddEntry(bar, ref x, Prims.Hex(0x6fa8dc), "Pop");
        _relicText = AddEntry(bar, ref x, Prims.Hex(0xe0b84b), "Relic");
        _relicText.color = Prims.Hex(0xf2d59b);

        var ageRect = NewRect("AgeText", bar);
        ageRect.anchorMin = new Vector2(1, 0.5f); ageRect.anchorMax = new Vector2(1, 0.5f);
        ageRect.pivot = new Vector2(1, 0.5f);
        ageRect.sizeDelta = new Vector2(260, 30);
        ageRect.anchoredPosition = new Vector2(-24, 0);
        _ageText = AddText(ageRect, "", TextAnchor.MiddleRight);
        _ageText.fontSize = 20;
        _ageText.fontStyle = FontStyle.Bold;
        _ageText.color = Prims.Hex(0xf2d59b);
        AddOutline(_ageText, 0.6f);

        BuildIdleIndicator(bar);
        BuildDifficultyIndicator(bar);
        BuildCivIndicator(bar);
        BuildVictoryBanner(parent);
    }

    /// <summary>Clickable difficulty pill (top bar). Cycles Easy→Normal→Hard→Insane and
    /// re-applies to every live <see cref="EnemyAI"/> immediately.</summary>
    void BuildDifficultyIndicator(RectTransform bar)
    {
        var rect = NewRect("Difficulty", bar);
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(150, 32);
        rect.anchoredPosition = new Vector2(-490, 0);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.30f, 0.92f);
        UiSkin.SkinPanel(img, UiSkin.PillNormal, Color.white);
        var btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(CycleDifficulty);
        _difficultyText = AddText(rect, "", TextAnchor.MiddleCenter);
        _difficultyText.fontSize = 15;
        _difficultyText.fontStyle = FontStyle.Bold;
        AddOutline(_difficultyText, 0.6f);
        UpdateDifficultyLabel();
    }

    void CycleDifficulty()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.difficulty = (Difficulty)(((int)gm.difficulty + 1) % 4);
        foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude)) ai.SetDifficulty();
        UpdateDifficultyLabel();
    }

    void UpdateDifficultyLabel()
    {
        if (_difficultyText == null) return;
        var gm = GameManager.Instance;
        _difficultyText.text = "Zorluk: " + (gm != null ? DiffName(gm.difficulty) : "");
    }

    static string DiffName(Difficulty d) => d switch
    {
        Difficulty.Easy   => "Kolay",
        Difficulty.Normal => "Normal",
        Difficulty.Hard   => "Zor",
        Difficulty.Insane => "Acımasız",
        _                 => "",
    };

    void BuildCivIndicator(RectTransform bar)
    {
        var rect = NewRect("Civ", bar);
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(170, 32);
        rect.anchoredPosition = new Vector2(-665, 0);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.18f, 0.14f, 0.30f, 0.92f);
        UiSkin.SkinPanel(img, UiSkin.PillNormal, Color.white);
        var btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(CycleCiv);
        _civText = AddText(rect, "", TextAnchor.MiddleCenter);
        _civText.fontSize = 14;
        _civText.fontStyle = FontStyle.Bold;
        AddOutline(_civText, 0.6f);
        UpdateCivLabel();
    }

    void CycleCiv()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var civs = (Civilization[])System.Enum.GetValues(typeof(Civilization));
        gm.playerCiv = civs[((int)gm.playerCiv + 1) % civs.Length];
        UpdateCivLabel();
    }

    void UpdateCivLabel()
    {
        if (_civText == null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        _civText.text = "Medeniyet: " + CivilizationDefs.Get(gm.playerCiv).display;
    }

    /// <summary>Top-centre banner that shows the active victory countdown
    /// (Wonder/relic). Hidden when no countdown is running.</summary>
    void BuildVictoryBanner(Transform parent)
    {
        _victoryRect = NewRect("VictoryBanner", parent);
        _victoryRect.anchorMin = new Vector2(0.5f, 1f);
        _victoryRect.anchorMax = new Vector2(0.5f, 1f);
        _victoryRect.pivot = new Vector2(0.5f, 1f);
        _victoryRect.sizeDelta = new Vector2(520, 34);
        _victoryRect.anchoredPosition = new Vector2(0, -64);
        _victoryRect.gameObject.AddComponent<Image>().color = new Color(0.5f, 0.12f, 0.12f, 0.85f);
        _victoryText = AddText(_victoryRect, "", TextAnchor.MiddleCenter);
        _victoryText.fontSize = 18;
        _victoryText.fontStyle = FontStyle.Bold;
        _victoryText.color = Prims.Hex(0xffe08a);
        AddOutline(_victoryText, 0.6f);
        _victoryRect.gameObject.SetActive(false);
    }

    /// <summary>Clickable "idle villager" pill, left of the age label. Hidden when none
    /// are idle; click (or the '.' hotkey) cycles to the next idle villager.</summary>
    void BuildIdleIndicator(RectTransform bar)
    {
        var rect = NewRect("IdleWorker", bar);
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(170, 32);
        rect.anchoredPosition = new Vector2(-300, 0);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.16f, 0.40f, 0.18f, 0.92f);

        _idleButton = rect.gameObject.AddComponent<Button>();
        _idleButton.targetGraphic = img;
        _idleButton.onClick.AddListener(() =>
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.selection != null) gm.selection.SelectNextIdleWorker();
        });

        _idleText = AddText(rect, "", TextAnchor.MiddleCenter);
        _idleText.fontSize = 16;
        _idleText.fontStyle = FontStyle.Bold;
        AddOutline(_idleText, 0.6f);
        rect.gameObject.SetActive(false);
    }

    void BuildCommandBar(Transform parent)
    {
        _cmdBar = NewRect("CommandBar", parent);
        _cmdBar.anchorMin = new Vector2(0, 0);
        _cmdBar.anchorMax = new Vector2(1, 0);
        _cmdBar.pivot     = new Vector2(0.5f, 0);
        _cmdBar.sizeDelta = new Vector2(0, BarH);
        _cmdBar.anchoredPosition = Vector2.zero;
        var barBg = _cmdBar.gameObject.AddComponent<Image>();
        barBg.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);   // flat fallback
        UiSkin.SkinPanel(barBg, UiSkin.BarBg, Color.white);   // wooden frame when kit present

        // Thin gold accent along the top edge gives the bar a crisp boundary.
        var topLine = NewRect("BarTopAccent", _cmdBar);
        topLine.anchorMin = new Vector2(0, 1); topLine.anchorMax = new Vector2(1, 1);
        topLine.pivot = new Vector2(0.5f, 1);
        topLine.sizeDelta = new Vector2(0, 2);
        topLine.anchoredPosition = Vector2.zero;
        topLine.gameObject.AddComponent<Image>().color = new Color(0.78f, 0.64f, 0.28f, 0.9f);

        // ── Info panel (2nd zone, right of the command grid; AoE-faithful order) ──
        var left = NewRect("InfoPanel", _cmdBar);
        left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1);
        left.pivot = new Vector2(0, 0.5f);
        left.sizeDelta = new Vector2(LeftW, -16);
        left.anchoredPosition = new Vector2(CmdZoneW, 0);
        var infoBg = left.gameObject.AddComponent<Image>();
        infoBg.color = new Color(0f, 0f, 0f, 0.35f);
        UiSkin.SkinPanel(infoBg, UiSkin.PanelInset, Color.white);

        // Vertical dividers marking the zone boundaries (grid | info | centre).
        void Divider(float xPos)
        {
            var d = NewRect("Sep", _cmdBar);
            d.anchorMin = new Vector2(0, 0); d.anchorMax = new Vector2(0, 1);
            d.pivot = new Vector2(0.5f, 0.5f);
            d.sizeDelta = new Vector2(2, -20);
            d.anchoredPosition = new Vector2(xPos, 0);
            d.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        }
        Divider(CmdZoneW);
        Divider(CmdZoneW + LeftW);

        var nameRect = NewRect("InfoName", left);
        nameRect.anchorMin = new Vector2(0, 1); nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1);
        nameRect.sizeDelta = new Vector2(-20, 32);
        nameRect.anchoredPosition = new Vector2(0, -10);
        _infoName = AddText(nameRect, "", TextAnchor.MiddleLeft);
        _infoName.fontSize = 20; _infoName.fontStyle = FontStyle.Bold;

        var subRect = NewRect("InfoSub", left);
        subRect.anchorMin = new Vector2(0, 1); subRect.anchorMax = new Vector2(1, 1);
        subRect.pivot = new Vector2(0.5f, 1);
        subRect.sizeDelta = new Vector2(-20, 22);
        subRect.anchoredPosition = new Vector2(0, -44);
        _infoSub = AddText(subRect, "", TextAnchor.MiddleLeft);
        _infoSub.fontSize = 14; _infoSub.color = new Color(0.78f, 0.82f, 0.88f, 1f);

        // HP bar (buildings)
        _hpBarBg = NewRect("HpBg", left);
        _hpBarBg.anchorMin = new Vector2(0, 1); _hpBarBg.anchorMax = new Vector2(1, 1);
        _hpBarBg.pivot = new Vector2(0.5f, 1);
        _hpBarBg.sizeDelta = new Vector2(-20, 18);
        _hpBarBg.anchoredPosition = new Vector2(0, -74);
        _hpBarBg.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 1f);

        _hpBarFill = NewRect("HpFill", _hpBarBg);
        _hpBarFill.anchorMin = new Vector2(0, 0); _hpBarFill.anchorMax = new Vector2(0, 1);
        _hpBarFill.pivot = new Vector2(0, 0.5f);
        _hpBarFill.sizeDelta = Vector2.zero;
        _hpBarFill.anchoredPosition = Vector2.zero;
        _hpBarFillImg = _hpBarFill.gameObject.AddComponent<Image>();
        _hpBarFillImg.color = Prims.Hex(0x4caf50);

        _hpText = AddText(_hpBarBg, "", TextAnchor.MiddleCenter);
        _hpText.fontSize = 12; _hpText.fontStyle = FontStyle.Bold;

        // Training / research progress lives in the info panel (below the HP bar)
        // so the command card can be a clean, fixed slot grid.
        var progBg = NewRect("ProgressBg", left);
        progBg.anchorMin = new Vector2(0, 1); progBg.anchorMax = new Vector2(1, 1);
        progBg.pivot = new Vector2(0.5f, 1);
        progBg.sizeDelta = new Vector2(-20, 12);
        progBg.anchoredPosition = new Vector2(0, -100);
        progBg.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

        _progressFill = NewRect("ProgressFill", progBg);
        _progressFill.anchorMin = new Vector2(0, 0); _progressFill.anchorMax = new Vector2(0, 1);
        _progressFill.pivot = new Vector2(0, 0.5f);
        _progressFill.sizeDelta = Vector2.zero;
        _progressFill.anchoredPosition = Vector2.zero;
        _progressFillImg = _progressFill.gameObject.AddComponent<Image>();
        _progressFillImg.color = Prims.Hex(0x4caf50);

        var queueRect = NewRect("QueueText", left);
        queueRect.anchorMin = new Vector2(0, 1); queueRect.anchorMax = new Vector2(1, 1);
        queueRect.pivot = new Vector2(0.5f, 1);
        queueRect.sizeDelta = new Vector2(-20, 18);
        queueRect.anchoredPosition = new Vector2(0, -116);
        _queueText = AddText(queueRect, "", TextAnchor.MiddleLeft);
        _queueText.fontSize = 13; _queueText.color = new Color(0.7f, 0.9f, 1f, 1f);

        // Training queue strip: small clickable unit icons (click to cancel + refund).
        _queueStrip = NewRect("QueueStrip", left);
        _queueStrip.anchorMin = new Vector2(0, 1); _queueStrip.anchorMax = new Vector2(1, 1);
        _queueStrip.pivot = new Vector2(0, 1);
        _queueStrip.sizeDelta = new Vector2(-16, 40);
        _queueStrip.anchoredPosition = new Vector2(10, -136);

        // ── Command card (1st zone, far left — AoE-faithful). A fixed Cols×Rows slot
        // grid; empty slots show a dark frame, commands fill the first N so the panel
        // always reads as deliberate. ──
        _cardRoot = NewRect("CommandCard", _cmdBar);
        _cardRoot.anchorMin = new Vector2(0, 0); _cardRoot.anchorMax = new Vector2(0, 1);
        _cardRoot.pivot = new Vector2(0, 0.5f);
        _cardRoot.sizeDelta = new Vector2(CmdZoneW, 0);
        _cardRoot.anchoredPosition = Vector2.zero;

        float gridW = Cols * BtnW + (Cols - 1) * Gap;
        float gridH = Rows * BtnH + (Rows - 1) * Gap;
        _gridRoot = NewRect("Grid", _cardRoot);
        _gridRoot.anchorMin = new Vector2(0, 0.5f); _gridRoot.anchorMax = new Vector2(0, 0.5f);
        _gridRoot.pivot = new Vector2(0, 0.5f);
        _gridRoot.sizeDelta = new Vector2(gridW, gridH);
        _gridRoot.anchoredPosition = new Vector2(14f, 0);

        for (int i = 0; i < Cols * Rows; i++)
        {
            int col = i % Cols, row = i / Cols;
            var slot = NewRect("Slot" + i, _gridRoot);
            slot.anchorMin = slot.anchorMax = new Vector2(0, 1);
            slot.pivot = new Vector2(0, 1);
            slot.sizeDelta = new Vector2(BtnW, BtnH);
            slot.anchoredPosition = new Vector2(col * (BtnW + Gap), -row * (BtnH + Gap));
            var slotImg = slot.gameObject.AddComponent<Image>();
            slotImg.color = new Color(0.11f, 0.12f, 0.14f, 0.65f);
            UiSkin.SkinPanel(slotImg, UiSkin.SlotFrame, new Color(1f, 1f, 1f, 0.45f));
        }

        // ── Centre emblem zone (3rd zone, decorative; stretches with screen width). ──
        var center = NewRect("CenterEmblem", _cmdBar);
        center.anchorMin = new Vector2(0, 0); center.anchorMax = new Vector2(1, 1);
        center.offsetMin = new Vector2(CmdZoneW + LeftW, 8f);
        center.offsetMax = new Vector2(-MinW, -8f);
        var titleRect = NewRect("GameTitle", center);
        titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(360, 60);
        titleRect.anchoredPosition = Vector2.zero;
        var titleTxt = AddText(titleRect, "AGE OF ARENA", TextAnchor.MiddleCenter);
        titleTxt.fontSize = 24; titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color = Prims.Hex(0xe8d4a0);
        AddOutline(titleTxt, 0.7f);

        // ── Minimap zone (4th zone, far right). MinimapSystem parents its rotated
        // diamond render here; kept empty (transparent) so the map fills it. ──
        _minimapZone = NewRect("MinimapZone", _cmdBar);
        _minimapZone.anchorMin = new Vector2(1, 0); _minimapZone.anchorMax = new Vector2(1, 1);
        _minimapZone.pivot = new Vector2(1, 0.5f);
        _minimapZone.sizeDelta = new Vector2(MinW, 0);
        _minimapZone.anchoredPosition = Vector2.zero;

        BuildTooltip();
        // Bar is persistent (always visible) — no SetActive toggle; idle state shows
        // empty slots + blank info, matching AoE2.
    }

    // ── Hover tooltip ──────────────────────────────────────────────────────────

    void BuildTooltip()
    {
        // Floats just above the command card; grows upward over the game view.
        _tooltip = NewRect("Tooltip", _cmdBar);
        _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0, 1);
        _tooltip.pivot = new Vector2(0, 0);
        _tooltip.sizeDelta = new Vector2(280, 72);
        _tooltip.anchoredPosition = new Vector2(LeftW + 14f, 8f);
        _tooltip.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.96f);

        var border = NewRect("TipBorder", _tooltip);
        border.anchorMin = new Vector2(0, 1); border.anchorMax = new Vector2(1, 1);
        border.pivot = new Vector2(0.5f, 1); border.sizeDelta = new Vector2(0, 2);
        border.anchoredPosition = Vector2.zero;
        border.gameObject.AddComponent<Image>().color = new Color(0.78f, 0.64f, 0.28f, 0.95f);

        var titleR = NewRect("TipTitle", _tooltip);
        titleR.anchorMin = new Vector2(0, 1); titleR.anchorMax = new Vector2(1, 1);
        titleR.pivot = new Vector2(0.5f, 1);
        titleR.sizeDelta = new Vector2(-16, 24);
        titleR.anchoredPosition = new Vector2(0, -8);
        _tipTitle = AddText(titleR, "", TextAnchor.UpperLeft);
        _tipTitle.fontSize = 15; _tipTitle.fontStyle = FontStyle.Bold;
        _tipTitle.color = Prims.Hex(0xf2d59b);

        var bodyR = NewRect("TipBody", _tooltip);
        bodyR.anchorMin = new Vector2(0, 0); bodyR.anchorMax = new Vector2(1, 1);
        bodyR.offsetMin = new Vector2(10, 8); bodyR.offsetMax = new Vector2(-10, -32);
        _tipBody = AddText(bodyR, "", TextAnchor.UpperLeft);
        _tipBody.fontSize = 12;
        _tipBody.color = new Color(0.82f, 0.86f, 0.92f, 1f);
        _tipBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tipBody.verticalOverflow = VerticalWrapMode.Truncate;

        _tooltip.gameObject.SetActive(false);
    }

    void AttachTooltip(GameObject button, string title, string cost, string desc)
    {
        var trig = button.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowTooltip(title, cost, desc));
        trig.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideTooltip());
        trig.triggers.Add(exit);
    }

    void ShowTooltip(string title, string cost, string desc)
    {
        if (_tooltip == null) return;
        _tipTitle.text = title;
        _tipBody.text  = string.IsNullOrEmpty(cost) ? desc : cost + "\n" + desc;
        _tooltip.gameObject.SetActive(true);
    }

    void HideTooltip()
    {
        if (_tooltip != null) _tooltip.gameObject.SetActive(false);
    }

    // ── Command button factory ─────────────────────────────────────────────

    CommandSlot MakeButton(int index, Color color, string title, string desc, string cost, string hotkey,
        System.Action<RectTransform> icon, System.Action onClick, System.Func<bool> affordable)
    {
        int col = index % Cols;
        int row = index / Cols;

        var rt = NewRect("Btn_" + title, _gridRoot);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(BtnW, BtnH);
        rt.anchoredPosition = new Vector2(col * (BtnW + Gap), -row * (BtnH + Gap));

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = color;
        UiSkin.Slice(bg, UiSkin.ButtonNormal);   // wooden button frame; keeps category tint

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.disabledColor = Color.white;   // keep our manual dim, not Unity's
        cb.fadeDuration = 0.05f;
        btn.colors = cb;
        btn.onClick.AddListener(() => { AudioManager.Play(AudioManager.SoundId.ButtonClick, 0.6f); onClick(); });

        // Procedural icon (AoE-style): fills the upper button face. Name moves to
        // the hover tooltip. Falls back to a wrapped title if no icon is provided.
        if (icon != null)
        {
            var iconRect = NewRect("Icon", rt);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(40, 40);
            iconRect.anchoredPosition = new Vector2(0, 6);
            icon(iconRect);
        }
        else
        {
            var titleRect = NewRect("Title", rt);
            titleRect.anchorMin = new Vector2(0, 0); titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(2, 12); titleRect.offsetMax = new Vector2(-2, -10);
            var tt = AddText(titleRect, title, TextAnchor.MiddleCenter);
            tt.fontSize = 13; tt.fontStyle = FontStyle.Bold;
            tt.horizontalOverflow = HorizontalWrapMode.Wrap;
            tt.verticalOverflow = VerticalWrapMode.Truncate;
            AddOutline(tt);
        }

        // Hover tooltip with the full name + cost + description.
        AttachTooltip(rt.gameObject, title, cost, desc);

        // Cost (bottom)
        if (!string.IsNullOrEmpty(cost))
        {
            var costRect = NewRect("Cost", rt);
            costRect.anchorMin = new Vector2(0, 0); costRect.anchorMax = new Vector2(1, 0);
            costRect.pivot = new Vector2(0.5f, 0);
            costRect.sizeDelta = new Vector2(0, 14);
            costRect.anchoredPosition = new Vector2(0, 2);
            var ct = AddText(costRect, cost, TextAnchor.MiddleCenter);
            ct.fontSize = 11; ct.color = new Color(1f, 0.95f, 0.7f, 1f);
            AddOutline(ct, 0.6f);
        }

        // Hotkey badge (top-left) — dark chip so the letter stays legible on any color.
        if (!string.IsNullOrEmpty(hotkey))
        {
            var hkRect = NewRect("Hotkey", rt);
            hkRect.anchorMin = new Vector2(0, 1); hkRect.anchorMax = new Vector2(0, 1);
            hkRect.pivot = new Vector2(0, 1);
            hkRect.sizeDelta = new Vector2(19, 17);
            hkRect.anchoredPosition = new Vector2(3, -3);
            hkRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            var ht = AddText(hkRect, hotkey, TextAnchor.MiddleCenter);
            ht.fontSize = 12; ht.fontStyle = FontStyle.Bold;
            ht.color = new Color(1f, 1f, 1f, 0.92f);
        }

        return new CommandSlot { btn = btn, bg = bg, baseColor = color, affordable = affordable };
    }

    // ── Per-frame update ───────────────────────────────────────────────────

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || _cmdBar == null) return;

        // Escape: toggle pause menu (resign / resume).
        if (Input.GetKeyDown(KeyCode.Escape) && !_gameOverShown)
        {
            if (_pauseMenu != null && _pauseMenu.activeSelf) ClosePauseMenu();
            else OpenPauseMenu(gm);
        }

        // Relics held readout in the top bar (replaces RelicSystem's IMGUI label).
        if (_relicText != null)
        {
            int total = gm.relics != null ? gm.relics.Count : 0;
            int mine  = total > 0 && gm.relicSystem != null ? gm.relicSystem.CountControlled(0) : 0;
            string rs = mine + "/" + total;
            if (_relicText.text != rs) _relicText.text = rs;
        }

        // Idle-villager pill: visible only when the player has idle workers.
        if (_idleButton != null && gm.selection != null)
        {
            int idle = gm.selection.IdleVillagerCount();
            if (_idleButton.gameObject.activeSelf != (idle > 0))
                _idleButton.gameObject.SetActive(idle > 0);
            if (idle > 0 && _idleText != null) _idleText.text = "Boşta köylü: " + idle + " (.)";
        }

        // Victory countdown banner: visible only while a Wonder/relic count is running.
        if (_victoryRect != null)
        {
            string vs = gm.match != null ? gm.match.VictoryStatus : "";
            bool showVictory = !string.IsNullOrEmpty(vs);
            if (_victoryRect.gameObject.activeSelf != showVictory) _victoryRect.gameObject.SetActive(showVictory);
            if (showVictory && _victoryText != null) _victoryText.text = vs;
        }

        var b = gm.selectedBuilding;
        var sel = gm.selection != null ? gm.selection.Selected : null;
        int unitCount = sel != null ? sel.Count : 0;
        bool hasVillager = false;
        if (sel != null)
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null && sel[i].type == UnitType.Villager) { hasVillager = true; break; }

        // Bar is persistent; rebuild the card only when the selection signature changes.
        int techVer = gm.teamTech != null ? gm.teamTech[0].Version : 0;
        if (b != _lastBld || unitCount != _lastUnitCount || hasVillager != _lastHasVillager
            || techVer != _lastTechVer)
        {
            _lastBld = b; _lastUnitCount = unitCount; _lastHasVillager = hasVillager;
            _lastTechVer = techVer; _lastQueueCount = -1;
            RebuildCard(gm, b, sel, unitCount, hasVillager);
        }

        UpdateSlotStates();
        UpdateInfoAndProgress(gm, b);
    }

    void UpdateSlotStates()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.btn == null) continue;
            bool ok = s.affordable == null || s.affordable();
            if (s.lastOk == ok) continue;
            s.lastOk = ok;
            s.btn.interactable = ok;
            s.bg.color = ok ? s.baseColor : Dim(s.baseColor);
        }
    }

    void UpdateInfoAndProgress(GameManager gm, BuildingEntity b)
    {
        // HP bar only for buildings.
        if (b != null && b.maxHp > 0f)
        {
            float frac = Mathf.Clamp01(b.hp / b.maxHp);
            float w = _hpBarBg.rect.width;
            _hpBarFill.sizeDelta = new Vector2(w * frac, 0);
            _hpText.text = Mathf.CeilToInt(b.hp) + " / " + Mathf.CeilToInt(b.maxHp);
        }

        // Live garrison count (updates as units enter/leave without reselecting).
        if (b != null && b.GarrisonCapacity > 0)
            _infoSub.text = $"Garnizon {b.GarrisonCount}/{b.GarrisonCapacity}";

        // Training / research progress (buildings only).
        float prog = -1f; bool isResearch = false; int qCount = 0;
        if (b != null)
        {
            prog   = gm.trainingQueue != null ? gm.trainingQueue.GetProgress(b) : -1f;
            qCount = gm.trainingQueue != null ? gm.trainingQueue.GetQueueCount(b) : 0;
            if (prog < 0f && gm.research != null)
            {
                prog = gm.research.GetProgress(b);
                isResearch = prog >= 0f;
            }
        }

        if (prog >= 0f)
        {
            float pw = (_progressFill.parent as RectTransform).rect.width;
            _progressFill.sizeDelta = new Vector2(pw * prog, 0);
            _progressFillImg.color  = isResearch ? Prims.Hex(0x4a90d9) : Prims.Hex(0x4caf50);
            SetQueueText(b, gm, isResearch, qCount);
        }
        else
        {
            _progressFill.sizeDelta = Vector2.zero;
            if (_lastQueueCount != 0) { _lastQueueCount = 0; _queueText.text = ""; }
        }

        UpdateQueueStrip(gm, b);
    }

    void SetQueueText(BuildingEntity b, GameManager gm, bool isResearch, int qCount)
    {
        if (isResearch)
        {
            string techName = TechDefs.Get(gm.research.GetActiveTech(b)).display;
            string line = "Araştırılıyor: " + techName;
            if (_queueText.text != line) _queueText.text = line;
        }
        else if (qCount != _lastQueueCount)
        {
            // Training queue is shown as the clickable icon strip below; the label
            // only carries a short "Üretim" header so the strip reads clearly.
            _lastQueueCount = qCount;
            _queueText.text = qCount > 0 ? "Üretim (iptal için tıkla):" : "";
        }
    }

    // ── Training queue strip ───────────────────────────────────────────────────

    void UpdateQueueStrip(GameManager gm, BuildingEntity b)
    {
        var view = (b != null && gm.trainingQueue != null) ? gm.trainingQueue.GetQueueView(b) : null;
        int n = view != null ? view.Count : 0;

        // Rebuild icons only when the queued unit list changes (not every frame).
        string sig = "";
        for (int i = 0; i < n; i++) sig += (int)view[i].type + ",";
        if (sig != _queueSig)
        {
            _queueSig = sig;
            RebuildQueueStrip(b, view);
        }

        // The front item's fill advances every frame.
        if (_queueFrontFill != null && n > 0)
            _queueFrontFill.rectTransform.sizeDelta = new Vector2(36f * Mathf.Clamp01(view[0].progress), 4f);
    }

    void RebuildQueueStrip(BuildingEntity b, List<(UnitType type, float progress)> view)
    {
        for (int i = 0; i < _queueIcons.Count; i++)
            if (_queueIcons[i] != null) Destroy(_queueIcons[i]);
        _queueIcons.Clear();
        _queueFrontFill = null;
        if (view == null || view.Count == 0) return;

        const float size = 38f, gap = 4f;
        for (int i = 0; i < view.Count; i++)
        {
            int index = i;
            var bb = b;
            var rt = NewRect("Q" + i, _queueStrip);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(i * (size + gap), 0);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                var g = GameManager.Instance;
                if (g != null && g.trainingQueue != null && bb != null) g.trainingQueue.Cancel(bb, index);
            });

            var iconRect = NewRect("Icon", rt);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(30, 30);
            iconRect.anchoredPosition = new Vector2(0, 2);
            CommandIconFactory.Unit(iconRect, view[i].type);

            // The front (currently-training) item carries a thin progress fill.
            if (i == 0)
            {
                var fill = NewRect("QFill", rt);
                fill.anchorMin = fill.anchorMax = new Vector2(0, 0);
                fill.pivot = new Vector2(0, 0);
                fill.sizeDelta = new Vector2(0, 4);
                fill.anchoredPosition = new Vector2(1, 1);
                _queueFrontFill = fill.gameObject.AddComponent<Image>();
                _queueFrontFill.color = Prims.Hex(0x4caf50);
            }

            _queueIcons.Add(rt.gameObject);
        }
    }

    // ── Card content ─────────────────────────────────────────────────────────

    void RebuildCard(GameManager gm, BuildingEntity b, List<UnitEntity> sel,
        int unitCount, bool hasVillager)
    {
        // Clear previous slots.
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].btn != null) Destroy(_slots[i].btn.gameObject);
        _slots.Clear();
        HideTooltip();              // a hovered button may have just been destroyed
        _lastQueueCount = -1;
        _queueText.text = "";
        _progressFill.sizeDelta = Vector2.zero;

        if (b != null)             BuildBuildingCard(gm, b);
        else if (hasVillager)      BuildVillagerCard(gm, sel, unitCount);
        else if (unitCount > 0)    BuildUnitInfo(sel, unitCount);
        else { _infoName.text = ""; _infoSub.text = ""; ShowHpBar(false); }  // nothing selected → idle bar
    }

    void BuildBuildingCard(GameManager gm, BuildingEntity b)
    {
        _infoName.text = BuildingTr(b.type) + (b.underConstruction ? "  (inşa)" : "");
        _infoSub.text  = b.GarrisonCapacity > 0 ? $"Garnizon {b.GarrisonCount}/{b.GarrisonCapacity}" : "";
        ShowHpBar(true);

        int idx = 0;

        // Train buttons
        if (b.type == BuildingType.Market && !b.underConstruction)
        {
            BuildMarketButtons(gm, ref idx);
            return;
        }

        foreach (var tr in b.GetTrainables())
        {
            var def = tr;
            var bb = b;
            _slots.Add(MakeButton(idx++, TrainCol, UnitTr(def.unitType), UnitDesc(def.unitType),
                CostLine(def.food, def.wood, def.gold, 0), def.hotkey,
                r => CommandIconFactory.Unit(r, def.unitType),
                () => { var g = GameManager.Instance; if (g != null && bb != null) g.trainingQueue.Enqueue(bb, def); },
                () => {
                    var g = GameManager.Instance; if (g == null) return false;
                    var rm = g.resources;
                    return rm.pop < rm.popCap && rm.CanAfford(def.food, def.wood, def.gold, 0);
                }));
        }

        // Research / age-advance buttons
        var techs = b.GetResearchables();
        for (int i = 0; i < techs.Count && idx < Cols * 2; i++)
        {
            var d = techs[i];
            var bb = b;
            int hk = i + 1;
            bool isAge = d.type == TechType.FeudalAge || d.type == TechType.CastleAge
                      || d.type == TechType.ImperialAge;
            _slots.Add(MakeButton(idx++, isAge ? AgeCol : UpgCol, d.display, TechDesc(d.type),
                CostLine(d.food, d.wood, d.gold, d.stone), hk.ToString(),
                r => CommandIconFactory.Tech(r, d.type),
                () => { var g = GameManager.Instance; if (g != null && bb != null) g.research.Enqueue(bb, d); },
                () => {
                    var g = GameManager.Instance; if (g == null) return false;
                    return !g.research.IsResearching(bb) && g.resources.CanAfford(d.food, d.wood, d.gold, d.stone);
                }));
        }

        // Ungarrison: shown for any garrison-capable building, enabled while it shelters units.
        if (b.GarrisonCapacity > 0 && !b.underConstruction)
        {
            var bb = b;
            _slots.Add(MakeButton(idx++, GarrisonCol, "Boşalt", "İçerideki birimleri dışarı çıkar (U).",
                "", "U",
                r => CommandIconFactory.Command(r, CommandIconFactory.CmdIcon.Garrison),
                () => { var g = GameManager.Instance; if (g != null && bb != null) g.garrison?.UngarrisonAll(bb); },
                () => bb != null && bb.GarrisonCount > 0));
        }
    }

    void BuildMarketButtons(GameManager gm, ref int idx)
    {
        int batch = MarketSystem.Batch;
        AddMarket(ref idx, ResourceKind.Food,  "Yiyecek Sat",  batch + "Y → " + MarketSystem.SellGold(ResourceKind.Food)  + "A", "1");
        AddMarket(ref idx, ResourceKind.Wood,  "Odun Sat",     batch + "O → " + MarketSystem.SellGold(ResourceKind.Wood)  + "A", "2");
        AddMarket(ref idx, ResourceKind.Stone, "Taş Sat",      batch + "T → " + MarketSystem.SellGold(ResourceKind.Stone) + "A", "3");
        int bc = MarketSystem.BuyCost(ResourceKind.Food);
        _slots.Add(MakeButton(idx++, MarketCol, "Yiyecek Al", "Altın vererek yiyecek satın al.",
            bc + "A → " + batch + "Y", "4",
            r => CommandIconFactory.Market(r, ResourceKind.Food, true),
            () => { var rm = GameManager.Instance?.resources; if (rm != null) MarketSystem.Buy(rm, ResourceKind.Food); },
            () => { var rm = GameManager.Instance?.resources; return rm != null && rm.gold >= MarketSystem.BuyCost(ResourceKind.Food); }));
    }

    void AddMarket(ref int idx, ResourceKind kind, string title, string cost, string hk)
    {
        _slots.Add(MakeButton(idx++, MarketCol, title, "Bu kaynağı altına çevir.", cost, hk,
            r => CommandIconFactory.Market(r, kind, false),
            () => { var rm = GameManager.Instance?.resources; if (rm != null) MarketSystem.Sell(rm, kind); },
            () => { var rm = GameManager.Instance?.resources; return rm != null && rm.Get(kind) >= MarketSystem.Batch; }));
    }

    void BuildVillagerCard(GameManager gm, List<UnitEntity> sel, int unitCount)
    {
        _infoName.text = unitCount + " Köylü";
        _infoSub.text  = "Bina yapmak için seç";
        ShowHpBar(false);

        int idx = 0;
        foreach (var d in BuildingDefs.Buildable())
        {
            if (!BuildingDefs.UnlockedAt(d.type, gm.tech.age)) continue; // age-locked
            var def = d;
            _slots.Add(MakeButton(idx++, BuildCol, BuildingTr(def.type), BuildingDesc(def.type),
                CostLine(def.food, def.wood, def.gold, def.stone), def.hotkey.ToString(),
                r => CommandIconFactory.Building(r, def.type),
                () => { var g = GameManager.Instance; if (g != null && g.placement != null) g.placement.Begin(def.type); },
                () => {
                    var g = GameManager.Instance; if (g == null) return false;
                    return g.resources.CanAfford(def.food, def.wood, def.gold, def.stone);
                }));
        }

        AddUnitCommands(ref idx, includeAttackMove: false);
    }

    void BuildUnitInfo(List<UnitEntity> sel, int unitCount)
    {
        // Non-villager units: count + dominant type, plus generic command buttons.
        UnitType first = UnitType.Villager;
        bool homogeneous = true;
        for (int i = 0; i < sel.Count; i++)
        {
            if (sel[i] == null) continue;
            if (i == 0) { first = sel[i].type; continue; }
            if (sel[i].type != first) { homogeneous = false; break; }
        }
        _infoName.text = homogeneous ? unitCount + " " + UnitTr(first, GameManager.Instance?.tech) : unitCount + " birim";
        _infoSub.text  = "";
        ShowHpBar(false);

        int idx = 0;
        AddUnitCommands(ref idx, includeAttackMove: true);
    }

    /// <summary>Stop (always) and Attack-move (optional) command buttons, shared by the
    /// unit-info and villager cards.</summary>
    void AddUnitCommands(ref int idx, bool includeAttackMove)
    {
        _slots.Add(MakeButton(idx++, CmdCol, "Dur", "Tüm emirleri bırak ve dur.", "", "S",
            r => CommandIconFactory.Command(r, CommandIconFactory.CmdIcon.Stop),
            () =>
            {
                var s = GameManager.Instance?.selection?.Selected;
                if (s == null) return;
                for (int i = 0; i < s.Count; i++) { var u = s[i]; if (u != null) { u.attackMove = false; u.Stop(); } }
            },
            () => true));

        if (includeAttackMove)
        {
            _slots.Add(MakeButton(idx++, CmdCol, "Saldır-Yürü", "Bir noktaya ilerle; yoldaki düşmana saldır.", "", "A",
                r => CommandIconFactory.Command(r, CommandIconFactory.CmdIcon.AttackMove),
                () => GameManager.Instance?.command?.BeginAttackMove(),
                () => true));

            _slots.Add(MakeButton(idx++, CmdCol, "Duruş", "Saldırı duruşunu değiştir (Agresif/Savunma/Sabit/Pasif).", "", "Q",
                r => CommandIconFactory.Command(r, CommandIconFactory.CmdIcon.Stop),
                () =>
                {
                    var s = GameManager.Instance?.selection?.Selected;
                    if (s == null || s.Count == 0) return;
                    var first = s[0];
                    if (first == null) return;
                    var next = (AttackStance)(((int)first.stance + 1) % 4);
                    for (int i = 0; i < s.Count; i++) { var u = s[i]; if (u != null) u.stance = next; }
                },
                () => true));
        }
    }

    void ShowHpBar(bool on)
    {
        if (_hpBarBg != null) _hpBarBg.gameObject.SetActive(on);
    }

    // ── Top bar refresh + game over (unchanged) ───────────────────────────────

    Text AddEntry(RectTransform bar, ref float x, Color swatch, string label)
    {
        var s = NewRect(label + "Swatch", bar);
        s.anchorMin = s.anchorMax = s.pivot = new Vector2(0, 0.5f);
        s.sizeDelta = new Vector2(24, 24);
        s.anchoredPosition = new Vector2(x, 0);
        var swImg = s.gameObject.AddComponent<Image>();
        swImg.color = swatch;
        // AoE feel: when the kit is present, nest the coloured chip inside an inset frame.
        if (UiSkin.Available)
        {
            UiSkin.SkinPanel(swImg, UiSkin.SlotFrame, Color.white);
            var chip = NewRect(label + "Chip", s);
            chip.anchorMin = new Vector2(0, 0); chip.anchorMax = new Vector2(1, 1);
            chip.offsetMin = new Vector2(5, 5); chip.offsetMax = new Vector2(-5, -5);
            chip.gameObject.AddComponent<Image>().color = swatch;
        }
        x += 32f;

        var v = NewRect(label + "Text", bar);
        v.anchorMin = v.anchorMax = v.pivot = new Vector2(0, 0.5f);
        v.sizeDelta = new Vector2(70, 30);
        v.anchoredPosition = new Vector2(x, 0);
        var t = AddText(v, "0", TextAnchor.MiddleLeft);
        t.fontStyle = FontStyle.Bold;
        AddOutline(t, 0.6f);
        x += 88f;
        return t;
    }

    Text AddText(RectTransform parent, string content, TextAnchor anchor)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var t = go.AddComponent<Text>();
        t.font = _font;
        t.fontSize = 20;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = content;
        return t;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    void Refresh()
    {
        if (_res == null) return;
        if (_foodText)  _foodText.text  = _res.food.ToString();
        if (_woodText)  _woodText.text  = _res.wood.ToString();
        if (_goldText)  _goldText.text  = _res.gold.ToString();
        if (_stoneText) _stoneText.text = _res.stone.ToString();
        if (_popText)
        {
            bool full = _res.popCap > 0 && _res.pop >= _res.popCap;
            _popText.text  = full ? _res.pop + "/" + _res.popCap + " DOLU" : _res.pop + "/" + _res.popCap;
            _popText.color = full ? Prims.Hex(0xff5555) : Color.white;
        }
    }

    /// <summary>Full-screen victory/defeat overlay shown by <see cref="MatchSystem"/>.</summary>
    void OpenPauseMenu(GameManager gm)
    {
        if (_canvasRoot == null) return;
        Time.timeScale = 0f;
        if (_pauseMenu != null) { _pauseMenu.SetActive(true); return; }

        var overlay = new GameObject("PauseMenu");
        overlay.transform.SetParent(_canvasRoot, false);
        _pauseMenu = overlay;
        var rt = overlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        void AddBtn(string label, System.Action onClick, float yOffset)
        {
            var br = NewRect(label, overlay.transform);
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(280, 50);
            br.anchoredPosition = new Vector2(0, yOffset);
            var img = br.gameObject.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            var btn = br.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var txt = AddText(br, label, TextAnchor.MiddleCenter);
            txt.fontSize = 22; txt.fontStyle = FontStyle.Bold;
        }

        AddBtn("Devam", () => ClosePauseMenu(), 40f);
        AddBtn("Teslim Ol", () => { ClosePauseMenu(); gm.match?.Resign(); }, -20f);
        AddBtn("Yeniden Başlat", () => { Time.timeScale = 1f; GameBootstrap.Restart(); }, -80f);
    }

    void ClosePauseMenu()
    {
        if (_pauseMenu != null) _pauseMenu.SetActive(false);
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    public void ShowGameOver(bool playerWon, string subtitle = "")
    {
        if (_gameOverShown || _canvasRoot == null) return;
        _gameOverShown = true;

        var overlay = NewRect("GameOverOverlay", _canvasRoot);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        var titleRect = NewRect("GameOverTitle", overlay);
        titleRect.anchorMin = new Vector2(0, 0.5f); titleRect.anchorMax = new Vector2(1, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(0, 120);
        titleRect.anchoredPosition = new Vector2(0, 30);
        var title = AddText(titleRect, playerWon ? "ZAFER!" : "YENILDIN", TextAnchor.MiddleCenter);
        title.fontSize = 80;
        title.fontStyle = FontStyle.Bold;
        title.color = playerWon ? Prims.Hex(0x4caf50) : Prims.Hex(0xff5555);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subRect = NewRect("GameOverSub", overlay);
            subRect.anchorMin = new Vector2(0, 0.5f); subRect.anchorMax = new Vector2(1, 0.5f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(0, 40);
            subRect.anchoredPosition = new Vector2(0, -50);
            var sub = AddText(subRect, subtitle, TextAnchor.MiddleCenter);
            sub.fontSize = 26;
            sub.color = Prims.Hex(0xf2d59b);
        }

        var hintRect = NewRect("GameOverHint", overlay);
        hintRect.anchorMin = new Vector2(0, 0.5f); hintRect.anchorMax = new Vector2(1, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(0, 40);
        hintRect.anchoredPosition = new Vector2(0, -100);
        var hint = AddText(hintRect, "Yeniden baslatmak icin R", TextAnchor.MiddleCenter);
        hint.fontSize = 28;
        hint.color = new Color(0.85f, 0.85f, 0.85f, 1f);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    // Disabled tint: fade toward a dark slate while keeping a faint hint of the
    // category hue. A flat ×0.32 crushed teal/blue almost to black; lerping reads
    // clearly as "greyed out" without losing the colour cue.
    static readonly Color DisabledBase = new Color(0.17f, 0.18f, 0.21f, 1f);
    static Color Dim(Color c) => Color.Lerp(c, DisabledBase, 0.72f);

    /// <summary>Black outline for legibility of label text over coloured buttons.</summary>
    static void AddOutline(Text t, float alpha = 0.7f)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, alpha);
        o.effectDistance = new Vector2(1f, -1f);
    }

    static string AgeName(Age a) => a switch
    {
        Age.Dark   => "Karanlık",
        Age.Feudal => "Derebeylik",
        Age.Castle => "Kale",
        Age.Imperial => "İmparatorluk",
        _          => "",
    };

    static string UnitTr(UnitType t) => t switch
    {
        UnitType.Villager    => "Köylü",
        UnitType.Militia     => "Asker",
        UnitType.Archer      => "Okçu",
        UnitType.Cavalry     => "Süvari",
        UnitType.Trebuchet   => "Mancınık",
        UnitType.Scout       => "Gözcü",
        UnitType.Medic       => "Şifacı",
        UnitType.Spearman    => "Mızrakçı",
        UnitType.Longbowman  => "Uzun Yaylı",
        UnitType.Galley      => "Gemi",
        UnitType.Skirmisher  => "Avcı",
        UnitType.Camel       => "Deveci",
        UnitType.Ram         => "Koçbaşı",
        UnitType.Mangonel    => "Mancınık Arabası",
        UnitType.CavalryArcher => "Atlı Okçu",
        UnitType.FireShip    => "Ateş Gemisi",
        UnitType.DemoShip    => "Patlayıcı Gemi",
        // M9 unique units
        UnitType.TeutonicKnight => "Töton Şövalyesi",
        UnitType.WarElephant => "Savaş Fili",
        UnitType.Mangudai    => "Mangudai",
        UnitType.Samurai     => "Samuray",
        UnitType.Eagle       => "Kartal Savaşçı",
        _                    => t.ToString(),
    };

    /// <summary>Tech-aware unit name: a unit shows its highest researched tier's
    /// title (e.g. Militia → "Piyade" → "Uzun Kılıç"). Falls back to the base name.</summary>
    static string UnitTr(UnitType t, TechState tech)
    {
        if (tech != null)
        {
            switch (t)
            {
                case UnitType.Militia:
                    if (tech.Has(TechType.Champion))      return "Şampiyon";
                    if (tech.Has(TechType.TwoHandedSwordsman)) return "İki Elli Kılıç";
                    if (tech.Has(TechType.Longswordsman)) return "Uzun Kılıç";
                    if (tech.Has(TechType.ManAtArms))     return "Piyade";
                    break;
                case UnitType.Archer:
                    if (tech.Has(TechType.Arbalest))      return "Arbalet";
                    if (tech.Has(TechType.Crossbowman))   return "Arbaletçi";
                    break;
                case UnitType.Scout:
                    if (tech.Has(TechType.Hussar))        return "Hüsar";
                    if (tech.Has(TechType.LightCavalry))  return "Hafif Süvari";
                    break;
                case UnitType.CavalryArcher:
                    if (tech.Has(TechType.HeavyCavalryArcher)) return "Ağır Atlı Okçu";
                    break;
                case UnitType.Galley:
                    if (tech.Has(TechType.Galleon))       return "Kalyon";
                    if (tech.Has(TechType.WarGalley))     return "Savaş Kadırgası";
                    break;
                case UnitType.Cavalry:
                    if (tech.Has(TechType.Paladin))       return "Paladin";
                    if (tech.Has(TechType.Cavalier))      return "Ağır Süvari";
                    break;
                case UnitType.Spearman:
                    if (tech.Has(TechType.Halberdier))    return "Teberli";
                    if (tech.Has(TechType.Pikeman))       return "Kargıcı";
                    break;
                case UnitType.Skirmisher:
                    if (tech.Has(TechType.EliteSkirmisher)) return "Seçkin Avcı";
                    break;
                case UnitType.Camel:
                    if (tech.Has(TechType.HeavyCamel))    return "Ağır Deve";
                    break;
                case UnitType.Eagle:
                    if (tech.Has(TechType.EliteEagle))    return "Seçkin Kartal Savaşçı";
                    break;
            }
        }
        return UnitTr(t);
    }

    static string BuildingTr(BuildingType t) => t switch
    {
        BuildingType.TownCenter   => "Şehir Merkezi",
        BuildingType.House        => "Ev",
        BuildingType.Barracks     => "Kışla",
        BuildingType.ArcheryRange => "Okçu Menzili",
        BuildingType.Stable       => "Ahır",
        BuildingType.Farm         => "Tarla",
        BuildingType.LumberCamp   => "Oduncu Kampı",
        BuildingType.MiningCamp   => "Madenci Kampı",
        BuildingType.Mill         => "Değirmen",
        BuildingType.Market       => "Pazar",
        BuildingType.Castle       => "Kale",
        BuildingType.Wall         => "Duvar",
        BuildingType.Gate         => "Kapı",
        BuildingType.Dock         => "Liman",
        BuildingType.SiegeWorkshop => "Kuşatma Atölyesi",
        BuildingType.Outpost      => "Gözcü Kulesi",
        BuildingType.BombardTower => "Bombard Kulesi",
        _                         => t.ToString(),
    };

    // ── Tooltip descriptions ──────────────────────────────────────────────────

    static string UnitDesc(UnitType t) => t switch
    {
        UnitType.Villager    => "İnşa eder ve kaynak toplar.",
        UnitType.Militia     => "Yakın dövüş piyadesi. Dengeli ve ucuz.",
        UnitType.Archer      => "Menzilli ok atar. Yakın dövüşe zayıf.",
        UnitType.Cavalry     => "Hızlı süvari. İlk vuruşta atılım bonusu.",
        UnitType.Trebuchet   => "Kuşatma silahı. Binalara karşı çok güçlü.",
        UnitType.Scout       => "Hızlı kâşif. Geniş görüş, hasarsız.",
        UnitType.Medic       => "Yakındaki dost birimleri iyileştirir.",
        UnitType.Spearman    => "Süvariye karşı uzman. Mızraklı piyade.",
        UnitType.Longbowman  => "Britanyalılar özgün birimi. Çok uzun menzil, delik zırh.",
        UnitType.Galley      => "Su birimi. Göldeki düşman gemilerine saldırır.",
        _                    => "",
    };

    static string BuildingDesc(BuildingType t) => t switch
    {
        BuildingType.TownCenter   => "Köylü üretir, çağ atlar. Üssün kalbi.",
        BuildingType.House        => "Nüfus tavanını +5 artırır.",
        BuildingType.Barracks     => "Asker ve gözcü eğitir.",
        BuildingType.ArcheryRange => "Okçu eğitir (Derebeylik).",
        BuildingType.Stable       => "Süvari eğitir (Kale çağı).",
        BuildingType.Farm         => "Yenilenebilir yiyecek kaynağı.",
        BuildingType.LumberCamp   => "Odun bırakma noktası.",
        BuildingType.MiningCamp   => "Altın/taş bırakma noktası.",
        BuildingType.Mill         => "Yiyecek bırakma noktası.",
        BuildingType.Market       => "Kaynak alıp satar.",
        BuildingType.Castle       => "Güçlü savunma kulesi; mancınık/şifacı eğitir.",
        BuildingType.Wall         => "Geçişi engelleyen sur. Ucuz ve dayanıklı.",
        BuildingType.Gate         => "Birimlerin geçebildiği kapı.",
        BuildingType.Dock         => "Gemi üretir. Su kenarına inşa et (150 odun).",
        BuildingType.SiegeWorkshop => "Koçbaşı + mancınık üretir (Kale Çağı, 200 odun).",
        BuildingType.Outpost      => "Ucuz gözcü kulesi. Ateş etmez (25 odun + 5 taş).",
        BuildingType.BombardTower => "Top kulesi: Siege hasarı, uzun menzil (İmparatorluk, 125 odun + 100 taş).",
        _                         => "",
    };

    static string TechDesc(TechType t) => t switch
    {
        TechType.FeudalAge     => "Derebeylik Çağı'na geç. Yeni bina/birim açar.",
        TechType.CastleAge     => "Kale Çağı'na geç. Gelişmiş birimleri açar.",
        TechType.ImperialAge   => "İmparatorluk Çağı'na geç. En güçlü yükseltmeleri açar.",
        TechType.Forging       => "Yakın dövüş saldırısı +.",
        TechType.Fletching     => "Okçu saldırısı ve menzili +.",
        TechType.Bodkin        => "Okçu saldırısı +.",
        TechType.ScaleMail     => "Asker/süvari canı +.",
        TechType.Bloodlines    => "Süvari canı +.",
        TechType.ManAtArms     => "Asker yükseltmesi: Piyade.",
        TechType.Longswordsman => "Asker yükseltmesi: Uzun Kılıç.",
        TechType.Crossbowman   => "Okçu yükseltmesi: Arbaletçi.",
        TechType.Cavalier      => "Süvari yükseltmesi: Ağır Süvari.",
        TechType.DoubleBitAxe  => "Odun toplama hızı +.",
        TechType.Wheelbarrow   => "Köylü taşıma kapasitesi ve hızı +.",
        // ── M6 Blacksmith ──
        TechType.IronCasting   => "Yakın dövüş saldırısı + (asker & süvari).",
        TechType.BlastFurnace  => "Yakın dövüş saldırısı ++ (asker & süvari).",
        TechType.ChainMail     => "Piyade zırhı + (yakın & delici).",
        TechType.PlateMail     => "Piyade zırhı ++ (yakın & delici).",
        TechType.ScaleBarding  => "Süvari zırhı +.",
        TechType.ChainBarding  => "Süvari zırhı +.",
        TechType.PlateBarding  => "Süvari zırhı ++.",
        TechType.PaddedArcherArmor  => "Okçu zırhı +.",
        TechType.LeatherArcherArmor => "Okçu zırhı +.",
        TechType.RingArcherArmor    => "Okçu zırhı ++.",
        TechType.Bracer        => "Okçu saldırısı ve menzili +.",
        // ── M6 Ekonomi ──
        TechType.Loom          => "Köylü canı ve zırhı +.",
        TechType.BowSaw        => "Odun toplama hızı +.",
        TechType.GoldMining    => "Altın toplama hızı +.",
        TechType.StoneMining   => "Taş toplama hızı +.",
        TechType.CropRotation  => "Tarla yiyecek kapasitesi +.",
        TechType.Husbandry     => "Süvari hareket hızı +.",
        TechType.Caravan       => "Ticaret arabası geliri +.",
        // ── M6 Üniversite ──
        TechType.Ballistics    => "Mermiler hareketli hedefleri daha iyi vurur.",
        TechType.Chemistry     => "Tüm menzilli saldırılar +1.",
        TechType.Architecture  => "Bina canı ve zırhı +.",
        // ── M7 Manastır ──
        TechType.Sanctity      => "Keşiş canı +.",
        TechType.BlockPrinting => "Keşiş dönüştürme menzili +.",
        TechType.Redemption    => "Keşişler bina/kuşatma dönüştürebilir.",
        TechType.Theocracy     => "Dönüştürmede yalnız bir keşiş inanç harcar.",
        // ── M8 Pazar ──
        TechType.Coinage       => "Haraç vergisiz gönderilir.",
        TechType.Banking       => "Ticaret arabası geliri +.",
        TechType.Guilds        => "Pazar alış-satış farkı daralır.",
        TechType.EliteEagle    => "Kartal Savaşçı → Seçkin: can ve saldırı +.",
        // ── M9 civ-özel unique tech ──
        TechType.Chivalry      => "Frank: süvari canı +20.",
        TechType.BeardedAxe    => "Frank: piyade saldırısı +2.",
        TechType.Ironclad      => "Töton: kuşatma birimi zırhı +4.",
        TechType.Crenellations => "Töton: kule menzili +.",
        _                      => "",
    };

    /// <summary>Compact cost string, e.g. "60O 20A" (Y=food, O=wood, A=gold, T=stone).</summary>
    static string CostLine(int f, int w, int g, int s)
    {
        var parts = new List<string>();
        if (f > 0) parts.Add(f + "Y");
        if (w > 0) parts.Add(w + "O");
        if (g > 0) parts.Add(g + "A");
        if (s > 0) parts.Add(s + "T");
        return string.Join(" ", parts);
    }
}
