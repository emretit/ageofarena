using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural uGUI command-card icons. Each unit/building/tech glyph is composed
/// from a handful of tinted primitive sprites (square, circle, ring, triangle,
/// diamond) — the UI analogue of how <see cref="Prims"/> builds 3D structures from
/// boxes and cones. The base sprites are generated once and cached. Icons are
/// intentionally schematic ("good-enough" silhouettes); the hotkey badge, cost
/// line and hover tooltip carry the precise meaning.
///
/// Usage: create a centred icon container RectTransform (~40×40) on a command
/// button, then call <see cref="Unit"/>/<see cref="Building"/>/<see cref="Tech"/>/
/// <see cref="Market"/> to populate it.
/// </summary>
public static class CommandIconFactory
{
    // ── Palette ───────────────────────────────────────────────────────────────
    static readonly Color Skin   = Prims.Hex(0xe0b68a);
    static readonly Color Blue   = Prims.Hex(0x2a5db0);
    static readonly Color Steel  = Prims.Hex(0xcfd4da);
    static readonly Color Wood   = Prims.Hex(0x8a5a2b);
    static readonly Color Dark   = Prims.Hex(0x5b3a1e);
    static readonly Color Green  = Prims.Hex(0x6f9e3f);
    static readonly Color Gold   = Prims.Hex(0xf2c14e);
    static readonly Color Stone  = Prims.Hex(0xb9b9b9);
    static readonly Color Red    = Prims.Hex(0xd64545);
    static readonly Color Roof   = Prims.Hex(0xb5482f);
    static readonly Color Wall   = Prims.Hex(0xcdbb98);
    static readonly Color Shield = Prims.Hex(0x6a8fc0);

    // ── Cached base sprites ─────────────────────────────────────────────────────
    const int Res = 48;
    static Sprite _square, _circle, _ring, _triangle;
    static Sprite Square   => _square   != null ? _square   : (_square   = MakeSquare());
    static Sprite Circle   => _circle   != null ? _circle   : (_circle   = MakeCircle(false));
    static Sprite Ring     => _ring     != null ? _ring     : (_ring     = MakeCircle(true));
    static Sprite Triangle => _triangle != null ? _triangle : (_triangle = MakeTriangle());

    // ── Public builders ─────────────────────────────────────────────────────────

    public static void Unit(RectTransform icon, UnitType t)
    {
        switch (t)
        {
            case UnitType.Villager:
                Add(icon, Square, Blue, 0, -5, 16, 18);            // body
                Add(icon, Circle, Skin, 0, 11, 12, 12);            // head
                Add(icon, Square, Wood, 11, 0, 4, 18, 35);         // tool
                break;
            case UnitType.Militia:
                Add(icon, Circle, Shield, -5, -3, 28, 28);         // shield
                Add(icon, Ring,   Steel, -5, -3, 28, 28);
                Add(icon, Square, Steel, 7, 4, 4, 26, -30);        // blade
                Add(icon, Square, Dark,  2, -7, 13, 4, -30);       // guard
                break;
            case UnitType.Archer:
                Add(icon, Ring,    Wood,  -5, 0, 20, 34);          // bow
                Add(icon, Square,  Dark,   3, 0, 26, 3);           // arrow shaft
                Add(icon, Triangle, Steel, 16, 0, 9, 9, -90);      // arrowhead
                break;
            case UnitType.Cavalry:
                Add(icon, Square, Wood, 0, -2, 26, 12);            // horse body
                Add(icon, Square, Wood, 11, 8, 7, 14, 20);         // neck/head
                Add(icon, Square, Dark, -7, -12, 3, 10);           // legs
                Add(icon, Square, Dark, 8, -12, 3, 10);
                Add(icon, Square, Blue, -1, 9, 8, 8);              // rider
                break;
            case UnitType.Trebuchet:
                Add(icon, Triangle, Dark,  0, -6, 28, 20);         // A-frame
                Add(icon, Square,   Wood,  0, 4, 30, 4, 25);       // throwing beam
                Add(icon, Square,   Stone, -13, -1, 8, 8, 25);     // counterweight
                Add(icon, Circle,   Dark,  -9, -14, 9, 9);         // wheel
                break;
            case UnitType.Scout:
                Add(icon, Triangle, Prims.Hex(0x8a6d3b), 0, -6, 22, 22); // cloak
                Add(icon, Circle,   Skin, 0, 9, 12, 12);           // head
                Add(icon, Square,   Gold, 8, 10, 9, 3, -20);       // spyglass
                break;
            case UnitType.Medic:
                Add(icon, Circle, Color.white, 0, 0, 30, 30);      // white disc
                Add(icon, Ring,   Red, 0, 0, 30, 30);
                Add(icon, Square, Red, 0, 0, 6, 18);               // cross v
                Add(icon, Square, Red, 0, 0, 18, 6);               // cross h
                break;
            case UnitType.Spearman:
                Add(icon, Square,   Steel,  5, -3, 28, 28);        // shield
                Add(icon, Ring,     Blue,   5, -3, 28, 28);
                Add(icon, Square,   Wood,  -9, 4, 4, 30);          // spear shaft
                Add(icon, Triangle, Steel, -9, 19, 9, 9, 0);       // spear tip
                break;
            case UnitType.Galley:
                Add(icon, Square,   Wood,  0, -6, 28, 10);         // hull
                Add(icon, Triangle, Dark, -13, -4, 8, 12);         // bow
                Add(icon, Square,   Dark,  0, 6, 3, 22);           // mast
                Add(icon, Square,   Blue,  5, 8, 12, 8, -15);      // sail
                break;
        }
    }

    public static void Building(RectTransform icon, BuildingType t)
    {
        switch (t)
        {
            case BuildingType.TownCenter:
                Add(icon, Square,   Wall, 0, -6, 30, 20);
                Add(icon, Triangle, Roof, 0, 11, 34, 16);
                Add(icon, Square,   Dark, 0, -10, 8, 12);          // door
                Add(icon, Square,   Gold, -16, 14, 2, 16);         // flag pole
                Add(icon, Square,   Blue, -11, 18, 8, 6);          // flag
                break;
            case BuildingType.House:
                Add(icon, Square,   Wall, 0, -7, 22, 16);
                Add(icon, Triangle, Roof, 0, 7, 26, 14);
                Add(icon, Square,   Dark, 0, -9, 6, 10);
                break;
            case BuildingType.Barracks:
                Add(icon, Square, Prims.Hex(0x9a9486), 0, -4, 30, 22);
                Add(icon, Square, Steel, 0, 2, 4, 26, 40);         // crossed swords
                Add(icon, Square, Steel, 0, 2, 4, 26, -40);
                break;
            case BuildingType.ArcheryRange:
                Add(icon, Square, Wood, 0, -4, 30, 22);
                Add(icon, Circle, Red,  0, 2, 16, 16);             // target
                Add(icon, Ring,   Color.white, 0, 2, 16, 16);
                Add(icon, Circle, Color.white, 0, 2, 5, 5);
                break;
            case BuildingType.Stable:
                Add(icon, Square,   Wood, 0, -6, 30, 18);
                Add(icon, Triangle, Dark, 0, 9, 32, 12);
                Add(icon, Ring,     Steel, 0, -3, 14, 14);         // horseshoe-ish
                break;
            case BuildingType.Farm:
                Add(icon, Square, Green, 0, -2, 30, 26);
                Add(icon, Square, Prims.Hex(0x55792f), 0, 5, 30, 3);  // furrows
                Add(icon, Square, Prims.Hex(0x55792f), 0, -3, 30, 3);
                Add(icon, Square, Prims.Hex(0x55792f), 0, -11, 30, 3);
                break;
            case BuildingType.LumberCamp:
                Add(icon, Square, Wood, 0, -9, 26, 10);
                Add(icon, Square, Dark, 0, 2, 24, 6);              // stacked logs
                Add(icon, Square, Dark, 0, 10, 24, 6);
                break;
            case BuildingType.MiningCamp:
                Add(icon, Square, Wood,  0, -9, 26, 10);
                Add(icon, Circle, Stone, -6, 6, 13, 13);           // rocks
                Add(icon, Circle, Stone, 7, 9, 10, 10);
                Add(icon, Square, Steel, 6, -1, 4, 16, 45);        // pick
                break;
            case BuildingType.Mill:
                Add(icon, Square, Wall, 0, -6, 24, 18);
                Add(icon, Square, Wood, 0, 8, 24, 4, 45);          // windmill blades
                Add(icon, Square, Wood, 0, 8, 24, 4, -45);
                break;
            case BuildingType.Market:
                Add(icon, Square, Prims.Hex(0xc79a5b), 0, -6, 30, 16);
                Add(icon, Square, Red,  0, 5, 32, 8);              // awning
                Add(icon, Circle, Gold, 0, -5, 11, 11);           // coin
                break;
            case BuildingType.Castle:
                Add(icon, Square, Stone, 0, -2, 24, 30);
                Add(icon, Square, Stone, -8, 16, 6, 6);            // battlements
                Add(icon, Square, Stone, 0, 16, 6, 6);
                Add(icon, Square, Stone, 8, 16, 6, 6);
                Add(icon, Square, Dark,  0, -8, 8, 12);            // gate
                break;
            case BuildingType.Wall:
                Add(icon, Square, Stone, 0, 0, 32, 18);
                Add(icon, Square, Prims.Hex(0x8f8f8f), 0, 3, 32, 2);  // mortar
                Add(icon, Square, Prims.Hex(0x8f8f8f), 0, -3, 32, 2);
                break;
            case BuildingType.Gate:
                Add(icon, Square, Stone, -10, -2, 8, 26);          // pillars
                Add(icon, Square, Stone, 10, -2, 8, 26);
                Add(icon, Square, Stone, 0, 12, 30, 6);            // lintel
                Add(icon, Square, Prims.Hex(0x33291c), 0, -4, 11, 18); // opening
                break;
            case BuildingType.Dock:
                Add(icon, Square, Wood,  0, -5, 28, 12);           // platform
                Add(icon, Square, Dark, -8, -12, 4, 10);           // pile left
                Add(icon, Square, Dark,  8, -12, 4, 10);           // pile right
                Add(icon, Square, Prims.Hex(0x2a5db0), 0, -16, 28, 5); // water line
                break;
        }
    }

    public static void Tech(RectTransform icon, TechType t)
    {
        // Common badge backing.
        Add(icon, Circle, Prims.Hex(0x33363c), 0, 0, 38, 38);
        Add(icon, Ring,   Gold, 0, 0, 38, 38);

        switch (t)
        {
            case TechType.FeudalAge:
            case TechType.CastleAge:
                Add(icon, Triangle, Gold, 0, 3, 22, 18);           // up arrow
                Add(icon, Square,   Gold, 0, -8, 6, 10);
                break;
            case TechType.Forging:
            case TechType.ManAtArms:
            case TechType.Longswordsman:
                Add(icon, Square, Steel, 0, 4, 5, 24);             // sword
                Add(icon, Square, Dark,  0, -8, 14, 4);
                break;
            case TechType.Fletching:
            case TechType.Bodkin:
            case TechType.Crossbowman:
                Add(icon, Square,   Wood,  -2, 0, 24, 3);          // arrow
                Add(icon, Triangle, Steel, 11, 0, 9, 9, -90);
                break;
            case TechType.ScaleMail:
            case TechType.Bloodlines:
            case TechType.Cavalier:
                Add(icon, Circle, Shield, 0, 2, 22, 22);           // shield
                Add(icon, Ring,   Steel, 0, 2, 22, 22);
                break;
            case TechType.DoubleBitAxe:
                Add(icon, Square,   Dark,  0, -4, 4, 22);          // axe handle
                Add(icon, Triangle, Steel, 6, 6, 14, 12, 90);      // axe head
                break;
            case TechType.Wheelbarrow:
                Add(icon, Ring,   Dark, 0, -2, 20, 20);            // wheel
                Add(icon, Circle, Dark, 0, -2, 6, 6);
                break;
        }
    }

    public enum CmdIcon { Stop, AttackMove, Garrison }

    /// <summary>Generic unit-command icons (stop, attack-move, garrison eject).</summary>
    public static void Command(RectTransform icon, CmdIcon c)
    {
        switch (c)
        {
            case CmdIcon.Stop:
                Add(icon, Circle, Red, 0, 0, 30, 30);
                Add(icon, Square, Color.white, 0, 0, 14, 14);   // stop emblem
                break;
            case CmdIcon.AttackMove:
                Add(icon, Circle, Shield, 0, 0, 32, 32);
                Add(icon, Ring,   Steel, 0, 0, 32, 32);
                Add(icon, Square, Steel, 6, 4, 4, 22, -30);     // sword
                Add(icon, Square, Dark,  2, -6, 12, 4, -30);
                break;
            case CmdIcon.Garrison:                              // eject: keep + up arrow
                Add(icon, Square,   Steel, 0, -5, 24, 16);      // building
                Add(icon, Square,   Dark,  0, -7, 8, 10);       // doorway
                Add(icon, Triangle, Gold,  0, 10, 18, 14);      // units leaving (up)
                break;
        }
    }

    /// <summary>Market trade icon: source swatch → gold arrow → target swatch.</summary>
    public static void Market(RectTransform icon, ResourceKind kind, bool buy)
    {
        Color from = buy ? Gold : ResColor(kind);
        Color to   = buy ? ResColor(ResourceKind.Food) : Gold;
        Add(icon, Square,   from, -11, 0, 13, 13);
        Add(icon, Triangle, Steel, 0, 0, 12, 12, -90);            // arrow →
        Add(icon, Square,   to, 11, 0, 13, 13);
    }

    static Color ResColor(ResourceKind k) => k switch
    {
        ResourceKind.Food  => Red,
        ResourceKind.Wood  => Wood,
        ResourceKind.Gold  => Gold,
        ResourceKind.Stone => Stone,
        _ => Color.white,
    };

    // ── Shape placement ──────────────────────────────────────────────────────────

    static Image Add(RectTransform parent, Sprite s, Color c, float x, float y, float w, float h, float rotZ = 0f)
    {
        var go = new GameObject("Shape");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        if (rotZ != 0f) rt.localRotation = Quaternion.Euler(0, 0, rotZ);
        var img = go.AddComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;     // icons never intercept the button click
        return img;
    }

    // ── Sprite generation ────────────────────────────────────────────────────────

    static Sprite ToSprite(Texture2D tex)
    {
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeSquare()
    {
        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
        var px = new Color32[Res * Res];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    static Sprite MakeCircle(bool ring)
    {
        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
        float c = (Res - 1) / 2f, rOut = c, rIn = c * 0.62f;
        var px = new Color32[Res * Res];
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                bool on = ring ? (d <= rOut && d >= rIn) : d <= rOut;
                px[y * Res + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }

    static Sprite MakeTriangle()
    {
        // Filled triangle, apex at top-centre, base along the bottom edge.
        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
        var px = new Color32[Res * Res];
        for (int y = 0; y < Res; y++)
        {
            float t = y / (float)(Res - 1);          // 0 bottom → 1 top
            float halfWidth = (1f - t) * (Res / 2f); // shrinks toward apex
            float cx = (Res - 1) / 2f;
            for (int x = 0; x < Res; x++)
            {
                bool on = Mathf.Abs(x - cx) <= halfWidth;
                px[y * Res + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(px);
        return ToSprite(tex);
    }
}
