using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central loader for the Kenney CC0 "UI Pack RPG Expansion" 9-slice sprites under
/// <c>Assets/Resources/UI/</c>. Every accessor is null-safe: if the kit is missing,
/// sprites resolve to <c>null</c> and the skinning helpers become no-ops, so the HUD
/// keeps its original flat-colour look and the build never breaks.
/// </summary>
public static class UiSkin
{
    static bool _loaded;
    static Sprite _panel, _inset, _button, _buttonPressed, _pill, _pillPressed;

    static Sprite Load(string n) => Resources.Load<Sprite>("UI/" + n);

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _panel        = Load("panel_brown");
        _inset        = Load("panelInset_brown");
        _button       = Load("buttonSquare_brown");
        _buttonPressed= Load("buttonSquare_brown_pressed");
        _pill         = Load("buttonLong_brown");
        _pillPressed  = Load("buttonLong_brown_pressed");
    }

    public static bool   Available    { get { EnsureLoaded(); return _panel != null; } }
    public static Sprite BarBg        { get { EnsureLoaded(); return _panel; } }        // wooden bar / minimap frame
    public static Sprite PanelInset   { get { EnsureLoaded(); return _inset; } }        // info panel / slot recess
    public static Sprite SlotFrame    { get { EnsureLoaded(); return _inset; } }
    public static Sprite ButtonNormal { get { EnsureLoaded(); return _button; } }
    public static Sprite ButtonPressed{ get { EnsureLoaded(); return _buttonPressed; } }
    public static Sprite PillNormal   { get { EnsureLoaded(); return _pill; } }
    public static Sprite PillPressed  { get { EnsureLoaded(); return _pillPressed; } }

    /// <summary>Apply a 9-slice sprite to an Image, tinting it. When <paramref name="s"/>
    /// is null the Image is left exactly as the caller set it (flat-colour fallback).</summary>
    public static void SkinPanel(Image img, Sprite s, Color tintIfSkinned)
    {
        if (img == null || s == null) return;
        img.sprite = s;
        img.type   = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color  = tintIfSkinned;
    }

    /// <summary>Apply a 9-slice sprite without touching the Image's existing colour
    /// (use when the caller's tint is already correct). No-op if sprite is null.</summary>
    public static void Slice(Image img, Sprite s)
    {
        if (img == null || s == null) return;
        img.sprite = s;
        img.type   = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
    }
}
