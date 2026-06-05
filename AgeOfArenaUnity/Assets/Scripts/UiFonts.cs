using UnityEngine;

/// <summary>
/// Single source for the runtime UI font. Unity 6 removed the implicit default font on
/// uGUI <see cref="UnityEngine.UI.Text"/>: a null font renders NOTHING (this caused several
/// "blank menu" bugs — ScenarioEditor, CampaignScreen, ReplayViewer). Always assign
/// <see cref="Default"/> instead of leaving font null.
///
/// LegacyRuntime.ttf is the built-in font shipped with Unity 6; it also works on WebGL.
/// </summary>
public static class UiFonts
{
    static Font _default;

    /// <summary>The shared built-in UI font. Cached after first lookup.</summary>
    public static Font Default => _default != null
        ? _default
        : (_default = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
}
