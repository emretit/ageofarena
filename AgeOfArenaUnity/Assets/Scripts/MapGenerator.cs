using UnityEngine;

/// <summary>
/// N10.rms: Map archetype definitions and seed-deterministic placement helpers.
/// N2.mapgen: Pure static class — no MonoBehaviour, no GameObject creation.
/// WorldRoot reads the active archetype and delegates placement to its own methods.
///
/// Archetypes parameterise: base layout, coastal forest belt, interior groves,
/// and resource distribution. Terrain disc shape (LandRadius=92) is fixed.
///
/// Adding a new map = one static Archetype field + one switch case in Get().
/// </summary>
public enum MapType { Arena, Arabia, BlackForest, Islands, Nomad }

public static class MapGenerator
{
    public struct Archetype
    {
        // Human-readable name shown in the setup screen.
        public string displayName;

        // Coastal forest ring [coastInner .. coastOuter]. Wider band = harder to
        // cross the edges. coastClusters controls angular density of the ring.
        public float  coastInner;
        public float  coastOuter;
        public int    coastClusters;

        // Starting base centres (world XZ). Array length must be >= MaxTeams (4).
        public Vector3[] basePositions;

        // Interior grove scatter: positions of cluster centres + scatter radius.
        // These trees are harvestable (no NavMesh obstacle), adding wood and visual noise.
        public Vector3[] groveCenters;
        public float     groveRadius;

        // Per-base bonus resources beyond the standard 2-gold/1-stone layout.
        public bool extraGoldPerBase;
        public bool extraStonePerBase;

        // Contested centre mines (gold + stone) placed symmetrically around origin.
        public int contestedGoldMines;
        public int contestedStoneMines;

        // When true WorldRoot forces GameMode.Nomad (no base buildings).
        public bool forceNomad;
    }

    // ── Archetype table ────────────────────────────────────────────────────────

    public static readonly Archetype Arena = new Archetype
    {
        displayName      = "Arena",
        coastInner       = 76f,
        coastOuter       = 91f,
        coastClusters    = 104,
        basePositions    = new[] { new Vector3( 0,0,-58), new Vector3( 0,0, 58),
                                   new Vector3(-58,0,  0), new Vector3(58,0,  0) },
        groveCenters     = new[] { new Vector3(0,0,34), new Vector3(0,0,-34),
                                   new Vector3(34,0,0), new Vector3(-34,0, 0) },
        groveRadius      = 4f,
        contestedGoldMines  = 2,
        contestedStoneMines = 2,
    };

    // Open map — thin coastal strip, sparse trees, many centre mines.
    public static readonly Archetype Arabia = new Archetype
    {
        displayName      = "Arabistan",
        coastInner       = 85f,
        coastOuter       = 91f,
        coastClusters    = 48,
        basePositions    = new[] { new Vector3( 0,0,-52), new Vector3( 0,0, 52),
                                   new Vector3(-52,0,  0), new Vector3(52,0,  0) },
        groveCenters     = new[] { new Vector3(20,0,20), new Vector3(-20,0,-20) },
        groveRadius      = 5f,
        contestedGoldMines  = 4,
        contestedStoneMines = 1,
    };

    // Chokepoint map — dense coastal ring + crowded interior groves.
    public static readonly Archetype BlackForest = new Archetype
    {
        displayName      = "Kara Orman",
        coastInner       = 72f,
        coastOuter       = 91f,
        coastClusters    = 136,
        basePositions    = new[] { new Vector3(-52,0,-52), new Vector3( 52,0, 52),
                                   new Vector3(-52,0, 52), new Vector3( 52,0,-52) },
        groveCenters     = new[] { new Vector3(0,0,0),
                                   new Vector3(30,0, 0), new Vector3(-30,0,  0),
                                   new Vector3( 0,0,30), new Vector3(  0,0,-30),
                                   new Vector3(20,0,20), new Vector3(-20,0,-20) },
        groveRadius      = 9f,
        extraGoldPerBase  = true,
        extraStonePerBase = true,
        contestedGoldMines  = 1,
        contestedStoneMines = 1,
    };

    // Naval map — diagonal far corners, minimal land forest.
    public static readonly Archetype Islands = new Archetype
    {
        displayName      = "Adalar",
        coastInner       = 87f,
        coastOuter       = 91f,
        coastClusters    = 28,
        basePositions    = new[] { new Vector3(-60,0,-60), new Vector3( 60,0, 60),
                                   new Vector3(-60,0, 60), new Vector3( 60,0,-60) },
        groveCenters     = new[] { new Vector3(0,0,0) },
        groveRadius      = 3f,
        contestedGoldMines  = 2,
        contestedStoneMines = 2,
    };

    // No-base scatter — same open terrain as Arabia, GameMode.Nomad enforced.
    public static readonly Archetype Nomad = new Archetype
    {
        displayName      = "Göçebe",
        coastInner       = 84f,
        coastOuter       = 91f,
        coastClusters    = 56,
        basePositions    = new[] { new Vector3(-38,0,-38), new Vector3( 38,0, 38),
                                   new Vector3(-38,0, 38), new Vector3( 38,0,-38) },
        groveCenters     = new[] { new Vector3(0,0,0),  new Vector3(25,0, 0),
                                   new Vector3(-25,0,0), new Vector3(0,0,25) },
        groveRadius      = 5f,
        contestedGoldMines  = 4,
        contestedStoneMines = 2,
        forceNomad        = true,
    };

    // ── Lookup ─────────────────────────────────────────────────────────────────

    public static Archetype Get(MapType type) => type switch
    {
        MapType.Arabia      => Arabia,
        MapType.BlackForest => BlackForest,
        MapType.Islands     => Islands,
        MapType.Nomad       => Nomad,
        _                   => Arena,
    };

    public static string DisplayName(MapType type) => Get(type).displayName;
}
