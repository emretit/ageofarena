using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// CIVS: pre-game civilization picker. Built procedurally over the arena.
/// Player picks a civ (or "Yok"); choice is applied live to team 0 then
/// persisted in GameBootstrap.PlayerCiv so restarts keep it.
/// </summary>
public class CivSelectScreen : MonoBehaviour
{
    // ── Palette ──────────────────────────────────────────────────────────────
    static readonly Color Gold        = new Color(0.95f, 0.82f, 0.42f);
    static readonly Color GoldDim     = new Color(0.80f, 0.68f, 0.30f);
    static readonly Color TextPrimary = new Color(0.95f, 0.96f, 1.00f);
    static readonly Color TextHint    = new Color(0.68f, 0.74f, 0.84f);
    static readonly Color TextLabel   = new Color(0.85f, 0.88f, 0.70f);
    static readonly Color BtnNormal   = new Color(0.11f, 0.16f, 0.26f);
    static readonly Color BtnNone     = new Color(0.20f, 0.22f, 0.26f);
    static readonly Color BtnHover    = new Color(0.20f, 0.30f, 0.48f);
    static readonly Color BtnPress    = new Color(0.28f, 0.42f, 0.65f);
    static readonly Color BtnCtrl     = new Color(0.22f, 0.30f, 0.44f);
    static readonly Color PanelBg     = new Color(0.04f, 0.05f, 0.09f, 0.94f);
    static readonly Color Separator   = new Color(0.95f, 0.82f, 0.42f, 0.35f);
    static readonly Color StartNormal = new Color(0.18f, 0.40f, 0.20f);
    static readonly Color StartHover  = new Color(0.26f, 0.56f, 0.28f);
    static readonly Color StartPress  = new Color(0.35f, 0.70f, 0.36f);

    Canvas _canvas;

    void Start()
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("CivSelectCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Full-screen backdrop — painterly castle artwork when present, dark panel otherwise.
        var bg = MakePanel(_canvas.transform, PanelBg);
        Stretch(bg.rectTransform);
        UiArt.ApplyBackground(bg, "menu_bg", 0.42f);

        // ── Header ─────────────────────────────────────────────────────────
        Label(bg.transform, "MEDENİYETİNİ SEÇ", 0, 448, 58, Gold, FontStyle.Bold);
        Label(bg.transform, "Medeniyetin bonusları hemen etkinleşir — 'Yok' ile dengeli başla.",
              0, 396, 22, TextHint, FontStyle.Normal);
        // Decorative gold separator line under header
        Line(bg.transform, 0, 374, 760, 2);

        // ── Civ grid ───────────────────────────────────────────────────────
        var civs = new System.Collections.Generic.List<Civilization> { Civilization.None };
        foreach (var c in CivilizationDefs.Playable()) civs.Add(c.civ);

        const int   cols = 4;
        const float bw   = 248f, bh = 86f, gx = 16f, gy = 14f;
        int   n      = civs.Count;
        float startY = 310f;

        for (int i = 0; i < n; i++)
        {
            int r     = i / cols;
            int col   = i % cols;
            int inRow = Mathf.Min(cols, n - r * cols);
            float rowW      = inRow * bw + (inRow - 1) * gx;
            float rowStartX = -rowW / 2f + bw / 2f;
            float x         = rowStartX + col * (bw + gx);
            float y         = startY - r * (bh + gy);
            CivButton(bg.transform, civs[i], x, y, bw, bh);
        }

        // ── Separator before controls ───────────────────────────────────────
        int lastRow   = (n - 1) / cols;
        float lastRowY = startY - lastRow * (bh + gy);
        float sepY    = lastRowY - bh / 2f - 22f;
        Line(bg.transform, 0, sepY, 900, 1);

        // ── Bottom controls row 1: Art-of-War challenge ────────────────────
        float aowY = sepY - 44f;
        BuildArtOfWarRow(bg.transform, 0f, aowY);
        // ── Bottom controls row 2: Map | Difficulty | Mode | Start ────────
        float ctrlY = aowY - 56f;
        BuildMapTypeRow(bg.transform,    -420f, ctrlY);
        BuildDifficultyRow(bg.transform,  -90f, ctrlY);
        BuildGameModeRow(bg.transform,    250f, ctrlY);
        BuildStartButton(bg.transform,   620f,  ctrlY);
    }

    // ── Civ button ──────────────────────────────────────────────────────────

    void CivButton(Transform parent, Civilization civ, float x, float y, float w, float h)
    {
        var def = CivilizationDefs.Get(civ);
        bool isNone = civ == Civilization.None;

        var go = new GameObject("Btn_" + civ);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);

        // Background
        var img   = go.AddComponent<Image>();
        img.color = isNone ? BtnNone : BtnNormal;

        // Thin gold accent line at top of each button
        var accent = MakePanel(go.transform, isNone ? new Color(0.6f, 0.6f, 0.6f, 0.5f) : new Color(0.95f, 0.82f, 0.42f, 0.55f));
        var art    = accent.rectTransform;
        art.anchorMin        = new Vector2(0f, 1f);
        art.anchorMax        = new Vector2(1f, 1f);
        art.pivot            = new Vector2(0.5f, 1f);
        art.offsetMin        = new Vector2(0f, -3f);
        art.offsetMax        = Vector2.zero;

        // Button hover/press tint
        var btn    = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f);
        colors.pressedColor     = new Color(1.6f, 1.6f, 1.6f);
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;

        // Civ name
        Label(go.transform, def.display, 0, 16, 28, isNone ? TextHint : TextPrimary, FontStyle.Bold);
        // Hint
        var hint = CivHint(civ);
        if (hint.Length > 0)
            Label(go.transform, hint, 0, -20, 17, isNone ? new Color(0.6f,0.6f,0.6f) : TextHint, FontStyle.Normal);

        var captured = civ;
        btn.onClick.AddListener(() => Choose(captured));
    }

    // ── Choose ──────────────────────────────────────────────────────────────

    void Choose(Civilization civ)
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.playerCiv = civ;
            for (int i = 0; i < gm.units.Count; i++)
            {
                var u = gm.units[i];
                if (u == null || u.teamId != 0) continue;
                u.RecomputeMaxHp();
                u.RecomputeSpeed();
            }
        }
        GameBootstrap.PlayerCiv = civ;
        var gm2 = GameManager.Instance;
        if (gm2 != null) GameBootstrap.NextDifficulty = gm2.difficulty;
        Destroy(gameObject);
    }

    // ── Art-of-War row ───────────────────────────────────────────────────────

    void BuildArtOfWarRow(Transform parent, float x, float y)
    {
        Label(parent, "Savaş Sanatı", x - 150f, y, 20, TextLabel, FontStyle.Normal);
        var lbl0 = Label(parent, "Kapalı", x + 10f, y, 20, TextHint, FontStyle.Normal);
        var btn = CtrlButton(parent, ArtOfWarSystem.DisplayName(ArtOfWarSystem.ActiveChallenge),
            x + 140f, y, 220, 46);
        var lbl = btn.GetComponentInChildren<Text>();
        btn.GetComponent<Button>().onClick.AddListener(() =>
        {
            var values = (ArtOfWarChallenge[])System.Enum.GetValues(typeof(ArtOfWarChallenge));
            int next = ((int)ArtOfWarSystem.ActiveChallenge + 1) % values.Length;
            ArtOfWarSystem.ActiveChallenge = values[next];
            lbl.text  = ArtOfWarSystem.DisplayName(ArtOfWarSystem.ActiveChallenge);
            lbl0.text = ArtOfWarSystem.ActiveChallenge == ArtOfWarChallenge.None ? "Kapalı" : "Aktif";
            lbl0.color = ArtOfWarSystem.ActiveChallenge == ArtOfWarChallenge.None ? TextHint : Gold;
        });
    }

    // ── Map type row ─────────────────────────────────────────────────────────

    void BuildMapTypeRow(Transform parent, float x, float y)
    {
        Label(parent, "Harita", x - 80f, y, 20, TextLabel, FontStyle.Normal);
        var btn = CtrlButton(parent, MapGenerator.DisplayName(GameBootstrap.NextMapType), x + 70f, y, 220, 46);
        var lbl = btn.GetComponentInChildren<Text>();
        btn.GetComponent<Button>().onClick.AddListener(() =>
        {
            var types = (MapType[])System.Enum.GetValues(typeof(MapType));
            int next = ((int)GameBootstrap.NextMapType + 1) % types.Length;
            GameBootstrap.NextMapType = types[next];
            lbl.text = MapGenerator.DisplayName(GameBootstrap.NextMapType);
        });
    }

    // ── Difficulty row ───────────────────────────────────────────────────────

    void BuildDifficultyRow(Transform parent, float x, float y)
    {
        Label(parent, "Zorluk", x - 80f, y, 20, TextLabel, FontStyle.Normal);
        var btn = CtrlButton(parent, DiffName(GameBootstrap.NextDifficulty), x + 70f, y, 220, 46);
        var lbl = btn.GetComponentInChildren<Text>();
        btn.GetComponent<Button>().onClick.AddListener(() =>
        {
            GameBootstrap.NextDifficulty = (Difficulty)(((int)GameBootstrap.NextDifficulty + 1) % 6);
            lbl.text = DiffName(GameBootstrap.NextDifficulty);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.difficulty = GameBootstrap.NextDifficulty;
                foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude))
                    ai.SetDifficulty();
            }
        });
    }

    // ── Game mode row ────────────────────────────────────────────────────────

    void BuildGameModeRow(Transform parent, float x, float y)
    {
        Label(parent, "Mod", x - 80f, y, 20, TextLabel, FontStyle.Normal);
        var btn = CtrlButton(parent, ModeName(GameBootstrap.NextGameMode), x + 90f, y, 280, 46);
        var lbl = btn.GetComponentInChildren<Text>();
        btn.GetComponent<Button>().onClick.AddListener(() =>
        {
            GameBootstrap.NextGameMode = (GameMode)(((int)GameBootstrap.NextGameMode + 1) % 9);
            lbl.text = ModeName(GameBootstrap.NextGameMode);
            var gm = GameManager.Instance;
            if (gm != null) gm.gameMode = GameBootstrap.NextGameMode;
        });
    }

    // ── Start button ─────────────────────────────────────────────────────────

    void BuildStartButton(Transform parent, float x, float y)
    {
        var go = new GameObject("StartBtn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(180, 52);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = StartNormal;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor     = new Color(1.5f, 1.5f, 1.5f);
        colors.fadeDuration     = 0.07f;
        btn.colors = colors;
        // Border
        var border = MakePanel(go.transform, new Color(0.4f, 0.85f, 0.42f, 0.6f));
        var brt    = border.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-2, -2); brt.offsetMax = new Vector2(2, 2);
        border.transform.SetAsFirstSibling();
        Label(go.transform, "BAŞLA  ▶", 0, 0, 26, new Color(0.9f, 1f, 0.9f), FontStyle.Bold);
        // Clicking Start = choose whichever civ was last selected (or None if none)
        btn.onClick.AddListener(() => Choose(GameBootstrap.PlayerCiv));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // A styled control button (Difficulty/Mode selector)
    GameObject CtrlButton(Transform parent, string text, float x, float y, float w, float h)
    {
        var go = new GameObject("CtrlBtn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = BtnCtrl;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor     = new Color(1.5f, 1.5f, 1.5f);
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;
        // Left arrow hint
        Label(go.transform, "◀ " + text + " ▶", 0, 0, 19, TextPrimary, FontStyle.Normal);
        return go;
    }

    void Line(Transform parent, float x, float y, float width, float height)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, y);
        go.AddComponent<Image>().color = Separator;
    }

    static Image MakePanel(Transform parent, Color c)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ── Font ──────────────────────────────────────────────────────────────────
    static Font _font;

    static Font ResolvedFont()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }

    static Text Label(Transform parent, string text, float x, float y,
                      int size, Color color, FontStyle style)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(900, size + 14);
        rt.anchoredPosition = new Vector2(x, y);
        var t = go.AddComponent<Text>();
        t.text         = text;
        t.font         = ResolvedFont();
        t.fontSize     = size;
        t.fontStyle    = style;
        t.alignment    = TextAnchor.MiddleCenter;
        t.color        = color;
        t.raycastTarget = false;
        return t;
    }

    // ── Data helpers ──────────────────────────────────────────────────────────

    static string DiffName(Difficulty d) => d switch
    {
        Difficulty.Easy     => "Kolay",
        Difficulty.Moderate => "Orta",
        Difficulty.Normal   => "Normal",
        Difficulty.Hard     => "Zor",
        Difficulty.Insane   => "Acımasız",
        Difficulty.Extreme  => "Efsanevi",
        _                   => "Normal",
    };

    static string ModeName(GameMode m) => m switch
    {
        GameMode.Deathmatch    => "Ölüm Maçı",
        GameMode.Regicide      => "Regicide",
        GameMode.Nomad         => "Göçebe",
        GameMode.EmpireWars    => "İmparatorluk",
        GameMode.KingOfTheHill => "Tepenin Kralı",
        GameMode.SuddenDeath   => "Ani Ölüm",
        GameMode.Treaty        => "Antlaşma (15dk)",
        GameMode.Turbo         => "Turbo",
        _                      => "Rastgele",
    };

    static string CivHint(Civilization c) => c switch
    {
        Civilization.None       => "Bonus yok — dengeli başlangıç",
        Civilization.Franks     => "+%20 yiyecek, süvari +%20 can",
        Civilization.Britons    => "Okçu +1 menzil, +%15 odun",
        Civilization.Mongols    => "Süvari +%25 hız, hızlı eğitim",
        Civilization.Japanese   => "Piyade +%10 atk, +%10 odun",
        Civilization.Byzantines => "Bina +%10 can, +%50 şifa",
        Civilization.Aztecs     => "+%15 yiyecek, takım +%5, Kartal",
        Civilization.Teutons    => "Bina +%15 can, güçlü piyade",
        Civilization.Persians   => "+%10 yiyecek, süvari +%10 can",
        Civilization.Vikings    => "Okçu +%10 atk, +%10 odun",
        Civilization.Saracens   => "+%15 altın, okçu +%10 atk",
        Civilization.Celts      => "+%15 odun, Woad Akıncısı",
        Civilization.Chinese    => "+%10 yiyecek, Chu Ko Nu",
        Civilization.Goths      => "Ucuz piyade, Huskarl",
        Civilization.Turks      => "+%15 altın, Yeniçeri",
        _                       => "",
    };
}
