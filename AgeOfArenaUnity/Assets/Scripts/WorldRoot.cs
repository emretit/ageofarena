using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Builds the full 4-player arena at runtime: 4 walled bases in a diamond layout,
/// shared forest ring, central mines, NavMesh, and gameplay systems.
/// Team 0 (south) is player-controlled; teams 1-3 are AI-controlled.
/// </summary>
public class WorldRoot : MonoBehaviour
{
    // Rectangular castle wall ring around each base (half-extents from the centre).
    const float WallHalfX   = 15f;
    const float WallHalfZ   = 13f;
    const float WallHeight  = 3.5f;
    // Kenney "Castle" modular piece tuning — calibrated against the imported FBX bounds
    // (native sizes at scale 1: wall 1.0 wide ×1.31 tall, gate 0.66 wide ×0.91 tall,
    // tower base/mid 1.01 tall each).
    const float WallSegW    = 2f;    // world width of one wall piece at WallScale
    const float WallScale   = 2f;    // uniform scale for wall Kenney models
    const float TowerScale  = 2.2f;  // corner towers a touch larger than the wall
    const float GateScale   = 3f;    // gate ≈2 units wide ×2.7 tall → fills one cell
    const float GateWidth   = 2f;    // visual gate piece footprint on the front edge
    const float GateOpening = 4.5f;  // NavMesh gap left in the wall — wide enough that a
                                     // 0.5-radius agent paths cleanly through the gate
    const float WallModelYaw = 0f;   // extra yaw if the FBX's length axis isn't +X

    // N10.rms: active archetype; set from mapType in Build(). Default = Arena.
    MapGenerator.Archetype _arch = MapGenerator.Arena;

    // Convenience alias for instance methods — keeps all call sites unchanged.
    Vector3[] BasePositions => _arch.basePositions;

    // Static cache so SampleTerrainNorm (static) can read base positions for terrain flattening.
    static Vector3[] _basePositions = MapGenerator.Arena.basePositions;

    // Team tints now come from the single-source TeamPalette (N4.palette); this
    // indexer keeps the existing TeamColors[i] call sites working unchanged.
    sealed class TeamColorsProxy { public Color this[int i] => TeamPalette.For(i); }
    static readonly TeamColorsProxy TeamColors = new TeamColorsProxy();

    static readonly Color RoofColor = Prims.Hex(0xa6402f);

    MeshRenderer _groundRenderer;  // saved for FogOfWarSystem.Init

    // N8.terrain: seed used by the heightmap (set in Build() before SetupGround).
    static int _mapSeed;

    // ── Island geometry (AoE2 "Arena island": round land ringed by ocean) ──────
    const float LandRadius  = 92f;   // grass disc radius (coastline)
    const float BeachRadius = 94f;   // thin sand rim just past the grass
    const float OceanHalf   = 116f;  // ocean plane half-extent (square sea frame)
    const float OceanDepth  = 0.25f; // ocean sits just below the flat (y=0) terrain so the
                                     // sea never z-fights up through flattened base/lane zones
                                     // (otherwise the blue plane bleeds through as fake "lakes")
    const float CoastInner  = 76f;   // coastal forest belt starts beyond the base back walls
    const float CoastOuter  = 91f;   // …and packs a thick belt up to the shoreline

    // Land disc mesh, reused as an exact NavMesh build source so the walkable area is a
    // true circle (no box approximation) and the sea is simply its inverse.
    Mesh _landMesh;

    // Not-walkable NavMesh sources collected while building (base walls minus their
    // gate, corner towers, coastal forest) — fed into the land bake so walls and the
    // forest ring actually block units. This is what makes it a real Arena.
    readonly List<NavMeshBuildSource> _navObstacles = new();

    int _navalAgentTypeId = -1;
    /// <summary>NavMesh agent type ID for Galley units (baked in BakeNavMesh).</summary>
    public int NavalAgentTypeId => _navalAgentTypeId;

    // Enemy brain flavours (index = teamId; slot 0 is the player and unused).
    static readonly AIPersonality[] Personalities =
    {
        AIPersonality.Balanced, // team 0 – player (unused)
        AIPersonality.Rusher,   // team 1 – red: early aggression
        AIPersonality.Boomer,   // team 2 – green: economy then big army
        AIPersonality.Balanced, // team 3 – yellow: steady
    };

    /// <summary>Seed for the procedural layout (trees/mines/relics). 0 = random each run.
    /// Change via the HUD or pass explicitly for reproducible maps.</summary>
    public int mapSeed;

    /// <summary>N10.rms: which map archetype to build. Set by GameBootstrap from CivSelectScreen.</summary>
    public MapType mapType = MapType.Arena;

    public void Build()
    {
        // Keep simulating when the window is unfocused (alt-tab) so the AI/economy
        // don't freeze — also makes headless/automated runs behave.
        Application.runInBackground = true;

        // N10.rms: resolve archetype before anything uses BasePositions.
        _arch = MapGenerator.Get(mapType);
        _basePositions = _arch.basePositions; // static cache for SampleTerrainNorm

        // Seed Unity's legacy RNG so trees/mines/relics produce a reproducible layout.
        if (mapSeed == 0) mapSeed = UnityEngine.Random.Range(1, int.MaxValue);
        UnityEngine.Random.InitState(mapSeed);
        SimRandom.Seed(mapSeed); // N3: seed the deterministic simulation RNG from the same seed
        MarketSystem.Reset();    // restore base market prices (static — would otherwise carry over a previous match's drift)
        _mapSeed = mapSeed;      // N8.terrain: heightmap uses same seed for determinism

        AudioManager.Init();
        SetupEnvironment();
        SetupGround();
        var cam = SetupCamera();
        cam.bounds  = new Vector2(95f, 95f); // match bigger 200×200 map
        cam.maxSize = 42f;                   // allow wider zoom-out
        var gm  = SetupGameManager();
        gm.gameMode  = _arch.forceNomad ? GameMode.Nomad : GameBootstrap.NextGameMode;
        gm.difficulty = GameBootstrap.NextDifficulty;

        // VNOMAD: skip static base construction; villagers scatter mid-map.
        if (gm.gameMode != GameMode.Nomad)
            for (int i = 0; i < gm.TeamCount; i++)
                BuildBase(BasePositions[i], TeamColors[i], i);

        BuildCoastalForest();   // dense conifer wall hugging the shoreline (blocks units)
        BuildInteriorClumps();  // a few broadleaf groves in the open battlefield
        for (int i = 0; i < gm.TeamCount; i++)
            BuildBaseResources(BasePositions[i]); // gold/stone/berry/wood/fish inside each pocket
        BuildContestedResources(); // extra mines fought over in the centre
        BuildRelics();
        BakeNavMesh();
        // N16.path: build deterministic grid pathfinder after NavMesh is baked.
        GridPathfinder.Build(LandRadius);
        gm.cameraRig = cam;
        SetupGameplay(gm);
        cam.Init(BasePositions[0]);

        // FoW is initialised after the full scene (units, buildings, nodes) is up.
        gm.fow = gm.gameObject.AddComponent<FogOfWarSystem>();
        gm.fow.fogEnabled = false;   // TEMP: tüm harita açık (sis kapalı) — geri açmak için sil
        gm.fow.Init(_groundRenderer);

        // Post-processing: must run after camera is ready.
        SetupPostProcessing(cam.gameObject);

        // N15.checksum: load replay baseline if this is a verify run.
        var gm2 = GameManager.Instance;
        if (gm2?.checksum != null && !string.IsNullOrEmpty(GameBootstrap.ReplayBaseline))
            ChecksumSystem.LoadBaseline(gm2.checksum);
        // N17.replay: auto-open viewer if a replay is ready.
        ReplayViewer.TryAutoStart();

        // CIVS: prompt the player to pick a civilization over the freshly-built arena.
        // STRT: show setup screen on first run; ARES: skip on restart (PlayerCiv persists).
        if (GameBootstrap.PlayerCiv == Civilization.None)
            new GameObject("CivSelect").AddComponent<CivSelectScreen>();
    }

    // ── Environment ──────────────────────────────────────────────────────────

    void SetupEnvironment()
    {
        // Anti-aliasing — biggest single visual win on WebGL (forward path → hardware MSAA).
        // Runtime set overrides the active quality tier (WebGL default tier has AA=0).
        QualitySettings.antiAliasing  = 4;     // 4× MSAA
        QualitySettings.shadowDistance = 35f;  // tighter than 40 → sharper shadow texels for an ortho RTS
        QualitySettings.shadowCascades = 1;    // ortho top-down doesn't need cascades (small GPU win)

        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(transform, false);
        var sun = sunGo.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.color     = Prims.Hex(0xffe8c4);   // warmer afternoon sun
        sun.intensity = 1.05f;                 // calmer key — keeps albedo colors saturated, not blown out
        sun.shadows   = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(42f, 320f, 0f); // lower + off-camera → long, readable shadows

        // Dimmer fill so lit colors read at their true saturation (AoE2 is bright but *saturated*).
        RenderSettings.ambientMode        = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientIntensity   = 0.65f;
        RenderSettings.ambientSkyColor    = Prims.Hex(0xa9cfe0);
        RenderSettings.ambientEquatorColor= Prims.Hex(0x8f8a6a); // warm (not blue) fill on shaded faces
        RenderSettings.ambientGroundColor = Prims.Hex(0x564832);

        // Procedural sky → atmospheric horizon instead of a flat clear color.
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetColor("_SkyTint",     Prims.Hex(0x7fb0d8));
        sky.SetColor("_GroundColor", Prims.Hex(0x9a8b66));
        sky.SetFloat("_AtmosphereThickness", 0.7f);
        sky.SetFloat("_Exposure", 1.1f);
        RenderSettings.skybox = sky;
        RenderSettings.sun    = sun;

        RenderSettings.fog                = true;
        RenderSettings.fogColor           = Prims.Hex(0xc9e0ec);
        RenderSettings.fogMode            = FogMode.Linear;
        RenderSettings.fogStartDistance   = 55f;
        RenderSettings.fogEndDistance     = 160f;

        Camera.main?.gameObject.SetActive(false);
    }

    // Island ground: a glossy ocean plane (also the single raycast collider for the
    // whole frame) with a sand disc and a grass disc stacked on top. The grass disc is
    // the playable land; the ocean shows in the ring/corners around it.
    void SetupGround()
    {
        // ── Ocean (bottom layer, y=-OceanDepth) — large flat plane with animated water
        // shader. Sits just under the terrain's y=0 floor so flattened base/lane zones
        // don't z-fight the sea up through the land. Keeps its MeshCollider so naval
        // click-to-move raycasts resolve on the sea.
        var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "Ocean";
        ocean.transform.SetParent(transform, false);
        ocean.transform.localPosition = new Vector3(0f, -OceanDepth, 0f);
        ocean.transform.localScale = new Vector3(OceanHalf * 2f / 10f, 1f, OceanHalf * 2f / 10f);
        var oceanRend = ocean.GetComponent<MeshRenderer>();
        // N8.terrain: water shader with animated waves; fallback to flat material.
        var waterShader = Shader.Find("Custom/Water");
        if (waterShader != null)
        {
            var waterMat = new Material(waterShader);
            waterMat.SetColor("_Color",     Prims.Hex(0x2a70bc));
            waterMat.SetColor("_DeepColor", Prims.Hex(0x102a4a));
            oceanRend.sharedMaterial = waterMat;
        }
        else
        {
            oceanRend.sharedMaterial = Prims.Mat(Prims.Hex(0x2766a8), 0.1f, 0.82f);
        }
        oceanRend.receiveShadows = false;

        // ── Beach (sand rim, y=0.03) — flat-coloured disc poking out past the terrain.
        var sand = NewMeshObject("Beach", BuildDiscMesh(BeachRadius, 72, 0f),
            Prims.Mat(Prims.Hex(0x9c8350), 0f, 0.05f), 0.03f);
        sand.GetComponent<MeshRenderer>().receiveShadows = true;

        // ── N8.terrain: heightmap terrain mesh with biome texture (replaces flat disc).
        // Heights embedded in mesh vertices; base zones (near each base + center) stay at
        // y≈0 so existing buildings/units remain flush with the ground there.
        var biomeTex   = BuildBiomeTexture(512, LandRadius);
        var terrainMat = Prims.Mat(Color.white, 0f, 0.04f);
        terrainMat.mainTexture = biomeTex;
        _landMesh = BuildTerrainMesh(LandRadius, 28, 128); // 28 rings × 128 segs
        var ground = NewMeshObject("Ground", _landMesh, terrainMat, 0f);
        _groundRenderer = ground.GetComponent<MeshRenderer>();
        _groundRenderer.receiveShadows = true;
        // MeshCollider so terrain raycasts return actual terrain surface height.
        var mc = ground.AddComponent<MeshCollider>();
        mc.sharedMesh = _landMesh;
    }

    // Flat horizontal disc (triangle fan) facing +Y, parented at the origin. uvWorldScale
    // bakes tiling UVs from world XZ (0 = single flat colour, 1/16 = a tile every 16 units).
    static Mesh BuildDiscMesh(float radius, int segments, float uvWorldScale)
    {
        var verts = new Vector3[segments + 1];
        var uvs   = new Vector2[segments + 1];
        var tris  = new int[segments * 3];
        verts[0] = Vector3.zero;
        uvs[0]   = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            verts[i + 1] = p;
            uvs[i + 1]   = uvWorldScale > 0f ? new Vector2(p.x * uvWorldScale, p.z * uvWorldScale)
                                             : new Vector2(0.5f, 0.5f);
            int t = i * 3;
            tris[t]     = 0;
            tris[t + 1] = (i + 1) % segments + 1; // winding chosen so the normal faces +Y
            tris[t + 2] = i + 1;
        }
        var m = new Mesh { name = $"Disc_{radius:0}" };
        m.vertices  = verts;
        m.uv        = uvs;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    GameObject NewMeshObject(string name, Mesh mesh, Material mat, float y)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>
    /// Procedural multi-octave grass texture — subtle tonal + soil variation instead of
    /// a single flat green. Generated once at boot (~256 KB). Made seamless so the 8×8
    /// tiling shows no grid seams.
    /// </summary>
    static Texture2D BuildGroundTexture()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGB24, true)
        {
            wrapMode   = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[N * N];
        Color grassA = Prims.Hex(0x486830); // mid-value grass — ACES will lift it to vivid green
        Color grassB = Prims.Hex(0x5c7a3a);
        Color soil   = Prims.Hex(0x7a6645);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float n = Seamless(u, v, 6f)  * 0.6f
                        + Seamless(u, v, 18f) * 0.3f
                        + Seamless(u, v, 42f) * 0.1f;
                Color col = Color.Lerp(grassA, grassB, Mathf.Clamp01(n));
                float soilMask = Seamless(u, v, 9f); // sparse dirt patches
                if (soilMask > 0.66f) col = Color.Lerp(col, soil, (soilMask - 0.66f) * 3f);
                px[y * N + x] = col;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    /// <summary>Tileable Perlin: blends the four wrapped corners so edges match seamlessly.</summary>
    static float Seamless(float u, float v, float freq)
    {
        float a = Mathf.PerlinNoise(u * freq,        v * freq);
        float b = Mathf.PerlinNoise((u - 1f) * freq, v * freq);
        float c = Mathf.PerlinNoise(u * freq,        (v - 1f) * freq);
        float d = Mathf.PerlinNoise((u - 1f) * freq, (v - 1f) * freq);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // ── N8.terrain: heightmap helpers ────────────────────────────────────────

    /// <summary>
    /// World-space terrain height at (x, z). Returns 0 in flattened zones (bases, center)
    /// and up to 1.4 units in the elevated mid-ring between bases.
    /// Deterministic: same mapSeed → same terrain each restart.
    /// </summary>
    public static float GetHeight(float x, float z) => SampleTerrainNorm(x, z) * 1.4f;

    static float SampleTerrainNorm(float x, float z)
    {
        float n = VNoise(x * 0.026f, z * 0.026f, _mapSeed)       * 1.000f
                + VNoise(x * 0.065f, z * 0.065f, _mapSeed +  7)  * 0.500f
                + VNoise(x * 0.130f, z * 0.130f, _mapSeed + 13)  * 0.250f;
        n = n / 1.75f;
        n = n * n * (3f - 2f*n); // smoothstep: fewer but smoother hills

        float r = Mathf.Sqrt(x * x + z * z);
        n *= Mathf.Clamp01((LandRadius - r - 7f) / 11f);  // flatten near coast
        n *= Mathf.Clamp01((r - 10f) / 8f);               // flatten center pocket
        foreach (var bp in _basePositions)
        {
            float dx = x - bp.x, dz = z - bp.z;
            float d  = Mathf.Sqrt(dx * dx + dz * dz);
            n *= Mathf.Clamp01((d - 16f) / 10f);          // flatten near each base
        }
        return Mathf.Clamp01(n);
    }

    static float VNoise(float x, float z, int seed)
    {
        int   ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
        float fx = x - ix,             fz = z - iz;
        float ux = fx * fx * (3f - 2f * fx);
        float uz = fz * fz * (3f - 2f * fz);
        return Mathf.Lerp(
            Mathf.Lerp(NHash(ix,   iz,   seed), NHash(ix+1, iz,   seed), ux),
            Mathf.Lerp(NHash(ix,   iz+1, seed), NHash(ix+1, iz+1, seed), ux), uz);
    }

    static float NHash(int x, int z, int s)
    {
        unchecked
        {
            int h = x * 374761393 + z * 668265263 + s * 1234567891;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    Mesh BuildTerrainMesh(float radius, int rings, int segs)
    {
        var verts = new System.Collections.Generic.List<Vector3>(rings * segs + 1);
        var uvs   = new System.Collections.Generic.List<Vector2>(rings * segs + 1);
        var tris  = new System.Collections.Generic.List<int>(rings * segs * 6);

        verts.Add(new Vector3(0f, GetHeight(0f, 0f), 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int r = 1; r <= rings; r++)
        {
            float rad = radius * r / rings;
            for (int s = 0; s < segs; s++)
            {
                float a  = s / (float)segs * Mathf.PI * 2f;
                float wx = Mathf.Cos(a) * rad;
                float wz = Mathf.Sin(a) * rad;
                verts.Add(new Vector3(wx, GetHeight(wx, wz), wz));
                uvs.Add(new Vector2(wx / (radius * 2f) + 0.5f, wz / (radius * 2f) + 0.5f));
            }
        }

        for (int s = 0; s < segs; s++)
        {
            int n = (s + 1) % segs;
            tris.Add(0); tris.Add(1 + s); tris.Add(1 + n);
        }
        for (int r = 1; r < rings; r++)
        {
            int ib = 1 + (r-1) * segs;
            int ob = 1 + r     * segs;
            for (int s = 0; s < segs; s++)
            {
                int n = (s + 1) % segs;
                tris.Add(ib+s); tris.Add(ob+s); tris.Add(ib+n);
                tris.Add(ib+n); tris.Add(ob+s); tris.Add(ob+n);
            }
        }

        var m = new Mesh
        {
            name        = "Terrain",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static Texture2D BuildBiomeTexture(int N, float radius)
    {
        var tex = new Texture2D(N, N, TextureFormat.RGB24, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color[N * N];
        float invN = 1f / N;
        for (int py = 0; py < N; py++)
        for (int px = 0; px < N; px++)
        {
            float wx = (px * invN - 0.5f) * radius * 2f;
            float wz = (py * invN - 0.5f) * radius * 2f;
            float r  = Mathf.Sqrt(wx * wx + wz * wz);
            Color c  = r > radius - 0.5f
                ? new Color(0.13f, 0.32f, 0.11f)
                : TerrainBiomeColor(SampleTerrainNorm(wx, wz));
            pixels[py * N + px] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply(false);
        return tex;
    }

    static Color TerrainBiomeColor(float h)
    {
        if (h < 0.12f) return new Color(0.42f, 0.60f, 0.25f); // lush grass
        if (h < 0.38f) return new Color(0.50f, 0.56f, 0.31f); // dry grass
        if (h < 0.62f) return new Color(0.46f, 0.44f, 0.30f); // rocky grass
        return                 new Color(0.52f, 0.48f, 0.43f); // bare rock
    }

    IsometricCameraRig SetupCamera()
    {
        var camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.backgroundColor = Prims.Hex(0xa9d4e8);
        cam.clearFlags      = CameraClearFlags.Skybox;
        return camGo.AddComponent<IsometricCameraRig>();
    }

    void SetupPostProcessing(GameObject camGo)
    {
        // Post Process Layer on camera (detects global volumes on all layers).
        var ppLayer = camGo.AddComponent<PostProcessLayer>();
        ppLayer.volumeLayer = ~0;
        ppLayer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

        // Global volume — drives all effects at priority 1.
        var volGo = new GameObject("PostProcessVolume");
        volGo.transform.SetParent(transform, false);
        var vol = volGo.AddComponent<PostProcessVolume>();
        vol.isGlobal = true;
        vol.priority = 1;
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        vol.profile = profile;

        // Ambient Occlusion — objects "sit" on the ground, depth between buildings.
        var ao = profile.AddSettings<AmbientOcclusion>();
        ao.enabled.Override(true);
        ao.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
        ao.intensity.Override(0.35f);   // subtle grounding — AoE2 is bright, not murky
        ao.radius.Override(0.35f);
        ao.quality.Override(AmbientOcclusionQuality.Medium);
        ao.ambientOnly.Override(false);

        // Bloom — subtle glow on bright roof/flag/relic highlights.
        var bloom = profile.AddSettings<Bloom>();
        bloom.enabled.Override(true);
        bloom.intensity.Override(0.8f);
        bloom.threshold.Override(0.75f);
        bloom.softKnee.Override(0.5f);
        bloom.fastMode.Override(true);

        // Color Grading — biggest single "pro vs. amatör" delta.
        var cg = profile.AddSettings<ColorGrading>();
        cg.enabled.Override(true);
        cg.tonemapper.Override(Tonemapper.ACES);
        cg.contrast.Override(12f);
        cg.saturation.Override(10f);         // ACES already boosts sat; extra 10 is enough
        cg.temperature.Override(5f);         // subtle warmth

        // Vignette — cinematic frame edge darkening.
        var vig = profile.AddSettings<Vignette>();
        vig.enabled.Override(true);
        vig.intensity.Override(0.28f);
        vig.smoothness.Override(0.5f);
        vig.rounded.Override(true);
    }

    // ── Base builder ─────────────────────────────────────────────────────────

    /// <param name="center">World position of this base's Town Center.</param>
    /// <param name="teamId">0 = player; 1-3 = AI-controlled teams.</param>
    void BuildBase(Vector3 center, Color teamColor, int teamId)
    {
        // Direction from this base toward the arena center — that's where the gate opens.
        Vector3 forward  = center.sqrMagnitude > 0.001f ? (-center).normalized : Vector3.forward;
        Vector3 backward = -forward;
        Vector3 right    = Vector3.Cross(Vector3.up, forward).normalized;

        float gateAngleDeg = Mathf.Atan2(-center.z, -center.x) * Mathf.Rad2Deg;

        var baseGo = new GameObject($"Base_T{teamId}");
        baseGo.transform.SetParent(transform, false);

        BuildWalls(baseGo.transform, center, gateAngleDeg);

        var gm = GameManager.Instance;

        var tc = BuildingFactory.Create(BuildingType.TownCenter, baseGo.transform, center, teamColor);
        SetBuildingTeam(tc, teamId);
        gm.RegisterBuilding(tc.GetComponent<BuildingEntity>());

        // Decorative fountain in the town square (native 2.0×0.28×2.0, scale 1.2).
        KenneyModels.Spawn("FantasyTown/fountain-round", baseGo.transform,
            center + forward * 3.5f, 1.2f);

        // Houses on the sides and behind the TC (provide population cap).
        var houses = new[]
        {
            BuildingFactory.Create(BuildingType.House, baseGo.transform, center + right * 5f,  RoofColor),
            BuildingFactory.Create(BuildingType.House, baseGo.transform, center - right * 5f,  RoofColor),
            BuildingFactory.Create(BuildingType.House, baseGo.transform, center + backward * 6f, RoofColor),
            BuildingFactory.Create(BuildingType.House, baseGo.transform, center + right * 5f + backward * 6f, RoofColor),
        };
        foreach (var h in houses)
        {
            SetBuildingTeam(h, teamId);
            gm.RegisterBuilding(h.GetComponent<BuildingEntity>());
        }

        // Barracks behind the houses.
        var barracks = BuildingFactory.Create(BuildingType.Barracks, baseGo.transform, center + backward * 8.5f, RoofColor);
        SetBuildingTeam(barracks, teamId);
        gm.RegisterBuilding(barracks.GetComponent<BuildingEntity>());

        // N14.aieco: Stable + ArcheryRange so AI has production buildings for cavalry/archers.
        var stable = BuildingFactory.Create(BuildingType.Stable, baseGo.transform, center + right * 8f + backward * 6f, RoofColor);
        SetBuildingTeam(stable, teamId);
        gm.RegisterBuilding(stable.GetComponent<BuildingEntity>());

        var archRange = BuildingFactory.Create(BuildingType.ArcheryRange, baseGo.transform, center - right * 8f + backward * 6f, RoofColor);
        SetBuildingTeam(archRange, teamId);
        gm.RegisterBuilding(archRange.GetComponent<BuildingEntity>());

        // Blacksmith: required for AI to research weapon/armor upgrades (Feudal+).
        var blacksmith = BuildingFactory.Create(BuildingType.Blacksmith, baseGo.transform, center + right * 4f + backward * 11f, RoofColor);
        SetBuildingTeam(blacksmith, teamId);
        gm.RegisterBuilding(blacksmith.GetComponent<BuildingEntity>());

        // Market + SiegeWorkshop: unlocks Castle-age trade and siege production.
        var market = BuildingFactory.Create(BuildingType.Market, baseGo.transform, center - right * 4f + backward * 11f, RoofColor);
        SetBuildingTeam(market, teamId);
        gm.RegisterBuilding(market.GetComponent<BuildingEntity>());

        var siege = BuildingFactory.Create(BuildingType.SiegeWorkshop, baseGo.transform, center + right * 12f + backward * 10f, RoofColor);
        SetBuildingTeam(siege, teamId);
        gm.RegisterBuilding(siege.GetComponent<BuildingEntity>());

        // Dock on the coast side of every base (backward = away from arena center, toward sea).
        // CoastInner ≈ 76; base centers sit ~84 units out, so backward*10 ≈ 94 — just past the
        // tree belt and onto the beach/water boundary, within NavMesh-snapping distance.
        var dock = BuildingFactory.Create(BuildingType.Dock, baseGo.transform, center + backward * 10f, teamColor);
        SetBuildingTeam(dock, teamId);
        gm.RegisterBuilding(dock.GetComponent<BuildingEntity>());
    }

    static void SetBuildingTeam(GameObject go, int teamId)
    {
        var be = go.GetComponent<BuildingEntity>();
        if (be != null) be.teamId = teamId;
    }

    // Rectangular castle perimeter built from Kenney "Castle" modular pieces
    // (wall + gate + square towers), with a procedural fallback per piece so the
    // base still renders if the asset pack is missing. Each wall cell and corner
    // tower also registers a not-walkable NavMesh source (BuildWallEdge / below), so
    // the pocket is sealed except for the single gate facing the arena centre.
    void BuildWalls(Transform parent, Vector3 center, float gateAngleDeg)
    {
        var wallsGo = new GameObject("Walls");
        wallsGo.transform.SetParent(parent, false);
        var wallMat = Prims.Mat(Prims.Hex(0x8a7d60), 0f, 0.05f); // matte stone — no specular glare
        var roofMat = Prims.Mat(Prims.Hex(0x7a5c3a), 0f, 0.05f);

        // Corners, clockwise from south-west.
        Vector3 sw = center + new Vector3(-WallHalfX, 0, -WallHalfZ);
        Vector3 se = center + new Vector3( WallHalfX, 0, -WallHalfZ);
        Vector3 ne = center + new Vector3( WallHalfX, 0,  WallHalfZ);
        Vector3 nw = center + new Vector3(-WallHalfX, 0,  WallHalfZ);

        // Each edge with its outward-normal heading (atan2(z,x) degrees) so the gate
        // lands on the edge facing the arena centre (matches gateAngleDeg).
        BuildWallEdge(wallsGo.transform, sw, se, -90f, gateAngleDeg, wallMat); // south
        BuildWallEdge(wallsGo.transform, se, ne,   0f, gateAngleDeg, wallMat); // east
        BuildWallEdge(wallsGo.transform, ne, nw,  90f, gateAngleDeg, wallMat); // north
        BuildWallEdge(wallsGo.transform, nw, sw, 180f, gateAngleDeg, wallMat); // west

        foreach (var corner in new[] { sw, se, ne, nw })
        {
            BuildCornerTower(wallsGo.transform, corner, wallMat, roofMat);
            AddObstacle(corner, 2.2f, 2.2f, 0f); // towers seal the wall corners
        }

        Prims.EnableShadows(wallsGo);
    }

    // Lay modular wall pieces end-to-end along one edge; on the gate edge leave a
    // centred gap and drop a single gate piece there.
    void BuildWallEdge(Transform parent, Vector3 a, Vector3 b, float normalDeg,
                       float gateAngleDeg, Material wallMat)
    {
        Vector3 dir = (b - a).normalized;
        float len   = Vector3.Distance(a, b);
        float yaw   = -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg + WallModelYaw;

        bool isGateEdge = Mathf.Abs(Mathf.DeltaAngle(normalDeg, gateAngleDeg)) < 45f;

        int n    = Mathf.Max(1, Mathf.RoundToInt(len / WallSegW));
        float step = len / n;
        int gateCells = isGateEdge ? Mathf.Clamp(Mathf.RoundToInt(GateOpening / step), 2, Mathf.Max(2, n - 2)) : 0;
        int gateStart = (n - gateCells) / 2;
        int gateEnd   = gateStart + gateCells; // exclusive

        for (int i = 0; i < n; i++)
        {
            if (gateCells > 0 && i >= gateStart && i < gateEnd)
            {
                if (i == gateStart)
                {
                    Vector3 gpos = a + dir * (step * (gateStart + gateCells * 0.5f));
                    // The gate model's span axis is perpendicular to the wall pieces',
                    // so rotate it an extra 90° to bridge the opening. No obstacle here →
                    // this is the one walkable opening into the pocket.
                    if (KenneyModels.Spawn("Castle/gate", parent, gpos, GateScale, yaw + 90f) == null)
                        FallbackGate(parent, gpos, yaw, wallMat);
                }
                continue;
            }

            Vector3 pos = a + dir * (step * (i + 0.5f));
            if (KenneyModels.Spawn("Castle/wall", parent, pos, WallScale, yaw) == null)
                FallbackWallSeg(parent, pos, yaw, step, wallMat);
            // Block this wall cell in the land NavMesh (everything but the gate opening).
            AddObstacle(pos, step + 0.3f, 1.6f, yaw);
        }
    }

    // Stacked square tower from the Castle kit (base + mid + tall spire roof);
    // procedural cylinder + cone otherwise. Native heights at scale 1: base/mid 1.01.
    void BuildCornerTower(Transform parent, Vector3 pos, Material wallMat, Material roofMat)
    {
        float s = TowerScale;
        if (KenneyModels.Spawn("Castle/tower-square-base", parent, pos, s) != null)
        {
            KenneyModels.Spawn("Castle/tower-square-mid",  parent, pos, s, 0f, 1.00f * s);
            KenneyModels.Spawn("Castle/tower-square-roof", parent, pos, s, 0f, 2.00f * s);
            return;
        }
        Prims.Cylinder(parent, new Vector3(pos.x, (WallHeight + 1.5f) * 0.5f, pos.z),
            0.9f, WallHeight + 1.5f, wallMat);
        Prims.Cone(parent, new Vector3(pos.x, WallHeight + 1.5f, pos.z),
            1.1f, 1.2f, 8, roofMat);
    }

    // Procedural stone wall block + merlon, matching the old ring's look.
    void FallbackWallSeg(Transform parent, Vector3 pos, float yaw, float segLen, Material wallMat)
    {
        var wall = Prims.Box(parent, new Vector3(pos.x, WallHeight * 0.5f, pos.z),
            new Vector3(segLen + 0.1f, WallHeight, 1f), wallMat);
        wall.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var merlon = Prims.Box(parent, new Vector3(pos.x, WallHeight + 0.45f, pos.z),
            new Vector3(0.8f, 0.9f, 0.8f), wallMat);
        merlon.transform.localRotation = Quaternion.Euler(0, yaw, 0);
    }

    // Procedural gate: two posts + a lintel leaving a passable opening.
    void FallbackGate(Transform parent, Vector3 pos, float yaw, Material wallMat)
    {
        var root = new GameObject("GateFallback");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var t = root.transform;
        foreach (var sx in new[] { -GateWidth * 0.5f, GateWidth * 0.5f })
            Prims.Box(t, new Vector3(sx, WallHeight * 0.5f + 0.3f, 0),
                new Vector3(1f, WallHeight + 0.6f, 1.2f), wallMat);
        Prims.Box(t, new Vector3(0, WallHeight + 0.7f, 0),
            new Vector3(GateWidth + 1f, 0.7f, 1.2f), wallMat);
    }

    // ── Resources ────────────────────────────────────────────────────────────

    // Per-base local frame: forward points at the arena centre (toward the gate),
    // backward at the coast, right is the lateral axis. For the cardinal base
    // positions these align with the world (and the axis-aligned castle walls).
    static void BaseFrame(Vector3 center, out Vector3 forward, out Vector3 backward, out Vector3 right)
    {
        forward  = center.sqrMagnitude > 0.001f ? (-center).normalized : Vector3.forward;
        backward = -forward;
        right    = Vector3.Cross(Vector3.up, forward).normalized;
    }

    // Thick conifer belt filling the whole shoreline (CoastInner..CoastOuter) — a
    // continuous natural wall, deliberately dense behind every base. Each tree is also a
    // NavMesh obstacle, so the belt blocks movement just like the castle walls do.
    void BuildCoastalForest()
    {
        var forest = new GameObject("CoastalForest");
        forest.transform.SetParent(transform, false);
        var gm = GameManager.Instance;

        float inner    = _arch.coastInner;
        float outer    = _arch.coastOuter;
        int   clusters = _arch.coastClusters;

        // Angular clusters around the full ring; each spans the belt's depth for a wall
        // that's several trees thick. No gaps — the forest hugs the coast all the way round.
        for (int c = 0; c < clusters; c++)
        {
            float clusterA = c / (float)clusters * Mathf.PI * 2f;
            int n = Random.Range(5, 9);
            for (int k = 0; k < n; k++)
            {
                float a = clusterA + Random.Range(-0.045f, 0.045f);
                float r = Random.Range(inner, outer);
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                gm.RegisterNode(ResourceFactory.Tree(forest.transform, pos, ResourceFactory.TreeKind.Conifer));
                AddObstacle(pos, 1.5f, 1.5f, 0f);
            }
        }

        // Floating lilies in the surrounding shallows + ground foliage on the interior grass.
        Decor.ScatterLilies(forest.transform, BeachRadius + 2f, OceanHalf - 6f, 60);
        Decor.Scatter(forest.transform, Vector3.zero, 6f, inner - 4f, 160);
    }

    // Broadleaf groves dotting the open battlefield. Harvestable, NOT obstacles.
    void BuildInteriorClumps()
    {
        var grove = new GameObject("Groves");
        grove.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        foreach (var ctr in _arch.groveCenters)
        {
            int n = Random.Range(4, 7);
            for (int k = 0; k < n; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(0f, _arch.groveRadius);
                gm.RegisterNode(ResourceFactory.Tree(grove.transform,
                    ctr + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                    ResourceFactory.TreeKind.Broadleaf));
            }
        }
    }

    // Base economy: self-sufficient pocket with standard + optional bonus resources.
    void BuildBaseResources(Vector3 center)
    {
        var root = new GameObject("BaseEco");
        root.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        BaseFrame(center, out var forward, out var backward, out var right);

        // Standard 2 gold at the front corners.
        gm.RegisterNode(ResourceFactory.GoldMine(root.transform, center + forward * 2f + right *  10f));
        gm.RegisterNode(ResourceFactory.GoldMine(root.transform, center + forward * 2f + right * -10f));
        // Archetype bonus gold (BlackForest: one extra mine behind TC).
        if (_arch.extraGoldPerBase)
            gm.RegisterNode(ResourceFactory.GoldMine(root.transform, center + backward * 5f + right * 8f));
        // Standard stone + berries.
        gm.RegisterNode(ResourceFactory.StoneMine(root.transform, center + right * 11f));
        if (_arch.extraStonePerBase)
            gm.RegisterNode(ResourceFactory.StoneMine(root.transform, center + right * -11.5f + forward * 0.5f));
        else
        {
            gm.RegisterNode(ResourceFactory.BerryBush(root.transform, center + right * -11f + forward * 1.5f));
            gm.RegisterNode(ResourceFactory.BerryBush(root.transform, center + right * -11f + backward * 2.5f));
        }
        // Starting wood line inside the back wall.
        for (int k = -2; k <= 2; k++)
            gm.RegisterNode(ResourceFactory.Tree(root.transform,
                center + backward * 10.5f + right * (k * 3.5f), ResourceFactory.TreeKind.Broadleaf));

        // Fish pond near the Dock (backward = toward coast) so FishingShips can reach it
        // from the naval NavMesh. The Dock sits at backward*10; pond at backward*8 is just
        // inside the tree belt — within FishingShip's extended gather range (4.0 for Food/naval).
        gm.RegisterNode(ResourceFactory.FishPond(root.transform, center + backward * 8f + right * -4f));
    }

    // Contested deposits in the open centre — prize that pulls armies out of the walls.
    void BuildContestedResources()
    {
        var root = new GameObject("ContestedMines");
        root.transform.SetParent(transform, false);
        var gm = GameManager.Instance;

        // Gold mines on one diagonal, stone on the other; extra mines spread outward.
        float[] offsets = { 17f, 26f, 35f };
        int gold  = _arch.contestedGoldMines;
        int stone = _arch.contestedStoneMines;
        for (int i = 0; i < gold && i < offsets.Length; i++)
        {
            float o = offsets[i];
            gm.RegisterNode(ResourceFactory.GoldMine(root.transform, new Vector3( o, 0,  o)));
            gm.RegisterNode(ResourceFactory.GoldMine(root.transform, new Vector3(-o, 0, -o)));
        }
        for (int i = 0; i < stone && i < offsets.Length; i++)
        {
            float o = offsets[i];
            gm.RegisterNode(ResourceFactory.StoneMine(root.transform, new Vector3( o, 0, -o)));
            gm.RegisterNode(ResourceFactory.StoneMine(root.transform, new Vector3(-o, 0,  o)));
        }
    }

    // Append a not-walkable box NavMesh source (area 1) at a world XZ. yawDeg lets wall
    // segments align with their edge; trees/towers pass 0 (square).
    void AddObstacle(Vector3 pos, float width, float depth, float yawDeg)
    {
        _navObstacles.Add(new NavMeshBuildSource
        {
            shape     = NavMeshBuildSourceShape.Box,
            size      = new Vector3(width, WallHeight + 1f, depth),
            transform = Matrix4x4.TRS(new Vector3(pos.x, 0f, pos.z),
                                      Quaternion.Euler(0f, yawDeg, 0f), Vector3.one),
            area      = 1,
        });
    }

    // Five contested relics near the map centre (clear of the ±8 mines), AoE2 parity:
    // one dead centre and four spread around it on the diagonals. Whoever holds them
    // earns a passive gold trickle.
    void BuildRelics()
    {
        var relics = new GameObject("Relics");
        relics.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3(  0, 0,   0)));
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3(-22, 0,  22)));
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3( 22, 0, -22)));
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3(-22, 0, -22)));
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3( 22, 0,  22)));
    }

    // ── NavMesh ───────────────────────────────────────────────────────────────

    // Two NavMeshes baked from the ground meshes themselves:
    //  • Land  = the grass disc (an exact circle) minus wall + coastal-forest obstacles.
    //  • Naval = the ocean plane minus the island disc → Galleys ring the whole coast.
    void BakeNavMesh()
    {
        const float agentHeight = 2f;
        var bounds = new Bounds(Vector3.zero,
            new Vector3(OceanHalf * 2f + 4f, agentHeight * 2f + 4f, OceanHalf * 2f + 4f));

        // NavMesh is baked from a FLAT disc, NOT the visual heightmap mesh (_landMesh). The
        // heightmap leaves small flat pockets (base/center, see SampleTerrainNorm) ringed by
        // terrain that rises ~1.4 over a few units — steeper than agentClimb (0.75). The
        // voxelizer then severs those pockets from the main mesh and agentRadius erosion wipes
        // them, so units spawned at the base sat OFF the NavMesh and ignored every move order.
        // Gameplay is on the y≈0 plane anyway; the heightmap is purely cosmetic.
        var flatLand = BuildDiscMesh(LandRadius, 128, 0f);

        // ── Land NavMesh (default agent): walkable on the disc, blocked by walls/forest.
        var landSources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape        = NavMeshBuildSourceShape.Mesh,
                sourceObject = flatLand,
                transform    = Matrix4x4.identity, // disc authored at the origin, y=0
                area         = 0,
            }
        };
        landSources.AddRange(_navObstacles); // base walls (minus gates) + coastal forest ring
        var landSettings = NavMesh.GetSettingsByIndex(0);
        var landData = NavMeshBuilder.BuildNavMeshData(
            landSettings, landSources, bounds, Vector3.zero, Quaternion.identity);
        if (landData != null) NavMesh.AddNavMeshData(landData);

        // ── Naval NavMesh (custom agent type): the sea ring around the island.
        var navalSettings = NavMesh.CreateSettings();
        navalSettings.agentRadius  = 0.5f;
        navalSettings.agentHeight  = 1.0f;
        navalSettings.agentClimb   = 0f;
        navalSettings.agentSlope   = 0f;
        _navalAgentTypeId = navalSettings.agentTypeID;

        var waterSources = new List<NavMeshBuildSource>
        {
            // Whole sea as a flat box (avoids relying on the built-in plane mesh being
            // CPU-readable in player builds)…
            new NavMeshBuildSource
            {
                shape     = NavMeshBuildSourceShape.Box,
                size      = new Vector3(OceanHalf * 2f, 0.2f, OceanHalf * 2f),
                transform = Matrix4x4.identity,
                area      = 0,
            },
            // …minus the island disc (same flat disc as the land bake), so naval units ring
            // the coast only.
            new NavMeshBuildSource
            {
                shape        = NavMeshBuildSourceShape.Mesh,
                sourceObject = flatLand,
                transform    = Matrix4x4.identity,
                area         = 1,
            },
        };
        var waterData = NavMeshBuilder.BuildNavMeshData(
            navalSettings, waterSources, bounds, Vector3.zero, Quaternion.identity);
        if (waterData != null) NavMesh.AddNavMeshData(waterData);
    }

    // ── Systems & Gameplay ────────────────────────────────────────────────────

    GameManager SetupGameManager()
    {
        var go = new GameObject("GameManager");
        go.transform.SetParent(transform, false);
        var gm = go.AddComponent<GameManager>();
        gm.gather        = go.AddComponent<GatherSystem>();
        gm.combat        = go.AddComponent<CombatSystem>();
        gm.buildingCombat = go.AddComponent<BuildingCombatSystem>();
        gm.garrison      = go.AddComponent<GarrisonSystem>();
        gm.build         = go.AddComponent<BuildSystem>();
        gm.placement     = go.AddComponent<BuildingPlacement>();
        gm.selection     = go.AddComponent<SelectionSystem>();
        gm.command       = go.AddComponent<CommandSystem>();
        gm.trainingQueue = go.AddComponent<TrainingQueue>();
        gm.research      = go.AddComponent<ResearchSystem>();
        gm.relicSystem   = go.AddComponent<RelicSystem>();
        gm.trading       = go.AddComponent<TradingSystem>();
        go.AddComponent<SaveSystem>();
        gm.hud           = go.AddComponent<HUD>();
        gm.minimap       = go.AddComponent<MinimapSystem>();
        gm.match         = go.AddComponent<MatchSystem>();
        gm.vfx           = go.AddComponent<VisualEffectSystem>();
        gm.triggers       = go.AddComponent<TriggerSystem>();   // N11.trig
        gm.scenarioEditor = go.AddComponent<ScenarioEditor>(); // N12.edit
        gm.campaignScreen = go.AddComponent<CampaignScreen>(); // N13.camp
        gm.tutorial       = go.AddComponent<TutorialSystem>(); // N13.tut
        gm.cmdRecorder    = go.AddComponent<CommandRecorder>(); // N3.cmdlog
        gm.checksum       = go.AddComponent<ChecksumSystem>(); // N15.checksum
        gm.lockstep       = go.AddComponent<LockstepSystem>(); // N16.lockstep
        gm.desync         = go.AddComponent<DesyncHandler>();  // N17.desync
        gm.transport      = go.AddComponent<TransportLayer>(); // N17.transport
        return gm;
    }

    void SetupGameplay(GameManager gm)
    {
        // CIVS: the player (team 0) keeps their chosen civ; only AI teams (1-3) are randomized.
        // GameBootstrap.PlayerCiv is None until the player picks via CivSelectScreen.
        var civValues = (Civilization[])System.Enum.GetValues(typeof(Civilization));
        gm.teamCivs[0] = GameBootstrap.PlayerCiv;
        for (int i = 1; i < gm.TeamCount; i++)
            gm.teamCivs[i] = civValues[Random.Range(1, civValues.Length)];

        var tcPos     = BasePositions[0];
        var teamColor = TeamColors[0];

        // Direction from player base toward arena center.
        Vector3 forward = (-tcPos).normalized;
        Vector3 right   = Vector3.Cross(Vector3.up, forward).normalized;

        // Drop-offs are now buildings: the Town Center (registered in BuildBase)
        // accepts all resources; players can build Lumber/Mining camps & Mill closer
        // to resources. See GatherSystem.NearestDropoff / BuildingDefs.AcceptsDropoff.
        gm.hud.Init(gm.resources);

        var unitsRoot = new GameObject("Units");
        unitsRoot.transform.SetParent(transform, false);

        // Villagers spawn in front of TC (toward gate / arena center).
        Vector3[] vPos =
        {
            tcPos + forward * 3.5f + right * -2.5f,
            tcPos + forward * 4.2f,
            tcPos + forward * 3.5f + right *  2.5f,
        };
        foreach (var p in vPos)
            gm.RegisterUnit(UnitFactory.Villager(unitsRoot.transform, p, teamColor));

        Vector3[] mPos = { tcPos + forward * 4f + right * 2f, tcPos + forward * 3.2f + right * 3.3f };
        foreach (var p in mPos)
            gm.RegisterUnit(UnitFactory.Militia(unitsRoot.transform, p, teamColor));

        // Islands map: give the player a starting FishingShip + Galley near the Dock.
        if (_arch.displayName == "Adalar")
        {
            int navalId = _navalAgentTypeId;
            Vector3 dockPos = tcPos + (-forward) * 10f; // matches Dock placement in BuildBase
            Vector3 seaDir  = dockPos.sqrMagnitude > 0.01f ? dockPos.normalized : Vector3.forward;
            gm.RegisterUnit(UnitFactory.FishingShip(unitsRoot.transform, dockPos + seaDir * 5f, teamColor, navalId));
            gm.RegisterUnit(UnitFactory.Galley(unitsRoot.transform, dockPos + seaDir * 8f, teamColor, navalId));
        }

        // Enemy garrisons (teams 1+): a starting army per base, plus an EnemyAI brain.
        for (int t = 1; t < gm.TeamCount; t++)
        {
            SpawnGarrison(gm, unitsRoot.transform, BasePositions[t], TeamColors[t], t);

            var personality = t < Personalities.Length ? Personalities[t] : Personalities[1];
            var aiGo = new GameObject($"EnemyAI_T{t}_{personality}");
            aiGo.transform.SetParent(transform, false);
            aiGo.AddComponent<EnemyAI>().Init(t, TeamColors[t], BasePositions[t], unitsRoot.transform, personality, mapType);
        }

        // popCap now derives from team-0 buildings (TC + Houses) via RecomputePop.
        gm.RecomputePop();

        // SAVF: if a save snapshot is pending, apply it (overrides default spawn).
        var pending = GameBootstrap.PendingLoad;
        if (pending != null)
        {
            GameBootstrap.PendingLoad = null; // consume
            ApplyPendingLoad(gm, unitsRoot.transform, pending);
            return; // skip game-mode post-setup (already captured in snapshot)
        }

        // ── Game-mode post-setup ─────────────────────────────────────────────────
        switch (gm.gameMode)
        {
            case GameMode.Deathmatch:
                ApplyDeathmatch(gm);
                break;
            case GameMode.Regicide:
                SpawnKings(gm, unitsRoot.transform);
                break;
            case GameMode.Nomad:
                SpawnNomad(gm, unitsRoot.transform);
                break;
            // ── N14/MODES: new rule-toggle modes ──
            case GameMode.EmpireWars:
                ApplyEmpireWars(gm);
                break;
            case GameMode.KingOfTheHill:
                gm.kothActive = true;
                break;
            case GameMode.SuddenDeath:
                gm.suddenDeath = true;
                break;
            case GameMode.Treaty:
                gm.treatyEndTime = 15f * 60f;   // 15 minutes of peace
                break;
            case GameMode.Turbo:
                gm.turboGatherMult = 3f;
                break;
        }

        // N17: wire TransportLayer.OnChecksumReceived → DesyncHandler.
        if (gm.transport != null && gm.desync != null)
        {
            gm.transport.OnChecksumReceived += remoteHash =>
            {
                uint local = gm.checksum?.LatestChecksum ?? 0u;
                gm.desync.CheckTick(gm.cmdRecorder?.Tick ?? 0, local, remoteHash);
            };
        }

        // N13.aow: inject challenge triggers after all systems are ready.
        ArtOfWarSystem.Setup(gm);
        // N13.camp: inject campaign mission triggers (overrides AoW if both set).
        CampaignSystem.Setup(gm);
        // N13.tut: first-game tutorial coach-marks (self-guards via PlayerPrefs DONE_KEY;
        // no-op on replays/scenario-test so it only fires on a fresh first match).
        if (GameBootstrap.PendingLoad == null && GameBootstrap.ReplayBaseline == null)
            gm.tutorial?.Init(gm, Camera.main);
    }

    // SAVF: restore a full game snapshot after arena rebuild (NavMesh already fresh).
    static void ApplyPendingLoad(GameManager gm, Transform unitsRoot, SaveSystem.SaveData data)
    {
        // Restore team resources, tech, civs.
        for (int t = 0; t < gm.TeamCount && t < data.teams.Length; t++)
        {
            var ts = data.teams[t];
            if (ts == null) continue;
            var r = gm.teamRes[t];
            r.food = ts.food; r.wood = ts.wood; r.gold = ts.gold; r.stone = ts.stone;
            gm.teamCivs[t] = (Civilization)ts.civ;
            foreach (int id in ts.techs)
                ResearchSystem.Apply((TechType)id, t);
        }

        // Remove default-spawned units (from SetupGameplay).
        for (int i = gm.units.Count - 1; i >= 0; i--)
        {
            var u = gm.units[i];
            if (u != null) Object.Destroy(u.gameObject);
        }
        gm.units.Clear();

        // Respawn saved units via the central dispatch — handles ALL unit types. The
        // old switch only knew ~10 types and turned everything else (Trebuchet, Monk,
        // uniques, and the King!) into a Villager, which silently broke Regicide saves.
        int navalId = Object.FindAnyObjectByType<WorldRoot>()?.NavalAgentTypeId ?? -1;
        foreach (var us in data.units)
        {
            if (us == null) continue;
            var pos = new Vector3(us.x, 0, us.z);
            UnitEntity e = UnitFactory.Spawn((UnitType)us.type, unitsRoot, pos, us.teamId, navalId);
            if (e != null)
            {
                // Restore veterancy/stance first, recompute maxHp WITHOUT refilling,
                // then restore the saved (possibly damaged) hp. Order matters: doing it
                // the other way heals a wounded veteran by its own veteran HP bonus.
                e.veteranRank = us.veteranRank;
                e.stance      = (AttackStance)us.stance;
                e.RecomputeMaxHp(fillOnIncrease: false);
                e.hp = Mathf.Min(us.hp, e.maxHp);
                if (us.isGarrisoned) e.isGarrisoned = true; // garrisoned state (hidden)
                gm.RegisterUnit(e);
            }
        }

        // Buildings: destroy the default set and re-create EVERY building from its snapshot.
        // The old code only position-matched saved buildings against the fixed set BuildBase
        // spawns (TC/houses/barracks/stable/archery), copying HP/rally — so any building the
        // player constructed during the saved game (Castle, towers, Market, Blacksmith,
        // Wonder, extra Houses…) silently VANISHED on load, and destroyed default buildings
        // came back at full HP. In Nomad mode (no BuildBase) it left zero buildings → the
        // player's own TC was never recreated → instant "TC destroyed" loss on load. Mirror
        // the unit path (wipe defaults, respawn from save) so the saved world is reproduced.
        for (int i = gm.buildings.Count - 1; i >= 0; i--)
        {
            var b = gm.buildings[i];
            if (b != null) Object.Destroy(b.gameObject);
        }
        gm.buildings.Clear();

        foreach (var bs in data.buildings)
        {
            if (bs == null) continue;
            var bpos = new Vector3(bs.x, 0, bs.z);
            var go = BuildingFactory.Create((BuildingType)bs.type, null, bpos, TeamPalette.For(bs.teamId));
            if (go == null) continue;
            var be = go.GetComponent<BuildingEntity>();
            if (be == null) continue;
            be.teamId = bs.teamId;
            if (bs.underConstruction)
            {
                // maxHp must be known to size buildProgress; set it explicitly (skips the
                // civ/tech HP-mult that Start() would apply, acceptable for the rare
                // mid-construction case) so BuildSystem resumes from the saved fraction.
                be.maxHp = BuildingEntity.MaxHpFor((BuildingType)bs.type);
                be.buildTime = BuildingDefs.Get((BuildingType)bs.type).buildTime;
                be.underConstruction = true;
                be.buildProgress = be.maxHp > 0f ? Mathf.Clamp01(bs.hp / be.maxHp) : 0f;
                be.transform.localScale = new Vector3(1f, Mathf.Lerp(0.05f, 1f, be.buildProgress), 1f);
            }
            // Set hp AFTER team (so Start's civ/tech maxHp mult resolves) but it survives
            // Start() because Start only fills hp when hp <= 0.
            be.hp = bs.hp;
            if (bs.hasRally)
            {
                be.hasRally   = true;
                be.rallyPoint = new Vector3(bs.rallyX, 0f, bs.rallyZ);
            }
            gm.RegisterBuilding(be);
        }

        gm.RecomputePop();

        // Relic ownership (relics are re-spawned at the same seed-deterministic positions).
        if (data.relics != null)
            foreach (var rs in data.relics)
            {
                if (rs.controllingTeam < 0) continue;
                var rpos = new Vector3(rs.x, 0, rs.z);
                foreach (var r in gm.relics)
                    if (r != null && Vector3.Distance(r.transform.position, rpos) < 2f)
                    { r.ForceControl(rs.controllingTeam); break; }
            }

        // Training queues + active research (match the owning building by position).
        if (data.queues != null && gm.trainingQueue != null)
            foreach (var qs in data.queues)
            {
                var bpos = new Vector3(qs.x, 0, qs.z);
                foreach (var b in gm.buildings)
                    if (b != null && Vector3.Distance(b.transform.position, bpos) < 3f)
                    { gm.trainingQueue.RestoreQueue(b, qs.types, qs.frontElapsed); break; }
            }
        if (data.research != null && gm.research != null)
            foreach (var resd in data.research)
            {
                var bpos = new Vector3(resd.x, 0, resd.z);
                foreach (var b in gm.buildings)
                    if (b != null && Vector3.Distance(b.transform.position, bpos) < 3f)
                    { gm.research.RestoreActive(b, resd.tech, resd.elapsed); break; }
            }

        // N11.trig: restore trigger state
        if (gm.triggers != null && data.triggers != null)
            gm.triggers.LoadSnapshot(data.triggers);
    }

    // VDEATH: all teams start with abundant resources.
    static void ApplyDeathmatch(GameManager gm)
    {
        for (int t = 0; t < gm.TeamCount; t++)
        {
            var r = gm.teamRes[t];
            r.food  = Mathf.Max(r.food,  20000);
            r.wood  = Mathf.Max(r.wood,  20000);
            r.gold  = Mathf.Max(r.gold,  10000);
            r.stone = Mathf.Max(r.stone,  5000);
        }
    }

    // N14/MODES: Empire Wars — all teams start at Castle Age with a solid eco base.
    static void ApplyEmpireWars(GameManager gm)
    {
        for (int t = 0; t < gm.TeamCount; t++)
        {
            ResearchSystem.Apply(TechType.FeudalAge, t);
            ResearchSystem.Apply(TechType.CastleAge, t);
            var r = gm.teamRes[t];
            r.food  = Mathf.Max(r.food,  2000);
            r.wood  = Mathf.Max(r.wood,  2000);
            r.gold  = Mathf.Max(r.gold,  1000);
            r.stone = Mathf.Max(r.stone,  500);
        }
    }

    // VREGI: one King unit per team; MatchSystem handles elimination on King death.
    void SpawnKings(GameManager gm, Transform unitsRoot)
    {
        for (int t = 0; t < gm.TeamCount; t++)
        {
            var pos = BasePositions[t] + new Vector3(0, 0, 1.5f);
            var king = UnitFactory.King(unitsRoot, pos, TeamColors[t], t);
            gm.RegisterUnit(king);
        }
    }

    // VNOMAD: no TC — scatter 6 villagers per team around the map centre.
    void SpawnNomad(GameManager gm, Transform unitsRoot)
    {
        for (int t = 0; t < gm.TeamCount; t++)
        {
            var dir   = (BasePositions[t] - Vector3.zero).normalized;
            var right = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < 6; i++)
            {
                float off = (i - 2.5f) * 3f;
                var pos = dir * 25f + right * off;
                var v = UnitFactory.Villager(unitsRoot, pos, TeamColors[t], t);
                gm.RegisterUnit(v);
            }
        }
    }

    void SpawnGarrison(GameManager gm, Transform parent, Vector3 center, Color color, int teamId)
    {
        Vector3 forward  = (-center).normalized;
        Vector3 backward = -forward;
        Vector3 right    = Vector3.Cross(Vector3.up, forward).normalized;

        // Starting militia (front of base)
        Vector3[] milPos =
        {
            center + forward * 4f + right * -2f,
            center + forward * 4.5f,
            center + forward * 4f + right *  2f,
        };
        foreach (var p in milPos)
            gm.RegisterUnit(UnitFactory.Militia(parent, p, color, teamId));

        // Starting villagers (behind TC, ready to gather)
        Vector3[] vilPos =
        {
            center + backward * 2f + right * -1.5f,
            center + backward * 2.5f,
            center + backward * 2f + right *  1.5f,
        };
        foreach (var p in vilPos)
            gm.RegisterUnit(UnitFactory.Villager(parent, p, color, teamId));

        // Islands map: enemy teams also start with a naval patrol unit.
        if (_arch.displayName == "Adalar")
        {
            int navalId = _navalAgentTypeId;
            Vector3 dockPos = center + backward * 10f;
            Vector3 seaDir  = dockPos.sqrMagnitude > 0.01f ? dockPos.normalized : Vector3.forward;
            gm.RegisterUnit(UnitFactory.Galley(parent, dockPos + seaDir * 7f, color, navalId));
        }
    }
}
