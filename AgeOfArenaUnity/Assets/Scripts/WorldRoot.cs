using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Builds the full 4-player arena at runtime: 4 walled bases in a diamond layout,
/// shared forest ring, central mines, NavMesh, and gameplay systems.
/// Only team 0 (south) is player-controlled; teams 1-3 are static enemy placeholders.
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
    const float GateWidth   = 2f;    // one-cell opening on the front edge for the gate
    const float WallModelYaw = 0f;   // extra yaw if the FBX's length axis isn't +X

    // Base centers further apart → more open mid-map feel.
    static readonly Vector3[] BasePositions =
    {
        new( 0, 0, -58), // team 0 – player (south)
        new( 0, 0,  58), // team 1 – red
        new(-58, 0,  0), // team 2 – green
        new( 58, 0,  0), // team 3 – yellow
    };

    static readonly Color[] TeamColors =
    {
        Prims.Hex(0x1e5fcc), // blue  — more saturated AoE2 blue
        Prims.Hex(0xd42020), // red   — deeper red
        Prims.Hex(0x1e9e40), // green — vivid
        Prims.Hex(0xf0a010), // yellow— warm gold
    };

    static readonly Color RoofColor = Prims.Hex(0xa6402f);

    MeshRenderer _groundRenderer;  // saved for FogOfWarSystem.Init

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

    public void Build()
    {
        // Keep simulating when the window is unfocused (alt-tab) so the AI/economy
        // don't freeze — also makes headless/automated runs behave.
        Application.runInBackground = true;

        // Seed Unity's legacy RNG so trees/mines/relics produce a reproducible layout.
        if (mapSeed == 0) mapSeed = UnityEngine.Random.Range(1, int.MaxValue);
        UnityEngine.Random.InitState(mapSeed);

        AudioManager.Init();
        SetupEnvironment();
        SetupGround();
        var cam = SetupCamera();
        cam.bounds  = new Vector2(95f, 95f); // match bigger 200×200 map
        cam.maxSize = 42f;                   // allow wider zoom-out
        var gm  = SetupGameManager();

        for (int i = 0; i < 4; i++)
            BuildBase(BasePositions[i], TeamColors[i], i);

        BuildForestRing();
        BuildMines();
        BuildRelics();
        BakeNavMesh();
        gm.cameraRig = cam;
        SetupGameplay(gm);
        cam.Init(BasePositions[0]);

        // FoW is initialised after the full scene (units, buildings, nodes) is up.
        gm.fow = gm.gameObject.AddComponent<FogOfWarSystem>();
        gm.fow.Init(_groundRenderer);

        // Post-processing: must run after camera is ready.
        SetupPostProcessing(cam.gameObject);
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

    void SetupGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(transform, false);
        ground.transform.localScale = new Vector3(20f, 1f, 20f); // 200×200
        _groundRenderer = ground.GetComponent<MeshRenderer>();
        // Textured grass instead of a flat green plane. FogOfWarSystem.Init replaces
        // this material with the fog shader only when fog of war is enabled (it isn't).
        var groundMat = Prims.Mat(Color.white, 0f, 0.05f);
        groundMat.mainTexture      = BuildGroundTexture();
        groundMat.mainTextureScale = new Vector2(12f, 12f);       // ~16 world units / tile on 200×200
        _groundRenderer.sharedMaterial = groundMat;
        _groundRenderer.receiveShadows = true;
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
    /// <param name="teamId">0 = player; 1-3 = enemy placeholders.</param>
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

        var tc = BuildingFactory.TownCenter(baseGo.transform, center, teamColor);
        SetBuildingTeam(tc, teamId);
        gm.RegisterBuilding(tc.GetComponent<BuildingEntity>());

        // Decorative fountain in the town square (native 2.0×0.28×2.0, scale 1.2).
        KenneyModels.Spawn("FantasyTown/fountain-round", baseGo.transform,
            center + forward * 3.5f, 1.2f);

        // Houses on the sides and behind the TC (provide population cap).
        var houses = new[]
        {
            BuildingFactory.House(baseGo.transform, center + right * 5f,  RoofColor),
            BuildingFactory.House(baseGo.transform, center - right * 5f,  RoofColor),
            BuildingFactory.House(baseGo.transform, center + backward * 6f, RoofColor),
            BuildingFactory.House(baseGo.transform, center + right * 5f + backward * 6f, RoofColor),
        };
        foreach (var h in houses)
        {
            SetBuildingTeam(h, teamId);
            gm.RegisterBuilding(h.GetComponent<BuildingEntity>());
        }

        // Barracks behind the houses.
        var barracks = BuildingFactory.Barracks(baseGo.transform, center + backward * 8.5f, RoofColor);
        SetBuildingTeam(barracks, teamId);
        gm.RegisterBuilding(barracks.GetComponent<BuildingEntity>());
    }

    static void SetBuildingTeam(GameObject go, int teamId)
    {
        var be = go.GetComponent<BuildingEntity>();
        if (be != null) be.teamId = teamId;
    }

    // Rectangular castle perimeter built from Kenney "Castle" modular pieces
    // (wall + gate + square towers), with a procedural fallback per piece so the
    // base still renders if the asset pack is missing. Decorative only — like the
    // old ring it carries no NavMeshObstacle, so it doesn't change unit pathing.
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
            BuildCornerTower(wallsGo.transform, corner, wallMat, roofMat);

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
        int gateCells = isGateEdge ? Mathf.Clamp(Mathf.RoundToInt(GateWidth / step), 1, n) : 0;
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
                    // so rotate it an extra 90° to bridge the opening.
                    if (KenneyModels.Spawn("Castle/gate", parent, gpos, GateScale, yaw + 90f) == null)
                        FallbackGate(parent, gpos, yaw, wallMat);
                }
                continue;
            }

            Vector3 pos = a + dir * (step * (i + 0.5f));
            if (KenneyModels.Spawn("Castle/wall", parent, pos, WallScale, yaw) == null)
                FallbackWallSeg(parent, pos, yaw, step, wallMat);
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

    void BuildForestRing()
    {
        var forest = new GameObject("Forest");
        forest.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        for (int i = 0; i < 140; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(20f, 45f);
            var pos = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            gm.RegisterNode(ResourceFactory.Tree(forest.transform, pos));
        }

        // Scatter berry bushes and fish ponds for food variety.
        for (int i = 0; i < 8; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(18f, 38f);
            gm.RegisterNode(ResourceFactory.BerryBush(forest.transform,
                new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)));
        }
        for (int i = 0; i < 4; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(22f, 42f);
            gm.RegisterNode(ResourceFactory.FishPond(forest.transform,
                new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)));
        }
    }

    void BuildMines()
    {
        var mines = new GameObject("Mines");
        mines.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        gm.RegisterNode(ResourceFactory.GoldMine(mines.transform, new Vector3(-14, 0,   0)));
        gm.RegisterNode(ResourceFactory.GoldMine(mines.transform, new Vector3( 14, 0,   0)));
        gm.RegisterNode(ResourceFactory.StoneMine(mines.transform, new Vector3(  0, 0,  14)));
        gm.RegisterNode(ResourceFactory.StoneMine(mines.transform, new Vector3(  0, 0, -14)));
        // Extra deposits scattered mid-map on the bigger arena
        gm.RegisterNode(ResourceFactory.GoldMine(mines.transform,  new Vector3(-30, 0, -30)));
        gm.RegisterNode(ResourceFactory.GoldMine(mines.transform,  new Vector3( 30, 0,  30)));
        gm.RegisterNode(ResourceFactory.StoneMine(mines.transform, new Vector3(-30, 0,  30)));
        gm.RegisterNode(ResourceFactory.StoneMine(mines.transform, new Vector3( 30, 0, -30)));
    }

    // Three contested relics near the map centre (clear of the ±8 mines): one dead
    // centre, two on a diagonal. Whoever holds them earns a passive gold trickle.
    void BuildRelics()
    {
        var relics = new GameObject("Relics");
        relics.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3(  0, 0,   0)));
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3(-22, 0,  22)));
        gm.RegisterRelic(RelicFactory.Relic(relics.transform, new Vector3( 22, 0, -22)));
    }

    // ── NavMesh ───────────────────────────────────────────────────────────────

    void BakeNavMesh()
    {
        const float groundHalf  = 100f; // 200×200 map half-extent
        const float agentHeight = 2f;

        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape     = NavMeshBuildSourceShape.Box,
                size      = new Vector3(groundHalf * 2f, 0.1f, groundHalf * 2f),
                transform = Matrix4x4.identity,
                area      = 0,
            }
        };

        var settings = NavMesh.GetSettingsByIndex(0);
        var bounds   = new Bounds(Vector3.zero, new Vector3(groundHalf * 2f + 4f, agentHeight * 2f, groundHalf * 2f + 4f));
        var data     = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data != null) NavMesh.AddNavMeshData(data);
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
        return gm;
    }

    void SetupGameplay(GameManager gm)
    {
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

        // Enemy garrisons (teams 1-3): a starting army per base, plus an EnemyAI
        // brain that reinforces and rushes. They self-defend via CombatSystem aggro.
        for (int t = 1; t < 4; t++)
        {
            SpawnGarrison(gm, unitsRoot.transform, BasePositions[t], TeamColors[t], t);

            var aiGo = new GameObject($"EnemyAI_T{t}_{Personalities[t]}");
            aiGo.transform.SetParent(transform, false);
            aiGo.AddComponent<EnemyAI>().Init(t, TeamColors[t], BasePositions[t], unitsRoot.transform, Personalities[t]);
        }

        // popCap now derives from team-0 buildings (TC + Houses) via RecomputePop.
        gm.RecomputePop();
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
        {
            var m = UnitFactory.Militia(parent, p, color);
            m.teamId = teamId;
            gm.RegisterUnit(m);
        }

        // Starting villagers (behind TC, ready to gather)
        Vector3[] vilPos =
        {
            center + backward * 2f + right * -1.5f,
            center + backward * 2.5f,
            center + backward * 2f + right *  1.5f,
        };
        foreach (var p in vilPos)
        {
            var v = UnitFactory.Villager(parent, p, color);
            v.teamId = teamId;
            gm.RegisterUnit(v);
        }
    }
}
