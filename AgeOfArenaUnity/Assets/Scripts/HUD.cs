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

    // Top bar
    Text _foodText, _woodText, _goldText, _stoneText, _popText, _ageText;

    // ── Command bar ──────────────────────────────────────────────────────────
    RectTransform _cmdBar;
    bool _barActive;
    // Left info panel
    Text _infoName, _infoSub, _hpText;
    RectTransform _hpBarBg, _hpBarFill;
    Image _hpBarFillImg;
    // Right command card
    RectTransform _cardRoot, _progressFill;
    Image _progressFillImg;
    Text _queueText;

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
    const float BtnW   = 62f, BtnH = 62f, Gap = 6f;
    const float BarH   = 190f;
    const float LeftW  = 260f;

    // Command-button category colors.
    static readonly Color TrainCol  = Prims.Hex(0x3a6ea5);
    static readonly Color UpgCol    = Prims.Hex(0x7d5ba6);
    static readonly Color AgeCol    = Prims.Hex(0xc8a13a);
    static readonly Color BuildCol  = Prims.Hex(0x3f8f4f);
    static readonly Color MarketCol = Prims.Hex(0x2e8b8b);

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
        bar.sizeDelta = new Vector2(0, 56);
        bar.anchoredPosition = Vector2.zero;
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        float x = 24f;
        _foodText  = AddEntry(bar, ref x, Prims.Hex(0xd64545), "Food");
        _woodText  = AddEntry(bar, ref x, Prims.Hex(0x8a5a2b), "Wood");
        _goldText  = AddEntry(bar, ref x, Prims.Hex(0xf2c14e), "Gold");
        _stoneText = AddEntry(bar, ref x, Prims.Hex(0xb9b9b9), "Stone");
        _popText   = AddEntry(bar, ref x, Prims.Hex(0x6fa8dc), "Pop");

        var ageRect = NewRect("AgeText", bar);
        ageRect.anchorMin = new Vector2(1, 0.5f); ageRect.anchorMax = new Vector2(1, 0.5f);
        ageRect.pivot = new Vector2(1, 0.5f);
        ageRect.sizeDelta = new Vector2(260, 30);
        ageRect.anchoredPosition = new Vector2(-24, 0);
        _ageText = AddText(ageRect, "", TextAnchor.MiddleRight);
        _ageText.fontSize = 20;
        _ageText.fontStyle = FontStyle.Bold;
        _ageText.color = Prims.Hex(0xf2d59b);
    }

    void BuildCommandBar(Transform parent)
    {
        _cmdBar = NewRect("CommandBar", parent);
        _cmdBar.anchorMin = new Vector2(0, 0);
        _cmdBar.anchorMax = new Vector2(1, 0);
        _cmdBar.pivot     = new Vector2(0.5f, 0);
        _cmdBar.sizeDelta = new Vector2(0, BarH);
        _cmdBar.anchoredPosition = Vector2.zero;
        _cmdBar.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 0.82f);

        // ── Left info panel ──
        var left = NewRect("InfoPanel", _cmdBar);
        left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1);
        left.pivot = new Vector2(0, 0.5f);
        left.sizeDelta = new Vector2(LeftW, 0);
        left.anchoredPosition = Vector2.zero;
        left.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

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

        // ── Right command card ──
        _cardRoot = NewRect("CommandCard", _cmdBar);
        _cardRoot.anchorMin = new Vector2(0, 0); _cardRoot.anchorMax = new Vector2(1, 1);
        _cardRoot.pivot = new Vector2(0.5f, 0.5f);
        _cardRoot.offsetMin = new Vector2(LeftW + 12f, 8f);
        _cardRoot.offsetMax = new Vector2(-16f, -8f);

        // Progress bar pinned to the bottom of the card.
        var progBg = NewRect("ProgressBg", _cardRoot);
        progBg.anchorMin = new Vector2(0, 0); progBg.anchorMax = new Vector2(1, 0);
        progBg.pivot = new Vector2(0.5f, 0);
        progBg.sizeDelta = new Vector2(0, 14);
        progBg.anchoredPosition = new Vector2(0, 0);
        progBg.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

        _progressFill = NewRect("ProgressFill", progBg);
        _progressFill.anchorMin = new Vector2(0, 0); _progressFill.anchorMax = new Vector2(0, 1);
        _progressFill.pivot = new Vector2(0, 0.5f);
        _progressFill.sizeDelta = Vector2.zero;
        _progressFill.anchoredPosition = Vector2.zero;
        _progressFillImg = _progressFill.gameObject.AddComponent<Image>();
        _progressFillImg.color = Prims.Hex(0x4caf50);

        var queueRect = NewRect("QueueText", _cardRoot);
        queueRect.anchorMin = new Vector2(0, 0); queueRect.anchorMax = new Vector2(1, 0);
        queueRect.pivot = new Vector2(0.5f, 0);
        queueRect.sizeDelta = new Vector2(0, 18);
        queueRect.anchoredPosition = new Vector2(0, 18);
        _queueText = AddText(queueRect, "", TextAnchor.MiddleLeft);
        _queueText.fontSize = 13; _queueText.color = new Color(0.7f, 0.9f, 1f, 1f);

        _cmdBar.gameObject.SetActive(false);
    }

    // ── Command button factory ─────────────────────────────────────────────

    CommandSlot MakeButton(int index, Color color, string title, string cost, string hotkey,
        System.Action onClick, System.Func<bool> affordable)
    {
        int col = index % Cols;
        int row = index / Cols;

        var rt = NewRect("Btn_" + title, _cardRoot);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(BtnW, BtnH);
        rt.anchoredPosition = new Vector2(col * (BtnW + Gap), -row * (BtnH + Gap));

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = color;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.disabledColor = Color.white;   // keep our manual dim, not Unity's
        cb.fadeDuration = 0.05f;
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick());

        // Title (centered, wraps to 2 lines)
        var titleRect = NewRect("Title", rt);
        titleRect.anchorMin = new Vector2(0, 0); titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(2, 12); titleRect.offsetMax = new Vector2(-2, -10);
        var tt = AddText(titleRect, title, TextAnchor.MiddleCenter);
        tt.fontSize = 12; tt.fontStyle = FontStyle.Bold;
        tt.horizontalOverflow = HorizontalWrapMode.Wrap;
        tt.verticalOverflow = VerticalWrapMode.Truncate;

        // Cost (bottom)
        if (!string.IsNullOrEmpty(cost))
        {
            var costRect = NewRect("Cost", rt);
            costRect.anchorMin = new Vector2(0, 0); costRect.anchorMax = new Vector2(1, 0);
            costRect.pivot = new Vector2(0.5f, 0);
            costRect.sizeDelta = new Vector2(0, 14);
            costRect.anchoredPosition = new Vector2(0, 2);
            var ct = AddText(costRect, cost, TextAnchor.MiddleCenter);
            ct.fontSize = 10; ct.color = new Color(1f, 0.95f, 0.7f, 1f);
        }

        // Hotkey badge (top-left)
        if (!string.IsNullOrEmpty(hotkey))
        {
            var hkRect = NewRect("Hotkey", rt);
            hkRect.anchorMin = new Vector2(0, 1); hkRect.anchorMax = new Vector2(0, 1);
            hkRect.pivot = new Vector2(0, 1);
            hkRect.sizeDelta = new Vector2(20, 14);
            hkRect.anchoredPosition = new Vector2(2, -1);
            var ht = AddText(hkRect, hotkey, TextAnchor.UpperLeft);
            ht.fontSize = 11; ht.fontStyle = FontStyle.Bold;
            ht.color = new Color(1f, 1f, 1f, 0.85f);
        }

        return new CommandSlot { btn = btn, bg = bg, baseColor = color, affordable = affordable };
    }

    // ── Per-frame update ───────────────────────────────────────────────────

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || _cmdBar == null) return;

        var b = gm.selectedBuilding;
        var sel = gm.selection != null ? gm.selection.Selected : null;
        int unitCount = sel != null ? sel.Count : 0;
        bool hasVillager = false;
        if (sel != null)
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null && sel[i].type == UnitType.Villager) { hasVillager = true; break; }

        bool show = b != null || unitCount > 0;
        if (show != _barActive)
        {
            _barActive = show;
            _cmdBar.gameObject.SetActive(show);
        }
        if (!show)
        {
            _lastBld = null; _lastUnitCount = -1; _lastHasVillager = false; _lastTechVer = -1;
            return;
        }

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
            _lastQueueCount = qCount;
            _queueText.text = qCount > 1 ? "Kuyruk: " + qCount : (qCount == 1 ? "Eğitiliyor…" : "");
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
        _lastQueueCount = -1;
        _queueText.text = "";
        _progressFill.sizeDelta = Vector2.zero;

        if (b != null)        BuildBuildingCard(gm, b);
        else if (hasVillager) BuildVillagerCard(gm, sel, unitCount);
        else                  BuildUnitInfo(sel, unitCount);
    }

    void BuildBuildingCard(GameManager gm, BuildingEntity b)
    {
        _infoName.text = BuildingTr(b.type) + (b.underConstruction ? "  (inşa)" : "");
        _infoSub.text  = "";
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
            _slots.Add(MakeButton(idx++, TrainCol, UnitTr(def.unitType), CostLine(def.food, def.wood, def.gold, 0),
                def.hotkey,
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
            bool isAge = d.type == TechType.FeudalAge || d.type == TechType.CastleAge;
            _slots.Add(MakeButton(idx++, isAge ? AgeCol : UpgCol, d.display,
                CostLine(d.food, d.wood, d.gold, d.stone), hk.ToString(),
                () => { var g = GameManager.Instance; if (g != null && bb != null) g.research.Enqueue(bb, d); },
                () => {
                    var g = GameManager.Instance; if (g == null) return false;
                    return !g.research.IsResearching(bb) && g.resources.CanAfford(d.food, d.wood, d.gold, d.stone);
                }));
        }
    }

    void BuildMarketButtons(GameManager gm, ref int idx)
    {
        int sg = MarketSystem.SellGold, bc = MarketSystem.BuyCost, batch = MarketSystem.Batch;
        AddMarket(ref idx, ResourceKind.Food,  "Yiyecek Sat",  batch + "Y → " + sg + "A", "1");
        AddMarket(ref idx, ResourceKind.Wood,  "Odun Sat",     batch + "O → " + sg + "A", "2");
        AddMarket(ref idx, ResourceKind.Stone, "Taş Sat",      batch + "T → " + sg + "A", "3");
        // Buy food is a distinct action (gold → food).
        _slots.Add(MakeButton(idx++, MarketCol, "Yiyecek Al", bc + "A → " + batch + "Y", "4",
            () => { var rm = GameManager.Instance?.resources; if (rm != null) MarketSystem.Buy(rm, ResourceKind.Food); },
            () => { var rm = GameManager.Instance?.resources; return rm != null && rm.gold >= MarketSystem.BuyCost; }));
    }

    void AddMarket(ref int idx, ResourceKind kind, string title, string cost, string hk)
    {
        _slots.Add(MakeButton(idx++, MarketCol, title, cost, hk,
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
            _slots.Add(MakeButton(idx++, BuildCol, BuildingTr(def.type),
                CostLine(def.food, def.wood, def.gold, def.stone), def.hotkey.ToString(),
                () => { var g = GameManager.Instance; if (g != null && g.placement != null) g.placement.Begin(def.type); },
                () => {
                    var g = GameManager.Instance; if (g == null) return false;
                    return g.resources.CanAfford(def.food, def.wood, def.gold, def.stone);
                }));
        }
    }

    void BuildUnitInfo(List<UnitEntity> sel, int unitCount)
    {
        // Non-villager units: info only (no commands). Show count + dominant type.
        UnitType first = UnitType.Villager;
        bool homogeneous = true;
        for (int i = 0; i < sel.Count; i++)
        {
            if (sel[i] == null) continue;
            if (i == 0) { first = sel[i].type; continue; }
            if (sel[i].type != first) { homogeneous = false; break; }
        }
        _infoName.text = homogeneous ? unitCount + " " + UnitTr(first) : unitCount + " birim";
        _infoSub.text  = "";
        ShowHpBar(false);
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
        s.sizeDelta = new Vector2(22, 22);
        s.anchoredPosition = new Vector2(x, 0);
        s.gameObject.AddComponent<Image>().color = swatch;
        x += 30f;

        var v = NewRect(label + "Text", bar);
        v.anchorMin = v.anchorMax = v.pivot = new Vector2(0, 0.5f);
        v.sizeDelta = new Vector2(70, 30);
        v.anchoredPosition = new Vector2(x, 0);
        var t = AddText(v, "0", TextAnchor.MiddleLeft);
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
    public void ShowGameOver(bool playerWon)
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

        var hintRect = NewRect("GameOverHint", overlay);
        hintRect.anchorMin = new Vector2(0, 0.5f); hintRect.anchorMax = new Vector2(1, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(0, 40);
        hintRect.anchoredPosition = new Vector2(0, -60);
        var hint = AddText(hintRect, "Yeniden baslatmak icin R", TextAnchor.MiddleCenter);
        hint.fontSize = 28;
        hint.color = new Color(0.85f, 0.85f, 0.85f, 1f);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    static Color Dim(Color c) => new Color(c.r * 0.32f, c.g * 0.32f, c.b * 0.32f, 1f);

    static string AgeName(Age a) => a switch
    {
        Age.Dark   => "Karanlık",
        Age.Feudal => "Derebeylik",
        Age.Castle => "Kale",
        _          => "",
    };

    static string UnitTr(UnitType t) => t switch
    {
        UnitType.Villager  => "Köylü",
        UnitType.Militia   => "Asker",
        UnitType.Archer    => "Okçu",
        UnitType.Cavalry   => "Süvari",
        UnitType.Trebuchet => "Mancınık",
        UnitType.Scout     => "Gözcü",
        UnitType.Medic     => "Şifacı",
        _                  => t.ToString(),
    };

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
        _                         => t.ToString(),
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
