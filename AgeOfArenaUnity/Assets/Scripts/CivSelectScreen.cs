using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// CIVS: a lightweight pre-game civilization picker. Built procedurally (no scene
/// asset) and shown over the freshly-built arena. The player taps a civ (or "Yok"/None);
/// the choice is applied to team 0 live — gather/attack/range bonuses are read live, and
/// existing team-0 units are recomputed so HP/speed civ bonuses take effect immediately —
/// then persisted in <see cref="GameBootstrap.PlayerCiv"/> so restarts keep it. AI teams
/// (1-3) stay randomized in <see cref="WorldRoot.SetupGameplay"/>.
/// Spawned by WorldRoot at the end of Build().
/// </summary>
public class CivSelectScreen : MonoBehaviour
{
    Canvas _canvas;

    void Start()
    {
        // Need an EventSystem for uGUI buttons to receive clicks.
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("CivSelectCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;                 // above the HUD
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Dim full-screen backdrop (also eats clicks behind the panel).
        var bg = Panel(_canvas.transform, new Color(0.05f, 0.06f, 0.08f, 0.92f));
        Stretch(bg.rectTransform);

        Label(bg.transform, "MEDENİYETİNİ SEÇ", 0, 380, 56, new Color(0.95f, 0.85f, 0.55f));
        Label(bg.transform, "Bonusu hemen etkin olur — istersen 'Yok' ile dengeli başla.",
            0, 320, 26, new Color(0.8f, 0.82f, 0.85f));

        // None + every playable civ in a centered grid.
        var civs = new System.Collections.Generic.List<Civilization> { Civilization.None };
        foreach (var c in CivilizationDefs.Playable()) civs.Add(c.civ);

        const int cols = 4;
        const float bw = 260f, bh = 90f, gx = 28f, gy = 26f;
        int n = civs.Count;
        int rows = Mathf.CeilToInt(n / (float)cols);
        float totalW = cols * bw + (cols - 1) * gx;
        float startX = -totalW / 2f + bw / 2f;
        float startY = 170f;

        for (int i = 0; i < n; i++)
        {
            int r = i / cols, c = i % cols;
            int inRow = Mathf.Min(cols, n - r * cols);
            float rowW = inRow * bw + (inRow - 1) * gx;
            float rowStartX = -rowW / 2f + bw / 2f;
            float x = rowStartX + c * (bw + gx);
            float y = startY - r * (bh + gy);
            var civ = civs[i];
            CivButton(bg.transform, civ, x, y, bw, bh);
        }
    }

    void CivButton(Transform parent, Civilization civ, float x, float y, float w, float h)
    {
        var def = CivilizationDefs.Get(civ);
        var go = new GameObject("Btn_" + civ);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = civ == Civilization.None
            ? new Color(0.30f, 0.32f, 0.36f)
            : new Color(0.18f, 0.26f, 0.40f);
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.40f, 0.55f, 0.80f);
        colors.pressedColor = new Color(0.55f, 0.70f, 0.95f);
        btn.colors = colors;

        Label(go.transform, def.display, 0, 14, 30, Color.white);
        Label(go.transform, CivHint(civ), 0, -22, 19, new Color(0.78f, 0.82f, 0.88f));

        var captured = civ;
        btn.onClick.AddListener(() => Choose(captured));
    }

    void Choose(Civilization civ)
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.playerCiv = civ;                 // backs teamCivs[0]
            // Apply HP/speed civ bonuses to already-spawned team-0 units (live for the rest).
            for (int i = 0; i < gm.units.Count; i++)
            {
                var u = gm.units[i];
                if (u == null || u.teamId != 0) continue;
                u.RecomputeMaxHp();
                u.RecomputeSpeed();
            }
        }
        GameBootstrap.PlayerCiv = civ;           // persist for restarts
        Destroy(gameObject);                     // closes the overlay (canvas is a child)
    }

    /// <summary>One-line bonus hint per civ (kept in sync with CivilizationDefs).</summary>
    static string CivHint(Civilization c) => c switch
    {
        Civilization.None       => "Bonus yok — dengeli",
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
        _                       => "",
    };

    // ── tiny uGUI helpers ────────────────────────────────────────────────────
    static Image Panel(Transform parent, Color c)
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

    static Text Label(Transform parent, string text, float x, float y, int size, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(900, size + 12);
        rt.anchoredPosition = new Vector2(x, y);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = null;                          // Unity 6 default runtime font (per HUD convention)
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }
}
