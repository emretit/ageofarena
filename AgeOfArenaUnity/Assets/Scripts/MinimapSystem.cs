using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal minimap: a secondary orthographic camera renders the arena top-down
/// into a RenderTexture that's displayed in the bottom-right corner. Unit dots
/// are drawn via OnGUI on a separate overlay Canvas so they're visible on the
/// small map without relying on scene lighting or post-processing.
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    const float MapSize  = 140f; // world units captured (covers 120×120 ground + margin)
    const int   TexSize  = 256;  // render texture resolution
    const float PanelPx  = 180f; // UI panel size in reference pixels

    Camera      _mmCam;
    RenderTexture _rt;

    // Screen-space rect of the minimap panel (for dot drawing).
    Rect _screenRect;

    void Start()
    {
        BuildCamera();
        BuildUI();
    }

    void BuildCamera()
    {
        _rt = new RenderTexture(TexSize, TexSize, 16);
        _rt.antiAliasing = 1;

        var go = new GameObject("MinimapCamera");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0, 80f, 0);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        _mmCam = go.AddComponent<Camera>();
        _mmCam.orthographic    = true;
        _mmCam.orthographicSize= MapSize * 0.5f;
        _mmCam.nearClipPlane   = 0.1f;
        _mmCam.farClipPlane    = 200f;
        _mmCam.targetTexture   = _rt;
        _mmCam.cullingMask     = LayerMask.GetMask("Default");
        _mmCam.depth           = -2; // render before main camera
        _mmCam.backgroundColor = new Color(0.05f, 0.12f, 0.05f, 1f);
        _mmCam.clearFlags      = CameraClearFlags.SolidColor;
    }

    void BuildUI()
    {
        // Build a dedicated canvas for the minimap so it stays independent of HUD.
        var canvasGo = new GameObject("MinimapCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Background panel (bottom-right corner).
        var panel = new GameObject("MinimapPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
        rt.sizeDelta = new Vector2(PanelPx + 8, PanelPx + 8);
        rt.anchoredPosition = new Vector2(-16, 16);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);

        // Actual render-texture image.
        var imgGo = new GameObject("MinimapImage");
        imgGo.transform.SetParent(panel.transform, false);
        var irt = imgGo.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0, 0); irt.anchorMax = new Vector2(1, 1);
        irt.offsetMin = new Vector2(4, 4); irt.offsetMax = new Vector2(-4, -4);
        var img = imgGo.AddComponent<RawImage>();
        img.texture = _rt;
    }

    void OnGUI()
    {
        if (_mmCam == null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Compute screen-space rect of the minimap in pixels (matching CanvasScaler 1920×1080).
        // For simplicity, calculate from actual Screen dimensions.
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        float px    = PanelPx * scale;
        float margin = 16f * scale;
        float x = Screen.width  - px - margin - 4 * scale;
        float y = margin + 4 * scale; // bottom origin but GUI is top-left, y flipped below
        _screenRect = new Rect(x, Screen.height - y - px, px, px);

        DrawDots(gm, _screenRect);
    }

    void DrawDots(GameManager gm, Rect rect)
    {
        // Team colors for dot fills.
        Color[] teamCol =
        {
            new Color(0.16f, 0.36f, 0.69f), // blue
            new Color(0.75f, 0.22f, 0.17f), // red
            new Color(0.15f, 0.68f, 0.38f), // green
            new Color(0.95f, 0.61f, 0.07f), // yellow
        };

        var units = gm.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            Vector2 uv = WorldToMinimap(u.transform.position);
            float dotX = rect.x + uv.x * rect.width  - 2f;
            float dotY = rect.y + uv.y * rect.height - 2f;
            Color c = (u.teamId >= 0 && u.teamId < teamCol.Length) ? teamCol[u.teamId] : Color.white;
            GUI.color = c;
            GUI.DrawTexture(new Rect(dotX, dotY, 5f, 5f), Texture2D.whiteTexture);
        }

        // Relics: larger markers, tinted by controlling team (neutral = gold).
        var relics = gm.relics;
        Color relicGold = new Color(1f, 0.82f, 0.2f);
        for (int i = 0; i < relics.Count; i++)
        {
            var r = relics[i];
            if (r == null) continue;
            Vector2 uv = WorldToMinimap(r.transform.position);
            float dotX = rect.x + uv.x * rect.width  - 4f;
            float dotY = rect.y + uv.y * rect.height - 4f;
            GUI.color = (r.controllingTeam >= 0 && r.controllingTeam < teamCol.Length)
                ? teamCol[r.controllingTeam] : relicGold;
            GUI.DrawTexture(new Rect(dotX, dotY, 9f, 9f), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    // Map world XZ position to UV (0–1) within the minimap square.
    static Vector2 WorldToMinimap(Vector3 world)
    {
        float half = MapSize * 0.5f;
        float u = Mathf.Clamp01((world.x + half) / MapSize);
        float v = Mathf.Clamp01(1f - (world.z + half) / MapSize); // flip Z: world +Z = minimap top
        return new Vector2(u, v);
    }
}
