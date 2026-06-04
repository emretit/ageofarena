using UnityEngine;

/// <summary>
/// Single-source team colour palette (N4.palette). Every team tint — unit/building
/// materials, minimap blips, relic orbs, monk-conversion recolour, training-queue
/// banners, diplomacy rows — reads from here, so N-team (N5) and civ work stay
/// consistent and adding a 5th+ player needs no new colour literal.
/// Order follows AoE2's player colours: blue, red, green, yellow, teal, purple, grey, orange.
/// Index == teamId.
///
/// N9.a11y: When <see cref="AccessibilitySettings.ColorblindMode"/> is true, <see cref="For"/>
/// returns a deuteranopia-safe palette (orange replaces red; cyan replaces green) so
/// team 1 and team 2 are distinguishable without relying on hue alone.
/// </summary>
public static class TeamPalette
{
    // Standard AoE2 palette
    static readonly Color[] _colors =
    {
        Prims.Hex(0x1e5fcc), // 0 blue   — saturated AoE2 blue
        Prims.Hex(0xd42020), // 1 red    — deep red
        Prims.Hex(0x1e9e40), // 2 green  — vivid
        Prims.Hex(0xf0a010), // 3 yellow — warm gold
        Prims.Hex(0x16b8c8), // 4 teal
        Prims.Hex(0x9a30c8), // 5 purple
        Prims.Hex(0x808080), // 6 grey
        Prims.Hex(0xe07b18), // 7 orange
    };

    // N9.a11y: Deuteranopia-safe palette — replaces red with bright orange,
    // green with azure/cyan, keeping blue/yellow/teal/purple/grey/orange distinct.
    static readonly Color[] _colorblind =
    {
        Prims.Hex(0x0066cc), // 0 blue   (unchanged — fine for all types)
        Prims.Hex(0xe05c00), // 1 red    → vivid orange-red (safe vs green for CB)
        Prims.Hex(0x00a3cc), // 2 green  → azure/cyan (distinguishable from orange)
        Prims.Hex(0xf0c030), // 3 yellow (unchanged)
        Prims.Hex(0x009999), // 4 teal   (slightly muted)
        Prims.Hex(0x7744cc), // 5 purple (unchanged)
        Prims.Hex(0x999999), // 6 grey   (unchanged)
        Prims.Hex(0xff8800), // 7 orange (lighter — distinct from team-1 orange-red)
    };

    /// Number of distinct team colours available (max simultaneous teams without wrap).
    public static int Count => _colors.Length;

    /// Team tint for teamId; wraps safely for any int so it never throws (out-of-range
    /// teams just reuse a colour rather than crashing the renderer path).
    /// Returns colorblind-safe palette when <see cref="AccessibilitySettings.ColorblindMode"/> is on.
    public static Color For(int teamId)
    {
        var palette = AccessibilitySettings.ColorblindMode ? _colorblind : _colors;
        return palette[((teamId % palette.Length) + palette.Length) % palette.Length];
    }
}
