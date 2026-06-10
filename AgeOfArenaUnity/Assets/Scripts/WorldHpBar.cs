using UnityEngine;

/// <summary>
/// N1.hpbar: World-space billboard HP bar. Replaces the IMGUI OnGUI approach in
/// CombatSystem so 400+ units draw their bars without IMGUI CPU overhead.
///
/// Two thin quads (background + fill) are created as children and positioned
/// above the entity. Each frame the fill scale is updated and all quads
/// billboard-rotate toward Camera.main.
/// </summary>
public class WorldHpBar : MonoBehaviour
{
    const float BarWidth  = 0.6f;
    const float BarHeight = 0.055f;

    static readonly Color BgColor      = new Color(0f, 0f, 0f, 0.75f);
    static readonly Color FriendlyColor = new Color(0.30f, 0.85f, 0.35f, 1f);
    static readonly Color EnemyColor    = new Color(0.85f, 0.25f, 0.20f, 1f);

    Transform _bg;
    Transform _fill;
    bool _friendly;
    bool _visible;

    public void Init(float yOffset, bool friendly)
    {
        _friendly = friendly;

        // Background quad
        _bg = CreateQuad("HpBg", new Vector3(0f, yOffset, 0f),
            new Vector3(BarWidth, BarHeight, 0.001f), BgColor);

        // Fill quad (child of bg for easy scale control)
        _fill = CreateQuad("HpFill", Vector3.zero,
            new Vector3(BarWidth, BarHeight, 0.001f), friendly ? FriendlyColor : EnemyColor);
        _fill.SetParent(_bg, false);
        _fill.localPosition = Vector3.zero;

        SetVisible(false);
    }

    /// <summary>Update bar fill fraction (0-1) and visibility.</summary>
    public void Refresh(float frac, bool show)
    {
        SetVisible(show);
        if (!show) return;
        frac = Mathf.Clamp01(frac);
        // Scale fill: starts at full width, shrinks left to right.
        _fill.localScale = new Vector3(frac, 1f, 1f);
        // Pivot correction: move fill so it aligns left.
        _fill.localPosition = new Vector3((frac - 1f) * BarWidth * 0.5f, 0f, -0.0005f);
    }

    void SetVisible(bool on)
    {
        if (_visible == on) return;
        _visible = on;
        if (_bg != null) _bg.gameObject.SetActive(on);
    }

    // Camera.main does a tagged scene scan; cache its transform statically (shared by every
    // bar). Re-fetches automatically when the cached camera is destroyed (e.g. on Restart).
    static Transform _camTf;
    static Transform CamTf
    {
        get
        {
            if (_camTf == null) { var c = Camera.main; _camTf = c != null ? c.transform : null; }
            return _camTf;
        }
    }

    void LateUpdate()
    {
        if (!_visible) return;
        var cam = CamTf;
        if (cam == null) return;
        // Billboard: face the camera.
        _bg.rotation = cam.rotation;
    }

    Transform CreateQuad(string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go  = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        // Destroy the MeshCollider that CreatePrimitive adds — no physics needed.
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        var mr  = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Prims.Mat(color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }
}
