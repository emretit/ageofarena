using UnityEngine;

/// <summary>
/// Classic RTS Fog of War — visual only (server-side AI/combat see everything).
///
/// A 128×128 CPU <see cref="Texture2D"/> covers the 120×120 world.  Each frame it
/// is repainted from team-0 sight circles; the result is uploaded to the GPU and
/// read by <see cref="FogOfWar"/> (Custom/FogOfWar) on the ground mesh.
/// Enemy unit and building renderers are toggled on/off every
/// <see cref="VisCheckInterval"/> seconds based on whether their grid cell is
/// currently lit.
///
/// Three visibility tiers (stored in the Red channel):
///   0   — unexplored (never seen):  ground is black.
///   70  — shroud (explored, not now visible): ground is dim.
///   255 — currently visible: ground at full colour.
/// </summary>
public class FogOfWarSystem : MonoBehaviour
{
    // ── Constants ─────────────────────────────────────────────────────────────
    const int   TexSize  = 128;
    const float WorldHalf = 60f;   // world is -60..+60 on X and Z
    const float WorldSize = WorldHalf * 2f;
    const float PixPerUnit = TexSize / WorldSize;  // pixels per world-unit (~1.067)

    const float VisCheckInterval = 0.5f;

    /// <summary>FOWD: Master switch — defaults to true (classic AoE2 fog). Set false to
    /// reveal the whole map (no fog); Init stays inert and Update no-ops.</summary>
    public bool fogEnabled = true;

    static readonly Color32 Black  = new Color32(0,   0,   0,   255);
    static readonly Color32 Shroud = new Color32(70,  70,  70,  255);
    static readonly Color32 Lit    = new Color32(255, 255, 255, 255);

    // ── State ─────────────────────────────────────────────────────────────────
    Texture2D _fogTex;

    /// <summary>MMTR: the fog texture (null if fog disabled). MinimapSystem reads this
    /// to overlay explored/unexplored areas on the minimap.</summary>
    public Texture2D FogTexture => fogEnabled ? _fogTex : null;
    Color32[] _pixels;
    byte[]    _explored;   // 0 = never seen, 1 = previously seen (shroud)

    float _visTimer;

    // ── Init / Reset ──────────────────────────────────────────────────────────

    /// <summary>
    /// Must be called once after the world is built.  Replaces the ground
    /// renderer's material with one that reads from the fog texture.
    /// </summary>
    public void Init(MeshRenderer groundRenderer)
    {
        // Fog disabled: leave the plain green ground in place and reveal everything.
        // Update() no-ops (guarded below), so enemy renderers are never hidden.
        if (!fogEnabled) return;

        _fogTex = new Texture2D(TexSize, TexSize, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name       = "FogOfWarTex",
        };
        _pixels   = new Color32[TexSize * TexSize];
        _explored = new byte[TexSize * TexSize];

        var fogMat = new Material(Shader.Find("Custom/FogOfWar"));
        fogMat.color = Prims.Hex(0x5b8c3e);            // match original ground green
        fogMat.SetTexture("_FogTex", _fogTex);
        groundRenderer.sharedMaterial = fogMat;
        groundRenderer.receiveShadows = true;

        // Start completely dark; the first Update() will reveal the TC area.
        Reset();
    }

    /// <summary>Clear explored state and repaint to black.  Called on Restart via
    /// <see cref="GameBootstrap"/> (the whole WorldRoot is rebuilt, so this system
    /// is recreated fresh — Reset() is here for manual/test use).</summary>
    public void Reset()
    {
        if (_explored == null) return;
        System.Array.Clear(_explored, 0, _explored.Length);
        for (int i = 0; i < _pixels.Length; i++) _pixels[i] = Black;
        _fogTex.SetPixels32(_pixels);
        _fogTex.Apply(false);
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (!fogEnabled) return;
        var gm = GameManager.Instance;
        if (gm == null || _fogTex == null) return;

        TickFogTexture(gm);

        _visTimer -= Time.deltaTime;
        if (_visTimer <= 0f)
        {
            _visTimer = VisCheckInterval;
            TickEnemyVisibility(gm);
        }
    }

    void TickFogTexture(GameManager gm)
    {
        // 1. Reset to shroud / unexplored
        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = _explored[i] > 0 ? Shroud : Black;

        // 2. Paint sight circles for player (team 0) + allied teams
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || !gm.IsAllied(0, u.teamId)) continue;
            // N10.minimap: elevation-aware sight — units on higher terrain see up to +20% farther.
            float elevBonus = 1f + Mathf.Clamp01(WorldRoot.GetHeight(
                u.transform.position.x, u.transform.position.z) / 1.4f) * 0.20f;
            PaintCircle(u.transform.position, UnitSight(u.type) * elevBonus);
        }

        // 3. Paint sight circles for player (team 0) + allied buildings
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || !gm.IsAllied(0, b.teamId)) continue;
            PaintCircle(b.transform.position, BuildingSight(b.type));
        }

        // 4. Update persistent explored map
        for (int i = 0; i < _pixels.Length; i++)
            if (_pixels[i].r == 255) _explored[i] = 1;

        _fogTex.SetPixels32(_pixels);
        _fogTex.Apply(false);
    }

    /// <summary>Stamp a filled circle of <see cref="Lit"/> pixels at the given
    /// world position with the given sight radius (world units).</summary>
    void PaintCircle(Vector3 worldPos, float radiusU)
    {
        float rp  = radiusU * PixPerUnit;   // radius in pixels
        float rp2 = rp * rp;
        int   cx  = WorldToPixel(worldPos.x);
        int   cz  = WorldToPixel(worldPos.z);
        int   ext = Mathf.CeilToInt(rp) + 1;

        int x0 = Mathf.Max(0, cx - ext),   x1 = Mathf.Min(TexSize - 1, cx + ext);
        int z0 = Mathf.Max(0, cz - ext),   z1 = Mathf.Min(TexSize - 1, cz + ext);

        for (int z = z0; z <= z1; z++)
        {
            int rowOff = z * TexSize;
            float dz = z - cz;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx;
                if (dx * dx + dz * dz <= rp2)
                    _pixels[rowOff + x] = Lit;
            }
        }
    }

    // ── Enemy visibility ──────────────────────────────────────────────────────

    /// <summary>Show/hide enemy units and buildings based on whether their
    /// map cell is currently lit (r == 255).  Runs every 0.5 s to amortize cost.
    /// Allied units are always visible regardless of fog.</summary>
    void TickEnemyVisibility(GameManager gm)
    {
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || gm.IsAllied(0, u.teamId)) continue;
            SetRenderersEnabled(u.gameObject, IsLit(u.transform.position));
        }
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || gm.IsAllied(0, b.teamId)) continue;
            SetRenderersEnabled(b.gameObject, IsLit(b.transform.position));
        }
    }

    bool IsLit(Vector3 worldPos)
    {
        int x = Mathf.Clamp(WorldToPixel(worldPos.x), 0, TexSize - 1);
        int z = Mathf.Clamp(WorldToPixel(worldPos.z), 0, TexSize - 1);
        return _pixels[z * TexSize + x].r == 255;
    }

    static void SetRenderersEnabled(GameObject go, bool on)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++) rs[i].enabled = on;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static int WorldToPixel(float world)
        => Mathf.RoundToInt((world + WorldHalf) * PixPerUnit);

    static float UnitSight(UnitType t) => t switch
    {
        UnitType.Scout     => 13f, // recon unit: widest sight, reveals the most fog
        UnitType.Cavalry   => 9f,
        UnitType.Archer    => 8f,
        UnitType.Militia   => 7f,
        UnitType.Villager  => 5f,
        UnitType.Trebuchet => 4f,
        _                  => 5f,
    };

    static float BuildingSight(BuildingType t) => t switch
    {
        BuildingType.TownCenter                                               => 10f,
        BuildingType.Castle                                                   => 8f,
        BuildingType.Barracks or BuildingType.ArcheryRange or BuildingType.Stable => 7f,
        _                                                                     => 5f,
    };
}
