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
    const float ArenaRadiusX = 16f;  // bigger base wall ring
    const float ArenaRadiusZ = 14f;
    const float WallHeight   = 3.5f;
    const int   WallSegments = 40;

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

    public void Build()
    {
        // Keep simulating when the window is unfocused (alt-tab) so the AI/economy
        // don't freeze — also makes headless/automated runs behave.
        Application.runInBackground = true;

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

        KenneyPilotRow(); // TEMP: visual comparison of Kenney CC0 models — remove after review.
    }

    /// <summary>
    /// TEMP pilot: spawns a row of Kenney models in front of the player base so we
    /// can screenshot them next to the procedural buildings and confirm style/scale.
    /// Remove once the model mapping is approved.
    /// </summary>
    void KenneyPilotRow()
    {
        var row = new GameObject("KenneyPilot").transform;
        row.SetParent(transform, false);
        float z = -48f;
        KenneyModels.Spawn("FantasyTown/windmill",        row, new Vector3(-12, 0, z), 3.2f);
        KenneyModels.Spawn("FantasyTown/watermill",       row, new Vector3( -6, 0, z), 3.2f);
        KenneyModels.Spawn("Castle/tower-square-mid",     row, new Vector3(  0, 0, z), 2.6f);
        KenneyModels.Spawn("Castle/gate",                 row, new Vector3(  6, 0, z), 2.6f);
        KenneyModels.Spawn("Nature/tree_default",         row, new Vector3( 11, 0, z), 3.0f);
        KenneyModels.Spawn("Nature/rock_largeA",          row, new Vector3( 14, 0, z), 2.4f);
        KenneyModels.Spawn("FantasyTown/stall-red",       row, new Vector3( -9, 0, z - 4f), 3.0f);
        KenneyModels.Spawn("FantasyTown/banner-red",      row, new Vector3( -3, 0, z - 4f), 3.0f);
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

    void BuildWalls(Transform parent, Vector3 center, float gateAngleDeg)
    {
        var wallsGo = new GameObject("Walls");
        wallsGo.transform.SetParent(parent, false);
        var wallMat  = Prims.Mat(Prims.Hex(0x8a7d60), 0f, 0.05f); // matte stone — no specular glare
        float gateHalf = 18f * Mathf.Deg2Rad;

        for (int i = 0; i < WallSegments; i++)
        {
            float t0   = (i       / (float)WallSegments) * Mathf.PI * 2f;
            float t1   = ((i + 1) / (float)WallSegments) * Mathf.PI * 2f;
            float tMid = (t0 + t1) * 0.5f;

            float diff = Mathf.DeltaAngle(tMid * Mathf.Rad2Deg, gateAngleDeg) * Mathf.Deg2Rad;
            if (Mathf.Abs(diff) < gateHalf) continue;

            var p0  = new Vector3(center.x + Mathf.Cos(t0) * ArenaRadiusX, 0, center.z + Mathf.Sin(t0) * ArenaRadiusZ);
            var p1  = new Vector3(center.x + Mathf.Cos(t1) * ArenaRadiusX, 0, center.z + Mathf.Sin(t1) * ArenaRadiusZ);
            var mid = (p0 + p1) * 0.5f;
            float segLen = Vector3.Distance(p0, p1) + 0.1f;
            float angle  = Mathf.Atan2(p1.z - p0.z, p1.x - p0.x) * Mathf.Rad2Deg;

            var wall = Prims.Box(wallsGo.transform,
                new Vector3(mid.x, WallHeight * 0.5f, mid.z),
                new Vector3(segLen, WallHeight, 1f), wallMat);
            wall.transform.localRotation = Quaternion.Euler(0, -angle, 0);

            var merlon = Prims.Box(wallsGo.transform,
                new Vector3(mid.x, WallHeight + 0.45f, mid.z),
                new Vector3(0.8f, 0.9f, 0.8f), wallMat);
            merlon.transform.localRotation = Quaternion.Euler(0, -angle, 0);
        }

        for (int c = 0; c < 4; c++)
        {
            float a   = (c / 4f) * Mathf.PI * 2f + Mathf.PI / 4f;
            var pos   = new Vector3(center.x + Mathf.Cos(a) * (ArenaRadiusX + 0.3f), 0,
                                    center.z + Mathf.Sin(a) * (ArenaRadiusZ + 0.3f));
            Prims.Cylinder(wallsGo.transform, new Vector3(pos.x, (WallHeight + 1.5f) * 0.5f, pos.z),
                0.9f, WallHeight + 1.5f, wallMat);
            Prims.Cone(wallsGo.transform, new Vector3(pos.x, WallHeight + 1.5f, pos.z),
                1.1f, 1.2f, 8, Prims.Mat(Prims.Hex(0x7a5c3a), 0f, 0.05f));
        }

        Prims.EnableShadows(wallsGo);
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
