using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// AoE2-style diamond minimap embedded in the HUD bottom bar. A secondary
/// orthographic camera renders the arena top-down into a RenderTexture; that
/// texture is shown in a RawImage rotated 45° so the square map reads as a diamond
/// (its corners pointing N/E/S/W — no map area lost). Unit/building/relic blips are
/// uGUI Images parented under the rotated RawImage, so they inherit the rotation and
/// land correctly inside the diamond. Clicking navigates the camera; right-click
/// orders the current selection to move there.
///
/// World↔minimap mapping goes through the minimap camera (WorldToViewportPoint /
/// ViewportToWorldPoint) so it stays correct regardless of camera orientation.
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    const float MapSize = 140f; // world units captured (covers the arena + margin)
    const int   TexSize = 256;  // render-texture resolution
    const float Side    = 130f; // RawImage square side; rotated 45° → diamond ≈184px bound

    Camera        _mmCam;
    RenderTexture _rt;
    RectTransform _mapRT;       // the rotated RawImage rect: blip parent + click target
    readonly List<Image> _blips = new();

    static readonly Color[] TeamCol =
    {
        new Color(0.16f, 0.36f, 0.69f), // 0 blue
        new Color(0.75f, 0.22f, 0.17f), // 1 red
        new Color(0.15f, 0.68f, 0.38f), // 2 green
        new Color(0.95f, 0.61f, 0.07f), // 3 yellow
    };
    static readonly Color RelicGold = new Color(1f, 0.82f, 0.2f);

    void Start()
    {
        BuildCamera();
        BuildUI();
    }

    void BuildCamera()
    {
        _rt = new RenderTexture(TexSize, TexSize, 16) { antiAliasing = 1 };

        var go = new GameObject("MinimapCamera");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0, 80f, 0);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down

        _mmCam = go.AddComponent<Camera>();
        _mmCam.orthographic     = true;
        _mmCam.orthographicSize  = MapSize * 0.5f;
        _mmCam.nearClipPlane     = 0.1f;
        _mmCam.farClipPlane      = 200f;
        _mmCam.targetTexture     = _rt;
        _mmCam.cullingMask       = LayerMask.GetMask("Default");
        _mmCam.depth             = -2; // render before the main camera
        _mmCam.backgroundColor   = new Color(0.05f, 0.12f, 0.05f, 1f);
        _mmCam.clearFlags        = CameraClearFlags.SolidColor;
    }

    void BuildUI()
    {
        Transform parent = ResolveParent();

        // Diamond backing frame (slightly larger, rotated) → bordered look.
        var frameGo = new GameObject("MinimapFrame", typeof(RectTransform), typeof(Image));
        var frt = (RectTransform)frameGo.transform;
        frt.SetParent(parent, false);
        frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(Side + 12f, Side + 12f);
        frt.localRotation = Quaternion.Euler(0, 0, 45f);
        var fimg = frameGo.GetComponent<Image>();
        fimg.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);  // flat fallback
        UiSkin.SkinPanel(fimg, UiSkin.BarBg, Color.white);  // wooden rim when kit present
        fimg.raycastTarget = false;

        // Rotated map render (the diamond itself).
        var imgGo = new GameObject("MinimapImage", typeof(RectTransform), typeof(RawImage));
        _mapRT = (RectTransform)imgGo.transform;
        _mapRT.SetParent(parent, false);
        _mapRT.anchorMin = _mapRT.anchorMax = _mapRT.pivot = new Vector2(0.5f, 0.5f);
        _mapRT.sizeDelta = new Vector2(Side, Side);
        _mapRT.localRotation = Quaternion.Euler(0, 0, 45f);
        var raw = imgGo.GetComponent<RawImage>();
        raw.texture       = _rt;
        raw.raycastTarget = true; // receives clicks → MinimapClick (also marks "over UI")

        // MMTR: fog overlay image — same size, on top of the terrain render.
        var fogGo = new GameObject("FogOverlay", typeof(RectTransform), typeof(RawImage));
        var fogRt = (RectTransform)fogGo.transform;
        fogRt.SetParent(_mapRT, false);
        fogRt.anchorMin = fogRt.anchorMax = fogRt.pivot = new Vector2(0.5f, 0.5f);
        fogRt.sizeDelta = new Vector2(Side, Side);
        fogGo.GetComponent<RawImage>().color = new Color(1, 1, 1, 0.55f); // semi-transparent
        fogGo.GetComponent<RawImage>().raycastTarget = false;
        StartCoroutine(UpdateFogOverlay(fogGo.GetComponent<RawImage>()));

        var click = imgGo.AddComponent<MinimapClick>();
        click.Init(this, _mapRT);
    }

    /// <summary>Parent the minimap into the HUD bottom bar's minimap zone. Falls back to
    /// a dedicated bottom-right canvas if the HUD isn't ready (build-order safety).</summary>
    Transform ResolveParent()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.hud != null && gm.hud.MinimapZone != null)
            return gm.hud.MinimapZone;

        var canvasGo = new GameObject("MinimapCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var holder = new GameObject("MinimapHolder", typeof(RectTransform));
        var hrt = (RectTransform)holder.transform;
        hrt.SetParent(canvasGo.transform, false);
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1, 0);
        hrt.sizeDelta = new Vector2(Side + 40f, Side + 40f);
        hrt.anchoredPosition = new Vector2(-20, 20);
        return hrt;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || _mapRT == null || _mmCam == null) return;

        int idx = 0;
        var units = gm.units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u != null) idx = Place(idx, u.transform.position, TeamColor(u.teamId), 4.5f);
        }
        var blds = gm.buildings;
        for (int i = 0; i < blds.Count; i++)
        {
            var b = blds[i];
            if (b != null) idx = Place(idx, b.transform.position, TeamColor(b.teamId), 6f);
        }
        var relics = gm.relics;
        for (int i = 0; i < relics.Count; i++)
        {
            var r = relics[i];
            if (r == null) continue;
            Color c = (r.controllingTeam >= 0 && r.controllingTeam < TeamCol.Length)
                ? TeamCol[r.controllingTeam] : RelicGold;
            idx = Place(idx, r.transform.position, c, 8f);
        }

        for (int i = idx; i < _blips.Count; i++)
            if (_blips[i].gameObject.activeSelf) _blips[i].gameObject.SetActive(false);
    }

    int Place(int idx, Vector3 world, Color c, float size)
    {
        Vector3 vp = _mmCam.WorldToViewportPoint(world);
        var img = GetBlip(idx);
        var rt  = img.rectTransform;
        rt.sizeDelta = new Vector2(size, size);
        // Local position inside the (un-rotated) RawImage; parent rotation diamonds it.
        rt.anchoredPosition = new Vector2(
            (Mathf.Clamp01(vp.x) - 0.5f) * Side,
            (Mathf.Clamp01(vp.y) - 0.5f) * Side);
        img.color = c;
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        return idx + 1;
    }

    Image GetBlip(int i)
    {
        while (_blips.Count <= i)
        {
            var go = new GameObject("Blip", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_mapRT, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var im = go.GetComponent<Image>();
            im.raycastTarget = false;
            _blips.Add(im);
        }
        return _blips[i];
    }

    static Color TeamColor(int t) => (t >= 0 && t < TeamCol.Length) ? TeamCol[t] : Color.white;

    /// <summary>Called by <see cref="MinimapClick"/> with a rotation-aware local point
    /// inside the RawImage. Left/drag = recentre camera; right = order selection there.</summary>
    public void OnMapPoint(Vector2 local, bool rightClick)
    {
        var gm = GameManager.Instance;
        if (gm == null || _mmCam == null) return;

        float vx = Mathf.Clamp01(local.x / Side + 0.5f);
        float vy = Mathf.Clamp01(local.y / Side + 0.5f);
        // Orthographic camera looks straight down from y = transform.position.y onto y=0.
        Vector3 world = _mmCam.ViewportToWorldPoint(new Vector3(vx, vy, _mmCam.transform.position.y));
        world.y = 0f;

        if (rightClick)
        {
            if (gm.command != null && gm.selection != null && gm.selection.Selected.Count > 0)
                gm.command.MoveSelectedTo(world);
        }
        else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            // MPNG: Alt + left-click on minimap = place a ping marker in the world.
            SpawnPing(world);
        }
        else if (gm.cameraRig != null)
        {
            gm.cameraRig.FocusOn(world);
        }
    }

    // MMTR: every 0.5s sync the FoW texture to the minimap fog overlay.
    System.Collections.IEnumerator UpdateFogOverlay(RawImage overlay)
    {
        while (overlay != null)
        {
            yield return new WaitForSeconds(0.5f);
            var gm = GameManager.Instance;
            var fogTex = gm?.fow?.FogTexture;
            overlay.texture = fogTex;
            overlay.enabled = fogTex != null;
        }
    }

    // MPNG: spawn a brief visual ping marker at world pos.
    static void SpawnPing(Vector3 world)
    {
        var go = new UnityEngine.GameObject("Ping");
        go.transform.position = world + Vector3.up * 0.05f;
        var mr = go.AddComponent<MeshRenderer>();
        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = CreateQuadMesh(2.5f);
        mr.material = new Material(Shader.Find("Sprites/Default"));
        mr.material.color = new Color(1f, 0.9f, 0.1f, 0.85f);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<PingDecay>();
        AudioManager.Play(AudioManager.SoundId.ButtonClick, 0.4f);
    }

    static Mesh CreateQuadMesh(float size)
    {
        float h = size * 0.5f;
        var m = new Mesh();
        m.vertices  = new[] { new Vector3(-h,0,-h), new Vector3(-h,0,h), new Vector3(h,0,h), new Vector3(h,0,-h) };
        m.triangles = new[] { 0,1,2, 0,2,3 };
        m.uv        = new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
        m.RecalculateNormals();
        return m;
    }
}

/// <summary>MPNG: auto-destroys the ping marker after 2 seconds.</summary>
class PingDecay : MonoBehaviour
{
    float _t = 2f;
    void Update()
    {
        _t -= Time.deltaTime;
        if (_t <= 0f) Destroy(gameObject);
    }
}

/// <summary>Routes rotation-aware pointer events on the diamond RawImage to the
/// MinimapSystem. <c>ScreenPointToLocalPointInRectangle</c> accounts for the 45°
/// rotation, so the returned local point maps straight to a viewport coordinate.</summary>
class MinimapClick : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    MinimapSystem _sys;
    RectTransform _rt;

    public void Init(MinimapSystem sys, RectTransform rt) { _sys = sys; _rt = rt; }

    public void OnPointerClick(PointerEventData e)
        => Handle(e, e.button == PointerEventData.InputButton.Right);

    // Drag pans the camera (left button only); right-drag is ignored to avoid spamming move orders.
    public void OnDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) Handle(e, false);
    }

    void Handle(PointerEventData e, bool rightClick)
    {
        if (_sys == null || _rt == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var lp))
            _sys.OnMapPoint(lp, rightClick);
    }
}
