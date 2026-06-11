using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loads the painterly UI artwork (Higgsfield-generated, style-locked to the
/// AoE2-HD direction) from <c>Resources/UI/Art/</c> and applies it as a
/// full-bleed background <see cref="Image"/>. All call sites degrade
/// gracefully: if a texture is missing the old flat-colour look remains.
/// </summary>
public static class UiArt
{
    static readonly System.Collections.Generic.Dictionary<string, Sprite> _cache = new();

    /// <summary>Sprite from Resources/UI/Art/<paramref name="name"/> (cached), or null.</summary>
    public static Sprite Load(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;
        var tex = Resources.Load<Texture2D>("UI/Art/" + name);
        Sprite s = tex == null ? null
            : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        _cache[name] = s;   // cache nulls too so missing art is checked once
        return s;
    }

    /// <summary>
    /// Put artwork behind <paramref name="panel"/>: the panel keeps its rect but its
    /// own colour is replaced by the art plus a dark tint so text stays readable.
    /// Returns false (and leaves the panel untouched) when the art is missing.
    /// </summary>
    public static bool ApplyBackground(Image panel, string artName, float tint = 0.55f)
    {
        var sprite = Load(artName);
        if (sprite == null) return false;

        panel.sprite = sprite;
        panel.color = Color.white;
        panel.type = Image.Type.Simple;
        panel.preserveAspect = false;   // fill; 16:9 art on 16:9-ish screens

        // Dark veil child for text readability.
        var veil = new GameObject("ArtVeil");
        veil.transform.SetParent(panel.transform, false);
        var rt = veil.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        veil.AddComponent<Image>().color = new Color(0f, 0f, 0f, tint);
        veil.transform.SetAsFirstSibling();   // behind the panel's other children
        return true;
    }
}
