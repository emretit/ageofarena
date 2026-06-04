using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// N13.tut — Rehberli ilk-oyun tutorial + coach-mark'lar.
/// PlayerPrefs "tutorial_done"=1 ise atlanır. "Atla" butonu her zaman görünür.
/// Her adım ya kullanıcı "İleri" tıklayana kadar bekler, ya da koşul sağlanınca
/// otomatik ilerler. Coach mark: hedef world-pos veya fixed screen-pos'ta pulse eden daire.
/// </summary>
public class TutorialSystem : MonoBehaviour
{
    const string DONE_KEY = "tutorial_done";

    GameManager _gm;
    Camera      _cam;
    bool        _active;
    int         _step;

    // UI
    Canvas          _canvas;
    RectTransform   _panel;
    Text            _instructionText;
    Text            _stepLabel;
    GameObject      _nextBtnGo;
    GameObject      _coachMark;     // pulsing ring
    RectTransform   _coachMarkRt;

    // step state
    int   _startFood;
    int   _startMilitiaCount;
    bool  _waitForClick; // true = "İleri" button required

    // ── step descriptors ──────────────────────────────────────────────────────
    struct TutStep
    {
        public string          text;
        public Func<bool>      done;          // null = requires click
        public Func<Vector3>   worldTarget;   // () → world pos of coach mark; null = off
        public Vector2         screenOffset;  // fallback screen pos when worldTarget is null
    }

    TutStep[] _steps;

    // ─────────────────────────────────────────────────────────────────────────
    public void Init(GameManager gm, Camera cam)
    {
        _gm  = gm;
        _cam = cam;

        if (PlayerPrefs.GetInt(DONE_KEY, 0) == 1) return;

        _startFood        = gm.resources.food;
        _startMilitiaCount = CountPlayerMilitia();

        DefineSteps();
        BuildUI();

        _active = true;
        _step   = 0;
        ShowStep();
    }

    // ─────────────────────────────────────────────────────────────────────────
    void DefineSteps()
    {
        _steps = new TutStep[]
        {
            // 0: welcome
            new TutStep
            {
                text        = "Hoş geldin! Age of Arena'ya hoş geldin.\n" +
                              "Bu kısa rehber temel mekanikleri adım adım öğretecek.",
                done        = null,
                worldTarget = null,
                screenOffset = Vector2.zero,
            },
            // 1: select a villager
            new TutStep
            {
                text        = "Sol tıkla ya da sürükleyerek bir KÖYLÜ seç.\n" +
                              "Köylüler yiyecek, odun, altın ve taş toplar.",
                done        = () => HasPlayerSelection(UnitType.Villager),
                worldTarget = () => FindNearestPlayerUnit(UnitType.Villager),
                screenOffset = new Vector2(0, 0),
            },
            // 2: gather food
            new TutStep
            {
                text        = "Köylüyü yiyecek kaynağına SAĞ-TIKLA — toplama emri verir.\n" +
                              "Yiyecek inşaat ve eğitim için gerekli!",
                done        = () => _gm.resources.food > _startFood + 5,
                worldTarget = () => FindNearestFoodNode(),
                screenOffset = new Vector2(0, 100),
            },
            // 3: build a house
            new TutStep
            {
                text        = "Bir köylü seç → B tuşuna bas → Ev (House) yap.\n" +
                              "Ev nüfus limitini 5 artırır.",
                done        = () => HasBuilding(BuildingType.House, 1),
                worldTarget = null,
                screenOffset = new Vector2(-200, 260), // command bar area
            },
            // 4: build barracks
            new TutStep
            {
                text        = "Şimdi bir KIŞLA (Barracks) inşa et — B → Kışla.\n" +
                              "Kışla'dan piyade birimi eğitebilirsin.",
                done        = () => HasBuilding(BuildingType.Barracks, 1),
                worldTarget = null,
                screenOffset = new Vector2(-200, 260),
            },
            // 5: train militia
            new TutStep
            {
                text        = "Kışla'yı seç → komut kartından NEFER (Militia) eğit.\n" +
                              "Eğitim birkaç saniye sürer.",
                done        = () => CountPlayerMilitia() > _startMilitiaCount,
                worldTarget = () => FindBuilding(BuildingType.Barracks),
                screenOffset = new Vector2(0, 260),
            },
            // 6: congratulations
            new TutStep
            {
                text        = "Harika! Temel mekanikleri tamamladın.\n" +
                              "Feodal Çağ için TC'yi seç ve Feodal Çağ araştır!",
                done        = null,
                worldTarget = null,
                screenOffset = Vector2.zero,
            },
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!_active || _waitForClick) return;
        if (_step >= _steps.Length) return;

        var s = _steps[_step];
        if (s.done != null && s.done())
        {
            _step++;
            ShowStep();
        }

        UpdateCoachMark();
    }

    void ShowStep()
    {
        if (_step >= _steps.Length) { Complete(); return; }

        var s = _steps[_step];
        _instructionText.text = s.text;
        _stepLabel.text       = $"Adım {_step + 1} / {_steps.Length}";
        _waitForClick         = (s.done == null);
        _nextBtnGo.SetActive(_waitForClick);

        // coach mark visibility
        bool hasMarker = (_step < _steps.Length) &&
                         (_steps[_step].worldTarget != null || _steps[_step].screenOffset != Vector2.zero);
        _coachMark.SetActive(hasMarker && !_waitForClick);

        UpdateCoachMark();
    }

    void UpdateCoachMark()
    {
        if (!_coachMark.activeSelf) return;
        if (_step >= _steps.Length) return;

        var s = _steps[_step];

        // Pulse scale
        float t    = Mathf.PingPong(Time.unscaledTime * 1.8f, 1f);
        float sc   = Mathf.Lerp(0.85f, 1.15f, t);
        _coachMarkRt.localScale = new Vector3(sc, sc, 1f);

        // Position
        Vector2 screenPos;
        if (s.worldTarget != null)
        {
            Vector3 world = s.worldTarget();
            if (world != Vector3.zero && _cam != null)
            {
                Vector3 sp = _cam.WorldToScreenPoint(world);
                screenPos = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f);
            }
            else
            {
                screenPos = s.screenOffset;
            }
        }
        else
        {
            screenPos = s.screenOffset;
        }

        _coachMarkRt.anchoredPosition = screenPos + new Vector2(0, 70); // offset above target
    }

    void Complete()
    {
        PlayerPrefs.SetInt(DONE_KEY, 1);
        PlayerPrefs.Save();
        if (_panel != null) _panel.gameObject.SetActive(false);
        if (_coachMark != null) _coachMark.SetActive(false);
        _active = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region Condition helpers

    bool HasPlayerSelection(UnitType ut)
    {
        foreach (var u in _gm.selection.Selected)
            if (u != null && u.teamId == 0 && u.type == ut) return true;
        return false;
    }

    bool HasBuilding(BuildingType bt, int minCount)
    {
        int n = 0;
        foreach (var b in _gm.buildings)
            if (b != null && b.teamId == 0 && b.type == bt && b.buildProgress >= 1f)
                n++;
        return n >= minCount;
    }

    int CountPlayerMilitia()
    {
        int n = 0;
        foreach (var u in _gm.units)
            if (u != null && u.teamId == 0 && u.type == UnitType.Militia) n++;
        return n;
    }

    Vector3 FindNearestPlayerUnit(UnitType ut)
    {
        Vector3 best  = Vector3.zero;
        float   bestD = float.MaxValue;
        foreach (var u in _gm.units)
        {
            if (u == null || u.teamId != 0 || u.type != ut) continue;
            float d = u.transform.position.sqrMagnitude;
            if (d < bestD) { bestD = d; best = u.transform.position; }
        }
        return best;
    }

    Vector3 FindNearestFoodNode()
    {
        var nodes = FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude);
        Vector3 best  = Vector3.zero;
        float   bestD = float.MaxValue;
        foreach (var n in nodes)
        {
            if (n.kind != ResourceKind.Food) continue;
            float d = n.transform.position.sqrMagnitude;
            if (d < bestD) { bestD = d; best = n.transform.position; }
        }
        return best;
    }

    Vector3 FindBuilding(BuildingType bt)
    {
        foreach (var b in _gm.buildings)
            if (b != null && b.teamId == 0 && b.type == bt && b.buildProgress >= 1f)
                return b.transform.position;
        return Vector3.zero;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region UI construction

    void BuildUI()
    {
        // Dedicated canvas (sortingOrder 200, above HUD)
        var canvasGo = new GameObject("TutorialCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920, 1080);
        scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight   = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var font = ResolveFont();

        // ── Instruction panel (anchored bottom-centre above command bar) ──────
        var panelGo = new GameObject("TutPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        _panel = panelGo.AddComponent<RectTransform>();
        _panel.anchorMin        = new Vector2(0.5f, 0f);
        _panel.anchorMax        = new Vector2(0.5f, 0f);
        _panel.sizeDelta        = new Vector2(760, 110);
        _panel.anchoredPosition = new Vector2(0f, 245f); // above 220px command bar
        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.1f, 0.93f);

        // Gold top border
        var border = MakeRect("Border", _panel);
        border.anchorMin = new Vector2(0, 1); border.anchorMax = new Vector2(1, 1);
        border.sizeDelta = new Vector2(0, 3); border.anchoredPosition = new Vector2(0, 0);
        border.gameObject.AddComponent<Image>().color = Prims.Hex(0xc8a13a);

        // Step label (top-left)
        var stepRect = MakeRect("StepLabel", _panel);
        stepRect.anchorMin = new Vector2(0f, 1f); stepRect.anchorMax = new Vector2(0.4f, 1f);
        stepRect.sizeDelta = new Vector2(0, 22); stepRect.anchoredPosition = new Vector2(10, -13);
        _stepLabel = AddText(stepRect, "", TextAnchor.MiddleLeft, font, 14);
        _stepLabel.color = Prims.Hex(0xc8a13a);
        _stepLabel.fontStyle = FontStyle.Bold;

        // Instruction text (center)
        var txtRect = MakeRect("InstrText", _panel);
        txtRect.anchorMin = new Vector2(0, 0); txtRect.anchorMax = new Vector2(1, 1);
        txtRect.offsetMin = new Vector2(12, 30); txtRect.offsetMax = new Vector2(-12, -4);
        _instructionText = AddText(txtRect, "", TextAnchor.MiddleCenter, font, 20);
        _instructionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _instructionText.verticalOverflow   = VerticalWrapMode.Truncate;

        // "İleri" button (bottom-centre, only for click-to-continue steps)
        _nextBtnGo = BuildBtn("İleri", _panel, new Vector2(0f, 0f), new Vector2(0, 15),
                              new Vector2(100, 26), Prims.Hex(0x2a6e2a), font, () =>
        {
            _step++;
            ShowStep();
        });

        // "Atla" button (always visible, bottom-right)
        BuildBtn("Atla", _panel, new Vector2(1f, 0f), new Vector2(-50, 15),
                 new Vector2(80, 26), new Color(0.4f, 0.12f, 0.12f, 0.9f), font, Complete);

        // ── Coach mark (pulsing dashed ring) ────────────────────────────────
        var cmGo = new GameObject("CoachMark");
        cmGo.transform.SetParent(canvasGo.transform, false);
        _coachMarkRt = cmGo.AddComponent<RectTransform>();
        _coachMarkRt.anchorMin = new Vector2(0.5f, 0.5f);
        _coachMarkRt.anchorMax = new Vector2(0.5f, 0.5f);
        _coachMarkRt.sizeDelta = new Vector2(54, 54);

        // Outer ring (Image with alpha)
        var ringImg = cmGo.AddComponent<Image>();
        ringImg.color = new Color(1f, 0.85f, 0.1f, 0.85f);
        // inner cutout: add a slightly smaller white square to fake a ring (simple)
        var inner = new GameObject("Inner");
        inner.transform.SetParent(cmGo.transform, false);
        var innerRt = inner.AddComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(5, 5); innerRt.offsetMax = new Vector2(-5, -5);
        inner.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f, 0f); // transparent inside

        // Arrow label
        var arrowGo = new GameObject("Arrow");
        arrowGo.transform.SetParent(cmGo.transform, false);
        var arrRt = arrowGo.AddComponent<RectTransform>();
        arrRt.anchorMin = new Vector2(0.5f, 0f); arrRt.anchorMax = new Vector2(0.5f, 0f);
        arrRt.sizeDelta = new Vector2(30, 30); arrRt.anchoredPosition = new Vector2(0, -20);
        var arrText = AddText(arrRt, "▼", TextAnchor.MiddleCenter, font, 20);
        arrText.color = Prims.Hex(0xffdd00);

        _coachMark = cmGo;
        cmGo.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static Text AddText(RectTransform parent, string content, TextAnchor anchor, Font font, int size)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.font      = font;
        t.fontSize  = size;
        t.alignment = anchor;
        t.color     = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.text      = content;
        return t;
    }

    static GameObject BuildBtn(string label, Transform parent, Vector2 anchor, Vector2 pos,
                                Vector2 size, Color color, Font font, Action onClick)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>(); img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        AddText(rt, label, TextAnchor.MiddleCenter, font, 15);
        return go;
    }

    static Font ResolveFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    #endregion
}
