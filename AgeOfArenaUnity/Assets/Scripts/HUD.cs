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
    CanvasScaler _canvasScaler;   // N9.a11y: UI scale slider drives referenceResolution
    bool _gameOverShown;
    /// <summary>N9.postgame test helper: reset game-over flag so overlay can be shown again.</summary>
    public void ResetGameOver() { _gameOverShown = false; }
    /// <summary>Diagnostic: returns whether the canvas root is initialised.</summary>
    public bool HasCanvasRoot => _canvasRoot != null;
    GameObject _pauseMenu;
    float _prePauseTimeScale = 1f;
    // N9.hotkeys: remap panel state
    GameObject _hotkeyPanel;
    HotkeyAction? _listeningAction;          // action awaiting its next key press
    readonly System.Collections.Generic.Dictionary<HotkeyAction, Text> _hotkeyKeyLabels = new();

    // Top bar
    Text _foodText, _woodText, _goldText, _stoneText, _popText, _ageText, _relicText;
    Button _idleButton; Text _idleText;
    Text _victoryText; RectTransform _victoryRect;
    Text _subtitleText; float _subtitleTimer; // N6.form: short transient notification
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
    readonly List<int> _queueSigTypes = new();   // cached queued-type signature (no per-frame string alloc)

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
    ResourceNode _lastNode;
    int _lastUnitCount = -1;
    bool _lastHasVillager;
    int _lastTechVer = -1;
    int _lastQueueCount = -1;

    const int   Cols         = 5;
    const int   Rows         = 3;            // fixed AoE-style slot grid (Cols×Rows)
    const int   SlotsPerPage = Cols * Rows;  // CMDP: 15 slots per page
    const float BtnW   = 60f, BtnH = 60f, Gap = 6f;
    int         _cmdPage;                    // CMDP: current command card page (0-based)
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
    static readonly Color DeniedCol = Prims.Hex(0x6a2f35);

    public void Init(ResourceManager res)
    {
        _res = res;
        _font = ResolveFont();
        BuildCanvas();
        Refresh();
        if (_ageText != null) _ageText.text = Loc.Get("hud.age") + ": " + AgeName(Age.Dark);
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
        _ageText.text = Loc.Get("hud.age") + ": " + AgeName(newAge);
        // AGFX: play age-up sound and show popup for player only.
        AudioManager.Play(AudioManager.SoundId.AgeUp, 1.0f);
        AudioManager.PlayMusicForAge(newAge); // N7.music
        MetaSystem.OnAgeAdvanced(team, newAge); // N13.meta
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
        _canvasScaler = canvasGo.AddComponent<CanvasScaler>();
        var scaler = _canvasScaler;
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
        _foodText  = AddEntry(bar, ref x, Prims.Hex(0xd64545), Loc.Get("res.food"));
        _woodText  = AddEntry(bar, ref x, Prims.Hex(0x8a5a2b), Loc.Get("res.wood"));
        _goldText  = AddEntry(bar, ref x, Prims.Hex(0xf2c14e), Loc.Get("res.gold"));
        _stoneText = AddEntry(bar, ref x, Prims.Hex(0xb9b9b9), Loc.Get("res.stone"));
        _popText   = AddEntry(bar, ref x, Prims.Hex(0x6fa8dc), Loc.Get("res.pop"));
        _relicText = AddEntry(bar, ref x, Prims.Hex(0xe0b84b), Loc.Get("res.relic"));
        _relicText.rectTransform.sizeDelta = new Vector2(230, 30);
        _relicText.fontSize = 16;
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
        BuildSpeedIndicator(bar);
        BuildVictoryBanner(parent);
    }

    /// <summary>Clickable difficulty pill (top bar). Cycles all 6 difficulty levels and
    /// re-applies to every live <see cref="EnemyAI"/> immediately (AIDF).</summary>
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
        // AIDF: 6 levels — modulo wraps Easy→Moderate→Normal→Hard→Insane→Extreme→Easy.
        gm.difficulty = (Difficulty)(((int)gm.difficulty + 1) % 6);
        var ais = EnemyAI.All;   // cached registry — no FindObjectsByType scene scan
        for (int i = 0; i < ais.Count; i++) if (ais[i] != null) ais[i].SetDifficulty();
        UpdateDifficultyLabel();
    }

    void UpdateDifficultyLabel()
    {
        if (_difficultyText == null) return;
        var gm = GameManager.Instance;
        _difficultyText.text = Loc.Get("hud.difficulty") + ": " + (gm != null ? DiffName(gm.difficulty) : "");
    }

    static string DiffName(Difficulty d) => d switch
    {
        Difficulty.Easy     => Loc.Get("diff.easy"),
        Difficulty.Moderate => Loc.Get("diff.moderate"),
        Difficulty.Normal   => Loc.Get("diff.normal"),
        Difficulty.Hard     => Loc.Get("diff.hard"),
        Difficulty.Insane   => Loc.Get("diff.insane"),
        Difficulty.Extreme  => Loc.Get("diff.extreme"),
        _                   => "",
    };

    // ── Game speed indicator ────────────────────────────────────────────────────

    Text _speedText;
    static readonly float[] SpeedLevels = { 0f, 0.5f, 1f, 2f, 3f };
    int _speedIdx = 2; // default 1×

    void BuildSpeedIndicator(RectTransform bar)
    {
        var rect = NewRect("Speed", bar);
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(80, 32);
        // Sits just left of the Civ pill (which spans -835..-665). The old -668 put this
        // 80px pill entirely inside the Civ pill, so their labels overlapped and the right
        // ~80px of the Civ button was unclickable.
        rect.anchoredPosition = new Vector2(-845, 0);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.30f, 0.92f);
        UiSkin.SkinPanel(img, UiSkin.PillNormal, Color.white);
        var btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(CycleSpeed);
        _speedText = AddText(rect, SpeedLabel(), TextAnchor.MiddleCenter);
        _speedText.fontSize = 14;
        _speedText.fontStyle = FontStyle.Bold;
        AddOutline(_speedText, 0.6f);
    }

    void CycleSpeed()
    {
        if (_gameOverShown) return;
        _speedIdx = (_speedIdx + 1) % SpeedLevels.Length;
        Time.timeScale = SpeedLevels[_speedIdx];
        if (_speedText != null) _speedText.text = SpeedLabel();
        AudioManager.Play(AudioManager.SoundId.ButtonClick, 0.4f);
    }

    string SpeedLabel() => SpeedLevels[_speedIdx] switch
    {
        0f    => "II Dur",
        0.5f  => "► ½×",
        1f    => "► 1×",
        2f    => "►► 2×",
        3f    => "███ 3×",
        _     => "► 1×",
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
        _civText.text = Loc.Get("hud.civ") + ": " + CivilizationDefs.Get(gm.playerCiv).display;
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

        // N6.form: small subtitle bar just below victory banner (formation name, etc.)
        var subRect = NewRect("SubtitleBar", parent);
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot     = new Vector2(0.5f, 1f);
        subRect.sizeDelta = new Vector2(320, 26);
        subRect.anchoredPosition = new Vector2(0, -102);
        subRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        _subtitleText = AddText(subRect, "", TextAnchor.MiddleCenter);
        _subtitleText.fontSize = 15;
        _subtitleText.color = Color.white;
        subRect.gameObject.SetActive(false);
        _subtitleText.transform.parent.gameObject.SetActive(false);
    }

    /// <summary>N6.form: show a brief text notification (formation name, Town Bell, etc.).</summary>
    public void ShowSubtitle(string msg, float duration = 1.8f)
    {
        if (_subtitleText == null) return;
        _subtitleText.text = msg;
        _subtitleText.transform.parent.gameObject.SetActive(true);
        _subtitleTimer = duration;
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
        => AttachTooltip(button, title, () => string.IsNullOrEmpty(cost) ? desc : cost + "\n" + desc);

    void AttachTooltip(GameObject button, string title, System.Func<string> body)
    {
        var trig = button.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowTooltip(title, body != null ? body() : ""));
        trig.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideTooltip());
        trig.triggers.Add(exit);
    }

    void ShowTooltip(string title, string body)
    {
        if (_tooltip == null) return;
        _tipTitle.text = title;
        _tipBody.text  = body;
        _tooltip.gameObject.SetActive(true);
    }

    void HideTooltip()
    {
        if (_tooltip != null) _tooltip.gameObject.SetActive(false);
    }

    // ── Command button factory ─────────────────────────────────────────────

    CommandSlot MakeButton(int index, Color color, string title, string desc, string cost, string hotkey,
        System.Action<RectTransform> icon, System.Action onClick, System.Func<bool> affordable,
        System.Func<string> tooltipBody = null)
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
        if (tooltipBody != null) AttachTooltip(rt.gameObject, title, tooltipBody);
        else AttachTooltip(rt.gameObject, title, cost, desc);

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

        // N6.form: tick subtitle auto-hide timer.
        if (_subtitleTimer > 0f)
        {
            _subtitleTimer -= Time.unscaledDeltaTime;
            if (_subtitleTimer <= 0f && _subtitleText != null)
                _subtitleText.transform.parent.gameObject.SetActive(false);
        }

        // N13.meta: achievement toast via subtitle.
        if (MetaSystem.TryTakeAchievement(out var ach))
            ShowSubtitle("🏆 " + MetaSystem.DisplayName(ach) + " rozeti kazanıldı!", 3.5f);

        // N9.hotkeys: while a remap row is listening, capture the key here and consume
        // all input this frame so the pressed key doesn't also fire a game action.
        if (_listeningAction.HasValue) { PollHotkeyRebind(); return; }

        // Escape: toggle pause menu (resign / resume).
        if (Input.GetKeyDown(KeyCode.Escape) && !_gameOverShown)
        {
            if (_pauseMenu != null && _pauseMenu.activeSelf) ClosePauseMenu();
            else OpenPauseMenu(gm);
        }

        // Sync speed label with Time.timeScale (CommandSystem [ ] / Space may change it externally).
        if (_speedText != null)
        {
            float ts = Time.timeScale;
            for (int i = 0; i < SpeedLevels.Length; i++)
                if (Mathf.Abs(SpeedLevels[i] - ts) < 0.1f) { _speedIdx = i; break; }
            _speedText.text = SpeedLabel();
        }

        // DIPL: hotkey toggles diplomacy panel.
        if (Hotkeys.Down(HotkeyAction.Diplomacy) && !_gameOverShown)
            ToggleDiplomacyPanel();
        UpdateTributeInfo(gm);

        // STIC: hotkey cycles attack stance for all selected units.
        if (Hotkeys.Down(HotkeyAction.Stance))
        {
            var stanceSel = gm.selection?.Selected;
            if (stanceSel != null && stanceSel.Count > 0 && stanceSel[0] != null)
            {
                var next = (AttackStance)(((int)stanceSel[0].stance + 1) % 4);
                for (int i = 0; i < stanceSel.Count; i++)
                    if (stanceSel[i] != null) stanceSel[i].stance = next;
            }
        }

        // Relics held readout in the top bar (replaces RelicSystem's IMGUI label).
        if (_relicText != null)
        {
            string rs = RelicHudLine(gm.relics, 0);
            if (_relicText.text != rs) _relicText.text = rs;
        }

        // Idle-villager pill: visible only when the player has idle workers.
        if (_idleButton != null && gm.selection != null)
        {
            int idle = gm.selection.IdleVillagerCount();
            if (_idleButton.gameObject.activeSelf != (idle > 0))
                _idleButton.gameObject.SetActive(idle > 0);
            if (idle > 0 && _idleText != null) _idleText.text = Loc.Get("hud.idleWorker") + ": " + idle + " (.)";
        }

        // Victory / time-limit banner — ONE priority-ordered show/hide. An imminent
        // Wonder/relic/KotH countdown (VictoryStatus) takes priority; otherwise the
        // time-limit countdown shows. Previously the time-limit branch hid VictoryStatus
        // entirely in timed matches AND never turned its own banner off (no else).
        if (_victoryRect != null && _victoryText != null)
        {
            string banner = null;
            string vs = gm.match != null ? gm.match.VictoryStatus : "";
            if (!string.IsNullOrEmpty(vs))
            {
                banner = vs;
            }
            else if (gm.match != null && gm.match.MatchTimeLimit > 0f)
            {
                float rem = gm.match.TimeRemaining;
                if (rem < float.MaxValue)
                {
                    int mins = (int)(rem / 60f), secs = (int)(rem % 60f);
                    banner = $"{Loc.Get("hud.time")}: {mins:00}:{secs:00}";
                }
            }

            bool show = banner != null;
            if (_victoryRect.gameObject.activeSelf != show) _victoryRect.gameObject.SetActive(show);
            if (show && _victoryText.text != banner) _victoryText.text = banner;
        }

        var b = gm.selectedBuilding;
        var node = gm.selectedNode;
        var sel = gm.selection != null ? gm.selection.Selected : null;
        int unitCount = sel != null ? sel.Count : 0;
        bool hasVillager = false;
        if (sel != null)
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null && sel[i].type == UnitType.Villager) { hasVillager = true; break; }

        // Bar is persistent; rebuild the card only when the selection signature changes.
        int techVer = (gm.teamTech != null && gm.teamTech.Length > 0) ? gm.teamTech[0].Version : 0;
        if (b != _lastBld || node != _lastNode || unitCount != _lastUnitCount || hasVillager != _lastHasVillager
            || techVer != _lastTechVer)
        {
            _lastBld = b; _lastNode = node; _lastUnitCount = unitCount; _lastHasVillager = hasVillager;
            _lastTechVer = techVer; _lastQueueCount = -1;
            _cmdPage = 0;  // CMDP: reset page on any selection change
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
            _infoSub.text = BuildingStatusLine(gm, b);
        else if (b != null && b.type == BuildingType.Market)
            _infoSub.text = MarketSpreadLine();

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

        // Live resource node amount update (decreases while gatherers work).
        var liveNode = gm.selectedNode;
        if (liveNode != null && _infoSub != null)
        {
            string nodeAmt = ResourceNodeInfoLine(liveNode, NodeOwnerTech(liveNode));
            if (_infoSub.text != nodeAmt) _infoSub.text = nodeAmt;
        }

        // STIC: show stance for unit selections.
        if (b == null && liveNode == null && _infoSub != null)
        {
            var sel2 = gm.selection?.Selected;
            if (sel2 != null && sel2.Count > 0 && sel2[0] != null)
            {
                string stanceName = sel2[0].stance switch
                {
                    AttackStance.Aggressive  => Loc.Get("stance.aggressive"),
                    AttackStance.Defensive   => Loc.Get("stance.defensive"),
                    AttackStance.StandGround => Loc.Get("stance.standground"),
                    AttackStance.NoAttack    => Loc.Get("stance.noattack"),
                    _                        => "",
                };
                string unitLine = UnitInfoSub(sel2, sel2[0].type);
                string stanceLine = stanceName.Length > 0 ? string.Format(Loc.Get("misc.stanceFmt"), stanceName) : "";
                string line = string.IsNullOrEmpty(unitLine) ? stanceLine : unitLine + " | " + stanceLine;
                if (_infoSub.text != line) _infoSub.text = line;
            }
        }

        UpdateQueueStrip(gm, b);
    }

    void SetQueueText(BuildingEntity b, GameManager gm, bool isResearch, int qCount)
    {
        if (isResearch)
        {
            string techName = TechDefs.Get(gm.research.GetActiveTech(b)).display;
            string line = Loc.Get("hud.researching") + ": " + techName;
            if (_queueText.text != line) _queueText.text = line;
        }
        else if (qCount != _lastQueueCount)
        {
            // Training queue is shown as the clickable icon strip below; the label
            // only carries a short "Üretim" header so the strip reads clearly.
            _lastQueueCount = qCount;
            _queueText.text = qCount > 0 ? Loc.Get("hud.producing") : "";
        }
    }

    // ── Training queue strip ───────────────────────────────────────────────────

    void UpdateQueueStrip(GameManager gm, BuildingEntity b)
    {
        var tq = (b != null) ? gm.trainingQueue : null;
        int n = tq != null ? tq.GetQueueCount(b) : 0;

        // Detect a change without allocating (the old code built a List<tuple> via
        // GetQueueView + a string sig every frame): compare queued types to a cached list.
        bool changed = n != _queueSigTypes.Count;
        if (!changed)
            for (int i = 0; i < n; i++)
                if (_queueSigTypes[i] != (int)tq.GetQueuedType(b, i)) { changed = true; break; }
        if (changed)
        {
            _queueSigTypes.Clear();
            for (int i = 0; i < n; i++) _queueSigTypes.Add((int)tq.GetQueuedType(b, i));
            RebuildQueueStrip(b, tq, n);
        }

        // The front item's fill advances every frame.
        if (_queueFrontFill != null && n > 0)
            _queueFrontFill.rectTransform.sizeDelta = new Vector2(36f * Mathf.Clamp01(tq.GetProgress(b)), 4f);
    }

    void RebuildQueueStrip(BuildingEntity b, TrainingQueue tq, int n)
    {
        for (int i = 0; i < _queueIcons.Count; i++)
            if (_queueIcons[i] != null) Destroy(_queueIcons[i]);
        _queueIcons.Clear();
        _queueFrontFill = null;
        if (tq == null || n == 0) return;

        const float size = 38f, gap = 4f;
        for (int i = 0; i < n; i++)
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
            CommandIconFactory.Unit(iconRect, tq.GetQueuedType(b, i));

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

        var node = gm.selectedNode;
        if (b != null)             BuildBuildingCard(gm, b);
        else if (node != null)     BuildNodeInfo(node);
        else if (hasVillager)      BuildVillagerCard(gm, sel, unitCount);
        else if (unitCount > 0)    BuildUnitInfo(sel, unitCount);
        else { _infoName.text = ""; _infoSub.text = ""; ShowHpBar(false); }  // nothing selected → idle bar

        // CMDP: rebuild page nav after slots are populated.
        BuildPageNav();
    }

    // CMDP: page navigation arrows — shown only when total slots > SlotsPerPage.
    void BuildPageNav()
    {
        if (_gridRoot == null) return;
        // Remove previous nav buttons.
        var old = _gridRoot.Find("PageNav");
        if (old != null) Destroy(old.gameObject);

        int total = _slots.Count;
        int pages  = Mathf.CeilToInt(total / (float)SlotsPerPage);
        if (pages <= 1) return;

        var nav = new GameObject("PageNav");
        nav.transform.SetParent(_gridRoot, false);
        var navRt = nav.AddComponent<RectTransform>();
        float gridH = Rows * (BtnH + Gap);
        navRt.anchorMin = navRt.anchorMax = new Vector2(0, 1);
        navRt.pivot = new Vector2(0, 1);
        navRt.sizeDelta = new Vector2(Cols * (BtnW + Gap), 20f);
        navRt.anchoredPosition = new Vector2(0, -(gridH + 4));

        // "←" and "→" buttons
        for (int side = 0; side < 2; side++)
        {
            int s = side;
            var go = new GameObject(s == 0 ? "Prev" : "Next");
            go.transform.SetParent(nav.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(28, 18);
            rt.anchoredPosition = new Vector2(s == 0 ? 2 : Cols * (BtnW + Gap) - 30, -1);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            var img = go.AddComponent<Image>(); img.color = new Color(0.3f, 0.3f, 0.4f, 0.9f);
            var btn = go.AddComponent<Button>();
            var lrt = NewRect("L", rt); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            AddText(lrt, s == 0 ? "◄" : "►", TextAnchor.MiddleCenter).fontSize = 11;
            btn.onClick.AddListener(() =>
            {
                _cmdPage = Mathf.Clamp(_cmdPage + (s == 0 ? -1 : 1), 0, pages - 1);
                // Refresh slot visibility.
                RefreshSlotVisibility();
                BuildPageNav();
            });
        }
        // Page counter
        var crt = NewRect("Count", navRt);
        crt.anchorMin = new Vector2(0.3f, 0); crt.anchorMax = new Vector2(0.7f, 1);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        AddText(crt, $"{_cmdPage + 1}/{pages}", TextAnchor.MiddleCenter).fontSize = 11;

        RefreshSlotVisibility();
    }

    void RefreshSlotVisibility()
    {
        int start = _cmdPage * SlotsPerPage;
        for (int i = 0; i < _slots.Count; i++)
        {
            bool visible = i >= start && i < start + SlotsPerPage;
            if (_slots[i].btn != null) _slots[i].btn.gameObject.SetActive(visible);
            // Reposition within the visible grid cell.
            if (visible && _slots[i].btn != null)
            {
                int pageIdx = i - start;
                var rt = _slots[i].btn.GetComponent<RectTransform>();
                if (rt != null)
                {
                    int col = pageIdx % Cols, row = pageIdx / Cols;
                    rt.anchoredPosition = new Vector2(col * (BtnW + Gap), -row * (BtnH + Gap));
                }
            }
        }
    }

    void BuildBuildingCard(GameManager gm, BuildingEntity b)
    {
        _infoName.text = BuildingTr(b.type) + (b.underConstruction ? "  (inşa)" : "");
        _infoSub.text  = BuildingStatusLine(gm, b);
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
        bool ageButtonAdded = false;
        for (int i = 0; i < techs.Count && idx < Cols * 2; i++)
        {
            var d = techs[i];
            if (IsAgeTech(d.type))
            {
                AddAgeResearchButton(gm, b, d, ref idx);
                ageButtonAdded = true;
                continue;
            }
            var bb = b;
            int hk = i + 1;
            bool monasteryTech = b.type == BuildingType.Monastery;
            _slots.Add(MakeButton(idx++, UpgCol, d.display, TechDesc(d.type),
                CostLine(d.food, d.wood, d.gold, d.stone), hk.ToString(),
                r => CommandIconFactory.Tech(r, d.type),
                () => { var g = GameManager.Instance; if (g != null && bb != null) g.research.Enqueue(bb, d); },
                () => {
                    var g = GameManager.Instance; if (g == null) return false;
                    return !g.research.IsResearching(bb) && g.resources.CanAfford(d.food, d.wood, d.gold, d.stone);
                },
                monasteryTech ? () => MonasteryTechTooltip(gm, d) : null));
        }
        AddCivDeniedTechButtons(gm, b, ref idx);

        if (!ageButtonAdded && TryGetNextAgeDef(gm, b, out var nextAge))
            AddAgeResearchButton(gm, b, nextAge, ref idx);

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
        _infoSub.text = MarketSpreadLine();
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

    void BuildNodeInfo(ResourceNode node)
    {
        ShowHpBar(false);
        string kindName = node.kind switch
        {
            ResourceKind.Food  => "Yiyecek",
            ResourceKind.Wood  => "Ahşap",
            ResourceKind.Gold  => "Altın",
            ResourceKind.Stone => "Taş",
            _                  => node.kind.ToString(),
        };
        _infoName.text = kindName;
        _infoSub.text = ResourceNodeInfoLine(node, NodeOwnerTech(node));
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
        _infoSub.text  = UnitInfoSub(sel, homogeneous ? first : UnitType.Villager);
        ShowHpBar(false);

        int idx = 0;
        AddUnitCommands(ref idx, includeAttackMove: true);
    }

    /// <summary>Stop (always) and Attack-move (optional) command buttons, shared by the
    /// unit-info and villager cards.</summary>
    void AddUnitCommands(ref int idx, bool includeAttackMove)
    {
        _slots.Add(MakeButton(idx++, CmdCol, "Dur", "Tüm emirleri bırak ve dur.", "", KeyLabel(Hotkeys.Get(HotkeyAction.Stop)),
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
            _slots.Add(MakeButton(idx++, CmdCol, "Saldır-Yürü", "Bir noktaya ilerle; yoldaki düşmana saldır.", "", KeyLabel(Hotkeys.Get(HotkeyAction.AttackMove)),
                r => CommandIconFactory.Command(r, CommandIconFactory.CmdIcon.AttackMove),
                () => GameManager.Instance?.command?.BeginAttackMove(),
                () => true));

            _slots.Add(MakeButton(idx++, CmdCol, "Devriye", "Seçili birimler iki nokta arasında devriye gezer.", "", KeyLabel(Hotkeys.Get(HotkeyAction.Patrol)),
                r => CommandIconFactory.Command(r, CommandIconFactory.CmdIcon.Patrol),
                () => GameManager.Instance?.command?.BeginPatrol(),
                () => true));

            _slots.Add(MakeButton(idx++, CmdCol, "Duruş", "Saldırı duruşunu değiştir (Agresif/Savunma/Sabit/Pasif).", "", KeyLabel(Hotkeys.Get(HotkeyAction.Stance)),
                r => CommandIconFactory.Command(r, CommandIconFactory.CmdIcon.Stance),
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
        if (Time.timeScale > 0.01f) _prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (_pauseMenu != null) { _pauseMenu.SetActive(true); return; }

        var overlay = new GameObject("PauseMenu");
        overlay.transform.SetParent(_canvasRoot, false);
        _pauseMenu = overlay;
        var rt = overlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        void AddBtn(string label, System.Action onClick, float yOffset, float xOffset = 0f)
        {
            var br = NewRect(label, overlay.transform);
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(xOffset != 0f ? 120 : 280, 50);
            br.anchoredPosition = new Vector2(xOffset, yOffset);
            var img = br.gameObject.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            var btn = br.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var txt = AddText(br, label, TextAnchor.MiddleCenter);
            txt.fontSize = 22; txt.fontStyle = FontStyle.Bold;
        }

        // Auto-stacking y-cursor: every full-width button gets its own 54px slot so they can
        // never overlap. (The old hand-numbered offsets collided — resign/-50 over Editör/-30,
        // restart/-110 over Kampanya/-90, fog/-170 over Replay/-150 — leaving several buttons
        // partially covered and unreliable to click.)
        float y = 320f;
        const float step = 54f;

        AddBtn(Loc.Get("pause.resume"),  () => ClosePauseMenu(), y); y -= step;
        AddBtn("Teknoloji Ağacı", () => { ClosePauseMenu(); OpenTechTreePanel(gm); }, y); y -= step;
        AddBtn(Loc.Get("pause.hotkeys"), () => OpenHotkeyPanel(), y); y -= step;   // N9.hotkeys: remap UI
        // N12.edit: open scenario editor
        AddBtn("Editör", () => { ClosePauseMenu(); gm.scenarioEditor?.Open(); }, y); y -= step;
        // N13.camp: open campaign screen
        AddBtn("Kampanya", () => { ClosePauseMenu(); gm.campaignScreen?.Show(); }, y); y -= step;
        // MP-3: open online lobby
        AddBtn("Cok Oyunculu", () => { ClosePauseMenu(); gm.lobbyScreen?.Show(); }, y); y -= step;
        // N15.checksum: save replay snapshot + trigger verify run
        AddBtn("Replay", () =>
        {
            if (gm.checksum == null) return;
            var result = gm.checksum.ReplayVerifyResult;
            if (result != null) { ShowSubtitle($"Sonuç: {result}", 5f); return; }
            ClosePauseMenu();
            gm.checksum.StartReplayVerify();
        }, y); y -= step;
        AddBtn(Loc.Get("pause.resign"),  () => { ClosePauseMenu(); gm.match?.Resign(); }, y); y -= step;
        AddBtn(Loc.Get("pause.restart"), () => { Time.timeScale = 1f; GameBootstrap.Restart(); }, y); y -= step;
        // FOWD: fog toggle in pause menu
        AddBtn(gm.fow != null && gm.fow.fogEnabled ? Loc.Get("pause.fogOff") : Loc.Get("pause.fogOn"),
            () => { if (gm.fow != null) { gm.fow.fogEnabled = !gm.fow.fogEnabled; ClosePauseMenu(); } }, y); y -= step;
        // N9.a11y: colorblind palette toggle
        AddBtn(AccessibilitySettings.ColorblindMode ? "Renk Std" : "Renk KB",
            () => { AccessibilitySettings.SetColorblindMode(!AccessibilitySettings.ColorblindMode); ClosePauseMenu(); }, y); y -= step;
        // N9.a11y: UI scale +/-
        AddBtn("UI +", () => { AccessibilitySettings.SetUiScale(AccessibilitySettings.UiScale + 0.1f); ApplyUiScale(); }, y, 60f);
        AddBtn("UI -", () => { AccessibilitySettings.SetUiScale(AccessibilitySettings.UiScale - 0.1f); ApplyUiScale(); }, y, -60f); y -= step;
        // N7.spatial: volume buttons
        AddBtn("Vol +", () => AudioManager.MasterVolume = Mathf.Clamp01(AudioManager.MasterVolume + 0.1f), y, 60f);
        AddBtn("Vol -", () => AudioManager.MasterVolume = Mathf.Clamp01(AudioManager.MasterVolume - 0.1f), y, -60f); y -= step;
        // N7.music: music volume buttons
        AddBtn("Müzik +", () => AudioManager.MusicVolume = Mathf.Clamp01(AudioManager.MusicVolume + 0.1f), y, 60f);
        AddBtn("Müzik -", () => AudioManager.MusicVolume = Mathf.Clamp01(AudioManager.MusicVolume - 0.1f), y, -60f); y -= step;
    }

    void ApplyUiScale()
    {
        if (_canvasScaler == null) return;
        float s = 1f / AccessibilitySettings.UiScale;
        _canvasScaler.referenceResolution = new Vector2(1920f * s, 1080f * s);
    }

    // N13.meta: tech-tree viewer panel
    GameObject _techTreePanel;

    void OpenTechTreePanel(GameManager gm)
    {
        if (_canvasRoot == null) return;
        if (_techTreePanel != null) { _techTreePanel.SetActive(true); return; }

        // Full-screen semi-transparent overlay
        var overlay = NewRect("TechTreePanel", _canvasRoot);
        _techTreePanel = overlay.gameObject;
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero;
        var ovImg = overlay.gameObject.AddComponent<UnityEngine.UI.Image>();
        ovImg.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
        overlay.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Title
        var titleR = NewRect("Title", overlay);
        titleR.anchorMin = new Vector2(0.5f, 1f); titleR.anchorMax = new Vector2(0.5f, 1f);
        titleR.pivot = new Vector2(0.5f, 1f);
        titleR.sizeDelta = new Vector2(700, 50);
        titleR.anchoredPosition = new Vector2(0, -20);
        var title = AddText(titleR, "TEKNOLOJİ AĞACI", TextAnchor.MiddleCenter);
        title.fontSize = 30; title.fontStyle = FontStyle.Bold;
        title.color = new Color(0.95f, 0.82f, 0.42f);

        // Close button
        var closeR = NewRect("CloseBtn", overlay);
        closeR.anchorMin = new Vector2(1f, 1f); closeR.anchorMax = new Vector2(1f, 1f);
        closeR.pivot = new Vector2(1f, 1f);
        closeR.sizeDelta = new Vector2(100, 40);
        closeR.anchoredPosition = new Vector2(-20, -15);
        var closeImg = closeR.gameObject.AddComponent<UnityEngine.UI.Image>();
        closeImg.color = new Color(0.5f, 0.1f, 0.1f);
        var closeBtn = closeR.gameObject.AddComponent<UnityEngine.UI.Button>();
        closeBtn.onClick.AddListener(() => _techTreePanel.SetActive(false));
        AddText(closeR, "✕ Kapat", TextAnchor.MiddleCenter).color = Color.white;

        // Achievement count
        int achCount = MetaSystem.UnlockedCount();
        int achTotal = System.Enum.GetValues(typeof(MetaSystem.Achievement)).Length;
        var achR = NewRect("AchCount", overlay);
        achR.anchorMin = new Vector2(0f, 1f); achR.anchorMax = new Vector2(0f, 1f);
        achR.pivot = new Vector2(0f, 1f);
        achR.sizeDelta = new Vector2(300, 36);
        achR.anchoredPosition = new Vector2(20, -15);
        AddText(achR, $"🏆 Başarımlar: {achCount}/{achTotal}", TextAnchor.MiddleLeft).color = new Color(0.9f, 0.82f, 0.42f);

        // Daily challenge
        var ch = MetaSystem.TodayChallenge();
        var chalR = NewRect("DailyChallenge", overlay);
        chalR.anchorMin = new Vector2(0f, 1f); chalR.anchorMax = new Vector2(0f, 1f);
        chalR.pivot = new Vector2(0f, 1f);
        chalR.sizeDelta = new Vector2(600, 32);
        chalR.anchoredPosition = new Vector2(20, -55);
        AddText(chalR, "📅 Günün Görevi: " + MetaSystem.ChallengeName(ch), TextAnchor.MiddleLeft).color = new Color(0.7f, 0.9f, 0.7f);

        // Tech list (scrollable region)
        var civ   = gm?.playerCiv ?? Civilization.None;
        var tech  = gm?.teamTech?[0];
        var techs = MetaSystem.GetTechList(civ, tech);

        float rowH = 32f, startY = -90f, x = 50f;
        int perCol = 20;
        for (int i = 0; i < techs.Count; i++)
        {
            var (def, researched, locked) = techs[i];
            int col = i / perCol;
            int row = i % perCol;
            float px = x + col * 450f;
            float py = startY - row * rowH;

            var rowR = NewRect("Tech_" + i, overlay);
            rowR.anchorMin = new Vector2(0f, 1f); rowR.anchorMax = new Vector2(0f, 1f);
            rowR.pivot = new Vector2(0f, 1f);
            rowR.sizeDelta = new Vector2(440f, rowH - 4f);
            rowR.anchoredPosition = new Vector2(px, py);

            var rowImg = rowR.gameObject.AddComponent<UnityEngine.UI.Image>();
            rowImg.color = researched ? new Color(0.1f, 0.25f, 0.12f, 0.8f)
                         : locked     ? new Color(0.2f, 0.08f, 0.08f, 0.6f)
                         :              new Color(0.12f, 0.16f, 0.22f, 0.7f);

            string status = researched ? "✓" : locked ? "✕" : "○";
            string costStr = def.gold > 0 ? $"{def.gold}A" : def.wood > 0 ? $"{def.wood}O" : $"{def.food}Y";
            string label = $"{status}  {def.display}  ({costStr})";

            var txt = AddText(rowR, label, TextAnchor.MiddleLeft);
            txt.fontSize = 16;
            txt.color = researched ? new Color(0.5f, 1f, 0.55f)
                      : locked     ? new Color(0.6f, 0.35f, 0.35f)
                      :              new Color(0.85f, 0.88f, 0.95f);

            AttachTooltip(rowR.gameObject, def.display, () => TechTreeTooltip(gm, def, researched, locked));
        }
    }

    void ClosePauseMenu()
    {
        if (_pauseMenu != null) _pauseMenu.SetActive(false);
        if (_hotkeyPanel != null) _hotkeyPanel.SetActive(false);
        _listeningAction = null;
        if (Time.timeScale == 0f) Time.timeScale = Mathf.Max(0.01f, _prePauseTimeScale);
    }

    // ── N9.hotkeys: rebindable-key settings panel ────────────────────────────
    // Only actions with a real consumer are remappable. Garrison is right-click,
    // BuildMenu has no handler, and Repair is contextual right-click.
    static readonly (HotkeyAction action, string label)[] RemapRows =
    {
        (HotkeyAction.Stop,        "Durdur"),
        (HotkeyAction.AttackMove,  "Saldır-Yürü"),
        (HotkeyAction.Patrol,      "Devriye"),
        (HotkeyAction.Stance,      "Duruş Değiştir"),
        (HotkeyAction.Formation,   "Formasyon"),
        (HotkeyAction.TownBell,    "Kule Çanı"),
        (HotkeyAction.Ungarrison,  "Garnizon Boşalt"),
        (HotkeyAction.Diplomacy,   "Diplomasi"),
        (HotkeyAction.SelectIdle,  "Boşta İşçi Seç"),
    };

    void OpenHotkeyPanel()
    {
        if (_canvasRoot == null) return;
        if (Time.timeScale > 0.01f) _prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (_pauseMenu != null) _pauseMenu.SetActive(false);
        if (_hotkeyPanel != null) { _hotkeyPanel.SetActive(true); RefreshHotkeyLabels(); return; }

        var overlay = new GameObject("HotkeyPanel");
        overlay.transform.SetParent(_canvasRoot, false);
        _hotkeyPanel = overlay;
        var ort = overlay.AddComponent<RectTransform>();
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var titleRect = NewRect("HotkeyTitle", overlay.transform);
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(520, 44);
        titleRect.anchoredPosition = new Vector2(0, 250);
        var title = AddText(titleRect, "TUŞ ATAMALARI", TextAnchor.MiddleCenter);
        title.fontSize = 28; title.fontStyle = FontStyle.Bold; title.color = Prims.Hex(0xf2d59b);

        float y = 190f;
        foreach (var (action, label) in RemapRows)
        {
            HotkeyAction act = action;   // capture
            var row = NewRect("Row_" + action, overlay.transform);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(460, 38);
            row.anchoredPosition = new Vector2(0, y);

            var nameRect = NewRect("Name", row);
            nameRect.anchorMin = new Vector2(0, 0); nameRect.anchorMax = new Vector2(0.6f, 1);
            nameRect.offsetMin = new Vector2(8, 0); nameRect.offsetMax = Vector2.zero;
            var nameTxt = AddText(nameRect, label, TextAnchor.MiddleLeft);
            nameTxt.fontSize = 20;

            var keyRect = NewRect("Key", row);
            keyRect.anchorMin = new Vector2(0.62f, 0); keyRect.anchorMax = new Vector2(1, 1);
            keyRect.offsetMin = keyRect.offsetMax = Vector2.zero;
            var keyImg = keyRect.gameObject.AddComponent<Image>();
            keyImg.color = new Color(0.12f, 0.13f, 0.2f, 0.95f);
            var keyBtn = keyRect.gameObject.AddComponent<Button>();
            keyBtn.targetGraphic = keyImg;
            var keyTxt = AddText(keyRect, KeyLabel(Hotkeys.Get(act)), TextAnchor.MiddleCenter);
            keyTxt.fontSize = 20; keyTxt.fontStyle = FontStyle.Bold;
            _hotkeyKeyLabels[act] = keyTxt;
            keyBtn.onClick.AddListener(() => { _listeningAction = act; RefreshHotkeyLabels(); });

            y -= 42f;
        }

        void Btn(string lbl, System.Action onClick, float xoff)
        {
            var br = NewRect("HkBtn_" + lbl, overlay.transform);
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(210, 46);
            br.anchoredPosition = new Vector2(xoff, -230);
            var img = br.gameObject.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            var b = br.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var t = AddText(br, lbl, TextAnchor.MiddleCenter);
            t.fontSize = 20; t.fontStyle = FontStyle.Bold;
        }
        Btn(Loc.Get("hk.resetAll"), () => { Hotkeys.ResetAll(); _listeningAction = null; RefreshHotkeyLabels(); }, -115f);
        Btn(Loc.Get("hk.back"), () => { _listeningAction = null; _hotkeyPanel.SetActive(false); if (_pauseMenu != null) _pauseMenu.SetActive(true); }, 115f);
    }

    /// <summary>Display string for a key (None → "—", listening row → "...").</summary>
    static string KeyLabel(KeyCode k) => k == KeyCode.None ? "—" : k.ToString();

    void RefreshHotkeyLabels()
    {
        foreach (var kv in _hotkeyKeyLabels)
        {
            if (kv.Value == null) continue;
            if (_listeningAction.HasValue && _listeningAction.Value == kv.Key)
            { kv.Value.text = "<bekleniyor>"; kv.Value.color = Prims.Hex(0xffd34a); }
            else
            { kv.Value.text = KeyLabel(Hotkeys.Get(kv.Key)); kv.Value.color = Color.white; }
        }
    }

    /// <summary>Called from Update: while a row is listening, capture the next key press
    /// and rebind (evicting any conflicting action). Esc cancels the capture.</summary>
    void PollHotkeyRebind()
    {
        if (!_listeningAction.HasValue) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { _listeningAction = null; RefreshHotkeyLabels(); return; }
        foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.None || kc == KeyCode.Escape) continue;
            if (!Input.GetKeyDown(kc)) continue;
            Hotkeys.Rebind(_listeningAction.Value, kc);   // evicts conflicts to None
            _listeningAction = null;
            RefreshHotkeyLabels();
            return;
        }
    }

    public void ShowGameOver(bool playerWon, string subtitle = "")
    {
        if (_gameOverShown || _canvasRoot == null) return;
        _gameOverShown = true;

        // N13.camp: mark campaign mission complete on win; on defeat, abort it so the
        // mission's triggers/economy don't leak into the next normal match.
        if (playerWon) CampaignSystem.OnCampaignWin();
        else           CampaignSystem.Abort();

        // Same leak existed for Art of War: ActiveChallenge was only reset by the campaign
        // screen, so after any AoW challenge ended, the NEXT match (auto-restart / new game)
        // silently re-injected the challenge's win/fail triggers over the real match. Clear it.
        ArtOfWarSystem.ActiveChallenge = ArtOfWarChallenge.None;

        var gm = GameManager.Instance;

        var overlay = NewRect("GameOverOverlay", _canvasRoot);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        // ── Title ──────────────────────────────────────────────────────────
        var titleRect = NewRect("GameOverTitle", overlay);
        titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0, 110);
        titleRect.anchoredPosition = new Vector2(0, -20);
        var title = AddText(titleRect, playerWon ? "ZAFER!" : "YENİLDİN", TextAnchor.MiddleCenter);
        title.fontSize = 76; title.fontStyle = FontStyle.Bold;
        title.color = playerWon ? Prims.Hex(0x4caf50) : Prims.Hex(0xff5555);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subRect = NewRect("GameOverSub", overlay);
            subRect.anchorMin = new Vector2(0, 1); subRect.anchorMax = new Vector2(1, 1);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.sizeDelta = new Vector2(0, 36);
            subRect.anchoredPosition = new Vector2(0, -130);
            var sub = AddText(subRect, subtitle, TextAnchor.MiddleCenter);
            sub.fontSize = 22; sub.color = Prims.Hex(0xf2d59b);
        }

        // ── N9.postgame: per-team stat summary ─────────────────────────────
        if (gm != null)
        {
            int teamCount = gm.teamRes != null ? gm.teamRes.Length : 0;
            string[] teamNames = { "Sen", "Kırmızı", "Yeşil", "Sarı" };

            // Column header
            var hdrRect = NewRect("PGHeader", overlay);
            hdrRect.anchorMin = new Vector2(0.5f, 1); hdrRect.anchorMax = new Vector2(0.5f, 1);
            hdrRect.pivot = new Vector2(0.5f, 1f);
            hdrRect.sizeDelta = new Vector2(680, 28);
            hdrRect.anchoredPosition = new Vector2(0, -176);
            var hdr = AddText(hdrRect, "Takım            Skor    Ordu   Köy  Bina   Altın  Yaş", TextAnchor.MiddleLeft);
            hdr.fontSize = 16; hdr.color = Prims.Hex(0xaab0c0);

            float rowY = -206f;
            for (int t = 0; t < Mathf.Min(teamCount, 4); t++)
            {
                int tt = t;
                // Gather stats
                int units = 0, military = 0, villagers = 0, buildings = 0;
                for (int i = 0; i < gm.units.Count; i++)
                {
                    var u = gm.units[i];
                    if (u == null || u.teamId != tt) continue;
                    units++;
                    if (u.type == UnitType.Villager) villagers++;
                    else military++;
                }
                for (int i = 0; i < gm.buildings.Count; i++)
                {
                    var b = gm.buildings[i];
                    if (b != null && b.teamId == tt && b.hp > 0f) buildings++;
                }
                var res = (gm.teamRes != null && tt < gm.teamRes.Length) ? gm.teamRes[tt] : null;
                int gold = res != null ? res.gold : 0;
                var tech = (gm.teamTech != null && tt < gm.teamTech.Length) ? gm.teamTech[tt] : null;
                string ageLbl = tech != null ? AgeName(tech.age) : "—";
                int score = units * 10 + military * 15 + buildings * 25 + (res != null ? (res.food + res.wood + res.gold + res.stone) / 10 : 0);

                string line = $"{teamNames[Mathf.Min(tt, teamNames.Length-1)]}       {score,5}  {military,5}  {villagers,3}  {buildings,4}  {gold,5}  {ageLbl}";

                var rowRect = NewRect("PGRow_" + tt, overlay);
                rowRect.anchorMin = new Vector2(0.5f, 1); rowRect.anchorMax = new Vector2(0.5f, 1);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.sizeDelta = new Vector2(680, 26);
                rowRect.anchoredPosition = new Vector2(0, rowY);
                var rowTxt = AddText(rowRect, line, TextAnchor.MiddleLeft);
                rowTxt.fontSize = 16;
                rowTxt.color = tt == 0
                    ? (playerWon ? Prims.Hex(0x4cdd6a) : Prims.Hex(0xff7070))
                    : TeamPalette.For(tt);
                rowY -= 28f;
            }
        }

        // ── Restart hint ───────────────────────────────────────────────────
        var hintRect = NewRect("GameOverHint", overlay);
        hintRect.anchorMin = new Vector2(0, 0); hintRect.anchorMax = new Vector2(1, 0);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(0, 50);
        hintRect.anchoredPosition = new Vector2(0, 30);
        var hint = AddText(hintRect, "Yeniden başlatmak için R", TextAnchor.MiddleCenter);
        hint.fontSize = 26; hint.color = new Color(0.85f, 0.85f, 0.85f, 1f);
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
        Age.Dark     => Loc.Get("age.dark"),
        Age.Feudal   => Loc.Get("age.feudal"),
        Age.Castle   => Loc.Get("age.castle"),
        Age.Imperial => Loc.Get("age.imperial"),
        _            => "",
    };

    static string UnitTr(UnitType t) => Loc.Get("unit." + t.ToString());

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
                case UnitType.Ram:
                    if (tech.Has(TechType.SiegeRam))      return "Kuşatma Koçbaşı";
                    if (tech.Has(TechType.CappedRam))     return "Gelişmiş Koçbaşı";
                    break;
                case UnitType.Mangonel:
                    if (tech.Has(TechType.SiegeOnager))   return "Kuşatma Onager";
                    if (tech.Has(TechType.Onager))        return "Onager";
                    break;
                case UnitType.Scorpion:
                    if (tech.Has(TechType.HeavyScorpion)) return "Ağır Akrep";
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
        BuildingType.FishTrap     => "Balık Tuzağı",
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
        UnitType.Ram         => "Binaları yıkan kuşatma aracı.",
        UnitType.Mangonel    => "Alan hasarlı kuşatma silahı.",
        UnitType.Scorpion    => "Piyadeye karşı kuşatma silahı.",
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
        TechType.HandCart      => "Köylü taşıma kapasitesi ve hızı daha da +.",
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
        TechType.TwoManSaw     => "Odun toplama hızı +.",
        TechType.GoldMining    => "Altın toplama hızı +.",
        TechType.GoldShaftMining => "Altın toplama hızı +.",
        TechType.StoneMining   => "Taş toplama hızı +.",
        TechType.StoneShaftMining => "Taş toplama hızı +.",
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
        TechType.TownWatch     => "Bina görüş mesafesi +.",
        TechType.TownPatrol    => "Bina görüş mesafesi daha da +.",
        TechType.Squires       => "Piyade hareket hızı +.",
        TechType.Arson         => "Piyade binalara daha fazla hasar verir.",
        TechType.Supplies      => "Asker serisi yiyecek maliyeti düşer.",
        TechType.Gambesons     => "Piyade delici zırhı +.",
        TechType.ThumbRing     => "Okçu ateş hızı +.",
        TechType.ParthianTactics => "Atlı okçu zırhı +.",
        TechType.CappedRam     => "Koçbaşı geliştirmesi.",
        TechType.SiegeRam      => "Kuşatma koçbaşı geliştirmesi.",
        TechType.Onager        => "Manganel geliştirmesi.",
        TechType.SiegeOnager   => "Kuşatma manganel geliştirmesi.",
        TechType.HeavyScorpion => "Akrep geliştirmesi.",
        TechType.EliteEagle    => "Kartal Savaşçı → Seçkin: can ve saldırı +.",
        // ── M9 civ-özel unique tech ──
        TechType.Chivalry      => "Frank: süvari canı +20.",
        TechType.BeardedAxe    => "Frank: piyade saldırısı +2.",
        TechType.Ironclad      => "Töton: kuşatma birimi zırhı +4.",
        TechType.Crenellations => "Töton: kule menzili +.",
        _                      => "",
    };

    static bool IsAgeTech(TechType t) => t == TechType.FeudalAge || t == TechType.CastleAge || t == TechType.ImperialAge;

    static bool TryGetNextAgeDef(GameManager gm, BuildingEntity b, out TechDef def)
    {
        def = default;
        if (gm == null || b == null || b.type != BuildingType.TownCenter) return false;
        var tech = gm.teamTech[b.teamId];
        if (tech == null) return false;
        TechType next = tech.age switch
        {
            Age.Dark => TechType.FeudalAge,
            Age.Feudal => TechType.CastleAge,
            Age.Castle => TechType.ImperialAge,
            _ => default,
        };
        if (!IsAgeTech(next)) return false;
        def = TechDefs.Get(next);
        return true;
    }

    static string BuildingStatusLine(GameManager gm, BuildingEntity b)
    {
        string garrison = b.GarrisonCapacity > 0 ? $"Garnizon {b.GarrisonCount}/{b.GarrisonCapacity}" : "";
        if (!TryGetNextAgeDef(gm, b, out var nextAge)) return garrison;
        var avail = TechDefs.Check(b, nextAge, gm.teamTech[b.teamId], gm.teamCivs[b.teamId], gm);
        string age = avail.canResearch ? "Cag: Hazir" : "Cag: " + TechAvailabilityReasonText(avail.reason);
        return string.IsNullOrEmpty(garrison) ? age : garrison + " | " + age;
    }

    static string AgeAdvanceTooltip(TechDef def, TechDefs.TechAvailability avail)
    {
        string desc = TechDesc(def.type);
        if (avail.canResearch) return CostLine(def.food, def.wood, def.gold, def.stone) + "\n" + desc;
        return CostLine(def.food, def.wood, def.gold, def.stone) + "\n" + desc + "\n" + "Durum: " + TechAvailabilityReasonText(avail.reason);
    }

    public static string AgeAdvanceReasonText(string reason) => TechAvailabilityReasonText(reason);

    public static string TechAvailabilityReasonText(string reason) => reason switch
    {
        "needs 2 substantial buildings" => "2 tamamlanmis ana bina gerekli",
        "age locked" => "onceki cag kosulu eksik",
        "building incomplete" => "bina insa halinde",
        "already researched" => "zaten tamamlandi",
        "missing prerequisite" => "on kosul eksik",
        "civilization locked" => "medeniyet kilidi var",
        "civilization denied" => "medeniyet bu arastirmayi acamaz",
        _ => "su an arastirilamiyor",
    };

    static string TechTreeTooltip(GameManager gm, TechDef def, bool researched, bool locked)
    {
        string body = CostLine(def.food, def.wood, def.gold, def.stone)
            + "\nBina: " + BuildingTr(def.building)
            + " | Cag: " + AgeName(def.requiredAge);
        if (def.hasRequires) body += "\nOn kosul: " + TechDefs.Get(def.requires).display;
        string desc = TechDesc(def.type);
        if (!string.IsNullOrEmpty(desc)) body += "\n" + desc;
        if (def.building == BuildingType.Monastery && def.type is TechType.Sanctity or TechType.BlockPrinting or TechType.Redemption or TechType.Theocracy)
            body += "\n" + MonasteryTechDetails(gm?.teamTech?[0], def.type);
        body += "\nDurum: " + TechTreeStatus(gm, def, researched, locked);
        return body;
    }

    static string MonasteryTechTooltip(GameManager gm, TechDef def)
    {
        string body = CostLine(def.food, def.wood, def.gold, def.stone) + "\n" + TechDesc(def.type);
        body += "\n" + MonasteryTechDetails(gm?.teamTech?[0], def.type);
        return body;
    }

    public static string MonasteryTechDetails(TechState tech, TechType focus)
    {
        bool hasBlock = tech != null && tech.Has(TechType.BlockPrinting);
        bool hasTheo = tech != null && tech.Has(TechType.Theocracy);
        bool hasRedemption = tech != null && tech.Has(TechType.Redemption);
        bool hasSanctity = tech != null && tech.Has(TechType.Sanctity);
        float currentRange = tech?.MonkConvertRange ?? 2.5f;
        float nextRange = focus == TechType.BlockPrinting && !hasBlock ? currentRange + 1.5f : currentRange;
        string spend = (hasTheo || focus == TechType.Theocracy) ? "50" : "100";
        string redemption = (hasRedemption || focus == TechType.Redemption) ? "acik" : "kapali";
        string sanctity = (hasSanctity || focus == TechType.Sanctity) ? "+15 aktif" : "+15 degil";
        return $"Donusum menzili: {currentRange:0.0} -> {nextRange:0.0}"
            + $"\nFaith: {UnitEntity.FaithFull:0} tam, {UnitEntity.FaithRegenPerSec:0.0}/sn yenilenir, donusum sonrasi %{spend} harcanir"
            + "\nRedemption: bina/kusatma donusumu " + redemption
            + "\nSanctity: Monk can bonusu " + sanctity;
    }

    static string UnitInfoSub(List<UnitEntity> sel, UnitType first)
    {
        if (sel == null || sel.Count == 0 || sel[0] == null) return "";
        if (first == UnitType.Monk)
        {
            var monk = sel[0];
            var tech = monk.TeamTech;
            float range = tech?.MonkConvertRange ?? 2.5f;
            return $"Faith {Mathf.RoundToInt(monk.faith)}/{Mathf.RoundToInt(UnitEntity.FaithFull)} | Donusum menzili {range:0.0}";
        }
        // FEEL.feedback: live carry readout for a single selected villager.
        if (first == UnitType.Villager && sel.Count == 1)
        {
            var v = sel[0];
            if (v.carrying.amount > 0)
            {
                string kindName = v.carrying.kind switch
                {
                    ResourceKind.Food  => "Yiyecek",
                    ResourceKind.Wood  => "Ahsap",
                    ResourceKind.Gold  => "Altin",
                    ResourceKind.Stone => "Tas",
                    _                  => v.carrying.kind.ToString(),
                };
                return $"Tasiyor: {v.carrying.amount}/{GatherSystem.CarryCapacityFor(v)} {kindName}";
            }
        }
        return "";
    }

    static string TechTreeStatus(GameManager gm, TechDef def, bool researched, bool locked)
    {
        if (researched) return "tamamlandi";
        if (locked) return TechAvailabilityReasonText("civilization denied");
        if (gm == null) return TechAvailabilityReasonText("no game manager");
        int teamId = GameBootstrap.LocalTeam;
        BuildingEntity building = null;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.teamId == teamId && b.type == def.building && !b.underConstruction && b.hp > 0f)
            {
                building = b;
                break;
            }
        }
        if (building == null) return "gerekli bina yok: " + BuildingTr(def.building);
        var avail = TechDefs.Check(building, def, gm.teamTech[teamId], gm.teamCivs[teamId], gm);
        return avail.canResearch ? "arastirilabilir" : TechAvailabilityReasonText(avail.reason);
    }

    static bool ShowsCivDeniedTechs(BuildingType type) => type == BuildingType.Barracks
        || type == BuildingType.ArcheryRange || type == BuildingType.Stable
        || type == BuildingType.SiegeWorkshop;

    public static bool ShouldShowCivDeniedTech(BuildingType building, Civilization civ, TechDef def, TechState tech)
    {
        return ShowsCivDeniedTechs(building)
            && def.building == building
            && !IsAgeTech(def.type)
            && (tech == null || !tech.Has(def.type))
            && CivilizationDefs.IsTechDenied(civ, def.type);
    }

    void AddCivDeniedTechButtons(GameManager gm, BuildingEntity b, ref int idx)
    {
        if (gm == null || b == null || idx >= Cols * 2 || !ShowsCivDeniedTechs(b.type)) return;
        var civ = gm.teamCivs[b.teamId];
        var tech = gm.teamTech[b.teamId];
        var all = TechDefs.All();
        for (int i = 0; i < all.Length && idx < Cols * 2; i++)
        {
            var d = all[i];
            if (!ShouldShowCivDeniedTech(b.type, civ, d, tech)) continue;
            _slots.Add(MakeButton(idx++, DeniedCol, d.display, TechDesc(d.type),
                CostLine(d.food, d.wood, d.gold, d.stone), "",
                r => CommandIconFactory.Tech(r, d.type),
                () => { },
                () => false,
                () => CivDeniedTechTooltip(d)));
        }
    }

    static string CivDeniedTechTooltip(TechDef def)
    {
        string body = CostLine(def.food, def.wood, def.gold, def.stone)
            + "\nBina: " + BuildingTr(def.building)
            + " | Cag: " + AgeName(def.requiredAge);
        if (def.hasRequires) body += "\nOn kosul: " + TechDefs.Get(def.requires).display;
        string desc = TechDesc(def.type);
        if (!string.IsNullOrEmpty(desc)) body += "\n" + desc;
        body += "\nDurum: " + TechAvailabilityReasonText("civilization denied");
        return body;
    }

    void AddAgeResearchButton(GameManager gm, BuildingEntity b, TechDef d, ref int idx)
    {
        if (idx >= Cols * 2) return;
        var bb = b;
        int teamId = b.teamId;
        int hk = idx + 1;
        _slots.Add(MakeButton(idx++, AgeCol, d.display, TechDesc(d.type),
            CostLine(d.food, d.wood, d.gold, d.stone), hk.ToString(),
            r => CommandIconFactory.Tech(r, d.type),
            () => { var g = GameManager.Instance; if (g != null && bb != null) g.research.Enqueue(bb, d); },
            () => {
                var g = GameManager.Instance; if (g == null || bb == null) return false;
                var avail = TechDefs.Check(bb, d, g.teamTech[teamId], g.teamCivs[teamId], g);
                return avail.canResearch && !g.research.IsResearching(bb)
                    && g.teamRes[teamId].CanAfford(d.food, d.wood, d.gold, d.stone);
            },
            () => {
                var g = GameManager.Instance;
                var avail = g != null && bb != null
                    ? TechDefs.Check(bb, d, g.teamTech[teamId], g.teamCivs[teamId], g)
                    : new TechDefs.TechAvailability(false, "no game manager");
                return AgeAdvanceTooltip(d, avail);
            }));
    }

    // ── DIPL: Diplomacy panel ────────────────────────────────────────────────

    bool _diplPanelOpen;
    GameObject _diplPanel;
    Text _tributeInfoText;

    /// <summary>Toggle the diplomacy panel (called from a HUD button or hotkey).</summary>
    public void ToggleDiplomacyPanel()
    {
        _diplPanelOpen = !_diplPanelOpen;
        if (_diplPanel != null) { Object.Destroy(_diplPanel); _diplPanel = null; _tributeInfoText = null; }
        if (!_diplPanelOpen) return;
        BuildDiplomacyPanel();
    }

    void BuildDiplomacyPanel()
    {
        if (_canvasRoot == null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;

        _diplPanel = new GameObject("DiplPanel");
        var rt = _diplPanel.AddComponent<RectTransform>();
        rt.SetParent(_canvasRoot, false);
        rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(260, 198);
        rt.anchoredPosition = new Vector2(8, 0);
        var bg = _diplPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.78f);

        string[] teamNames = { "", "Kırmızı", "Yeşil", "Sarı" };

        for (int t = 1; t < 4; t++)
        {
            int team = t; // capture for lambda
            var row = new GameObject($"DiplRow_{t}");
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.SetParent(rt, false);
            rowRt.anchorMin = new Vector2(0, 1); rowRt.anchorMax = new Vector2(1, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.sizeDelta = new Vector2(0, 36);
            rowRt.anchoredPosition = new Vector2(0, -(8 + (t - 1) * 40f));

            var nameRect = NewRect($"DiplName_{t}", rowRt);
            nameRect.anchorMin = Vector2.zero; nameRect.anchorMax = new Vector2(0.55f, 1);
            nameRect.offsetMin = new Vector2(8, 0); nameRect.offsetMax = Vector2.zero;
            var nameText = AddText(nameRect, teamNames[t], TextAnchor.MiddleLeft);
            nameText.color = TeamPalette.For(t);
            nameText.fontSize = 14;

            var btnRect = NewRect($"DiplBtn_{t}", rowRt);
            btnRect.anchorMin = new Vector2(0.55f, 0.1f); btnRect.anchorMax = new Vector2(0.98f, 0.9f);
            btnRect.offsetMin = Vector2.zero; btnRect.offsetMax = Vector2.zero;
            var btnImg = btnRect.gameObject.AddComponent<Image>();
            btnImg.color = new Color(0.25f, 0.25f, 0.35f, 1f);

            var btnLabelRect = NewRect($"DiplBtnLabel_{t}", btnRect);
            btnLabelRect.anchorMin = Vector2.zero; btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.offsetMin = Vector2.zero; btnLabelRect.offsetMax = Vector2.zero;
            var btnLabel = AddText(btnLabelRect, DiplStateLabel(gm.diplomacy[0, team]), TextAnchor.MiddleCenter);
            btnLabel.fontSize = 13;

            var btn = btnRect.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                var cur = gm.diplomacy[0, team];
                var next = cur == DiplomacyState.Enemy ? DiplomacyState.Neutral
                         : cur == DiplomacyState.Neutral ? DiplomacyState.Allied
                         : DiplomacyState.Enemy;
                gm.diplomacy[0, team] = next;
                btnLabel.text = DiplStateLabel(next);
                btnLabel.color = DiplStateColor(next);
            });
            btnLabel.color = DiplStateColor(gm.diplomacy[0, team]);
        }

        var tribRect = NewRect("TributeInfo", rt);
        tribRect.anchorMin = new Vector2(0, 0); tribRect.anchorMax = new Vector2(1, 0);
        tribRect.pivot = new Vector2(0.5f, 0);
        tribRect.sizeDelta = new Vector2(-16, 38);
        tribRect.anchoredPosition = new Vector2(0, 36);
        _tributeInfoText = AddText(tribRect, TributeInfoLine(gm.tech), TextAnchor.MiddleLeft);
        _tributeInfoText.fontSize = 12;
        _tributeInfoText.color = Prims.Hex(0xf2d59b);

        var closeRect = NewRect("DiplClose", rt);
        closeRect.anchorMin = new Vector2(0.7f, 0); closeRect.anchorMax = new Vector2(1, 0);
        closeRect.pivot = new Vector2(1, 0);
        closeRect.sizeDelta = new Vector2(0, 28);
        closeRect.anchoredPosition = new Vector2(-4, 4);
        var closeImg = closeRect.gameObject.AddComponent<Image>();
        closeImg.color = new Color(0.45f, 0.15f, 0.15f, 1f);
        var closeLabelRect = NewRect("DiplCloseLabel", closeRect);
        closeLabelRect.anchorMin = Vector2.zero; closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.offsetMin = Vector2.zero; closeLabelRect.offsetMax = Vector2.zero;
        var closeLabel = AddText(closeLabelRect, "Kapat", TextAnchor.MiddleCenter);
        closeLabel.fontSize = 12;
        var closeBtn = closeRect.gameObject.AddComponent<Button>();
        closeBtn.onClick.AddListener(ToggleDiplomacyPanel);
    }

    static string DiplStateLabel(DiplomacyState s) => s switch
    {
        DiplomacyState.Allied  => "İttifak",
        DiplomacyState.Neutral => "Tarafsız",
        _                      => "Düşman",
    };
    static Color DiplStateColor(DiplomacyState s) => s switch
    {
        DiplomacyState.Allied  => Prims.Hex(0x4cdd70),
        DiplomacyState.Neutral => Prims.Hex(0xf0d050),
        _                      => Prims.Hex(0xff5555),
    };

    void UpdateTributeInfo(GameManager gm)
    {
        if (_tributeInfoText == null || _diplPanel == null || !_diplPanel.activeSelf || gm == null) return;
        string line = TributeInfoLine(gm.tech);
        if (_tributeInfoText.text != line) _tributeInfoText.text = line;
    }

    public static string TributeInfoLine(TechState tech)
    {
        const int sample = 100;
        float tax = TributeSystem.TaxFor(tech);
        int received = TributeSystem.ReceivedAmount(sample, tech);
        string tier = tech != null && tech.Has(TechType.Banking) ? "Banking"
            : tech != null && tech.Has(TechType.Coinage) ? "Coinage"
            : "Standart";
        return $"Harac vergisi: %{Mathf.RoundToInt(tax * 100f)} ({tier}) | {sample} -> {received}";
    }

    public static string MarketSpreadLine()
    {
        return "Fark "
            + MarketSpreadPart("Y", ResourceKind.Food) + " | "
            + MarketSpreadPart("O", ResourceKind.Wood) + " | "
            + MarketSpreadPart("T", ResourceKind.Stone);
    }

    static string MarketSpreadPart(string label, ResourceKind kind)
    {
        var (sell, buy) = MarketSystem.Rates(kind);
        return $"{label} {sell}/{buy} (+{buy - sell})";
    }

    static TechState NodeOwnerTech(ResourceNode node)
    {
        var gm = GameManager.Instance;
        if (node == null || gm == null || gm.teamTech == null) return null;
        if (node.ownerTeamId < 0 || node.ownerTeamId >= gm.teamTech.Length) return null;
        return gm.teamTech[node.ownerTeamId];
    }

    public static string ResourceNodeInfoLine(ResourceNode node, TechState ownerTech = null)
    {
        if (node == null) return "";
        if (!node.renewable)
            return node.Depleted ? "Tükendi" : $"{node.amount} / {node.maxAmount}";

        int bonus = ownerTech != null ? ownerTech.FarmCapacityBonus : 0;
        int capacity = node.maxAmount + bonus;
        string amount = node.Depleted ? "Tükendi" : $"{node.amount} / {capacity}";
        return $"{amount} | Reseed {node.reseedWoodCost}O | Kapasite {capacity} (+{bonus})";
    }

    public static string RelicHudLine(IList<RelicEntity> relics, int teamId)
    {
        int total = relics != null ? relics.Count : 0;
        int controlled = 0;
        int deposited = 0;
        int carried = 0;
        if (relics != null)
        {
            for (int i = 0; i < relics.Count; i++)
            {
                var r = relics[i];
                if (r == null) continue;
                if (r.controllingTeam == teamId)
                {
                    controlled++;
                    if (r.heldInMonastery) deposited++;
                }
                if (r.carrier != null && r.carrier.teamId == teamId) carried++;
            }
        }
        return $"Kontrol {controlled}/{total} | Man {deposited} | Tas {carried}";
    }

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
