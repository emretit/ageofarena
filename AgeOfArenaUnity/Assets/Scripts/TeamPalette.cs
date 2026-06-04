using UnityEngine;

/// <summary>
/// Single-source team colour palette (N4.palette). Every team tint — unit/building
/// materials, minimap blips, relic orbs, monk-conversion recolour, training-queue
/// banners, diplomacy rows — reads from here, so N-team (N5) and civ work stay
/// consistent and adding a 5th+ player needs no new colour literal.
/// Order follows AoE2's player colours: blue, red, green, yellow, teal, purple, grey, orange.
/// Index == teamId.
/// </summary>
public static class TeamPalette
{
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

    /// Number of distinct team colours available (max simultaneous teams without wrap).
    public static int Count => _colors.Length;

    /// Team tint for teamId; wraps safely for any int so it never throws (out-of-range
    /// teams just reuse a colour rather than crashing the renderer path).
    public static Color For(int teamId)
        => _colors[((teamId % _colors.Length) + _colors.Length) % _colors.Length];
}
