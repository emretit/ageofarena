using UnityEngine;

/// <summary>
/// N9.a11y: Persistent accessibility preferences (PlayerPrefs).
/// - ColorblindMode: swaps TeamPalette to deuteranopia-safe colours (orange/cyan instead of red/green).
/// - UiScale: HUD canvas reference-resolution multiplier (0.75×–1.5×).
/// </summary>
public static class AccessibilitySettings
{
    const string KeyCB    = "A11y.ColorblindMode";
    const string KeyScale = "A11y.UiScale";

    public const float ScaleMin     = 0.75f;
    public const float ScaleMax     = 1.50f;
    public const float ScaleDefault = 1.00f;

    static bool  _colorblindMode;
    static float _uiScale = ScaleDefault;

    public static bool  ColorblindMode => _colorblindMode;
    public static float UiScale        => _uiScale;

    public static void Load()
    {
        _colorblindMode = PlayerPrefs.GetInt(KeyCB, 0) == 1;
        _uiScale        = PlayerPrefs.GetFloat(KeyScale, ScaleDefault);
        _uiScale        = Mathf.Clamp(_uiScale, ScaleMin, ScaleMax);
    }

    public static void SetColorblindMode(bool on)
    {
        _colorblindMode = on;
        PlayerPrefs.SetInt(KeyCB, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetUiScale(float scale)
    {
        _uiScale = Mathf.Clamp(scale, ScaleMin, ScaleMax);
        PlayerPrefs.SetFloat(KeyScale, _uiScale);
        PlayerPrefs.Save();
    }
}
