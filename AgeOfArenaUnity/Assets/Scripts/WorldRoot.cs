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
    const float GateWidth   = 2f;    // visual gate piece footprint on the front edge
    const float GateOpening = 4.5f;  // NavMesh gap left in the wall — wide enough that a
                                     // 0.5-radius agent paths cleanly through the gate
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

    // ── Island geometry (AoE2 "Arena island": round land ringed by ocean) ──────
    const float LandRadius  = 84f;   // grass disc radius (coastline)
    const float BeachRadius = 88f;   // sand rim just past the grass
    const float OceanHalf   = 110f;  // ocean plane half-extent (square sea frame)
    const float CoastInner  = 76f;   // coastal forest ring starts beyond the base back walls
    const float CoastOuter  = 84f;   // …and hugs the shoreline

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

        BuildCoastalForest();   // dense conifer wall hugging the shoreline (blocks units)
        BuildInteriorClumps();  // a few broadleaf groves in the open battlefield
        for (int i = 0; i < 4; i++)
            BuildBaseResources(BasePositions[i]); // gold/stone/berry/wood/fish inside each pocket
        BuildContestedResources(); // extra mines fought over in the centre
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

        // CIVS: prompt the player to pick a civilization over the freshly-built arena.
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
        // ── Ocean (bottom layer, y=0) — large flat plane, keeps its MeshCollider so
        // click-to-move / selection raycasts resolve everywhere (land discs have none).
        var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "Ocean";
        ocean.transform.SetParent(transform, false);
        ocean.transform.localScale = new Vector3(OceanHalf * 2f / 10f, 1f, OceanHalf * 2f / 10f); // Plane = 10u @ scale 1
        var oceanRend = ocean.GetComponent<MeshRenderer>();
        oceanRend.sharedMaterial = Prims.Mat(Prims.Hex(0x2766a8), 0.1f, 0.82f); // deep glossy sea
        oceanRend.receiveShadows = false;

        // ── Beach (sand rim, y=0.03) — a flat-coloured disc poking out past the grass.
        var sand = NewMeshObject("Beach", BuildDiscMesh(BeachRadius, 72, 0f),
            Prims.Mat(Prims.Hex(0xd9c9a3), 0f, 0.05f), 0.03f);
        sand.GetComponent<MeshRenderer>().receiveShadows = true;

        // ── Grass (playable land, y=0.05) — reuses the seamless grass texture, tiled via
        // baked UVs (every 16 world units) so detail stays crisp on the big disc.
        var grassMat = Prims.Mat(Color.white, 0f, 0.05f);
        grassMat.mainTexture = BuildGroundTexture();
        _landMesh = BuildDiscMesh(LandRadius, 96, 1f / 16f);
        var grass = NewMeshObject("Ground", _landMesh, grassMat, 0.05f);
        _groundRenderer = grass.GetComponent<MeshRenderer>();
        _groundRenderer.receiveShadows = true;
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

    // Dense conifer wall hugging the shoreline (CoastInner..CoastOuter), clustered for
    // a natural look, with a beach gap behind each base so the shore stays reachable.
    // Every coastal tree is also registered as a NavMesh obstacle → the ring is a wall.
    void BuildCoastalForest()
    {
        var forest = new GameObject("CoastalForest");
        forest.transform.SetParent(transform, false);
        var gm = GameManager.Instance;

        // Backward heading (radians) of each base — leave a gap centred there.
        var gaps = new float[BasePositions.Length];
        for (int i = 0; i < BasePositions.Length; i++)
            gaps[i] = Mathf.Atan2(-BasePositions[i].z, -BasePositions[i].x);
        const float gapHalf = 0.16f; // ~9° each side

        const int clusters = 72;
        for (int c = 0; c < clusters; c++)
        {
            float clusterA = c / (float)clusters * Mathf.PI * 2f;
            if (InAnyGap(clusterA, gaps, gapHalf)) continue;
            int n = Random.Range(3, 6);
            for (int k = 0; k < n; k++)
            {
                float a = clusterA + Random.Range(-0.05f, 0.05f);
                float r = Random.Range(CoastInner, CoastOuter);
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                gm.RegisterNode(ResourceFactory.Tree(forest.transform, pos, ResourceFactory.TreeKind.Conifer));
                AddObstacle(pos, 1.4f, 1.4f, 0f);
            }
        }

        // Floating lilies in the shallows + ground foliage carpeting the interior.
        Decor.ScatterLilies(forest.transform, BeachRadius + 2f, OceanHalf * 0.55f, 40);
        Decor.Scatter(forest.transform, Vector3.zero, 6f, CoastInner - 4f, 130);
    }

    // Minimal angular distance (radians) check against any base's beach gap.
    static bool InAnyGap(float angle, float[] gaps, float half)
    {
        foreach (var g in gaps)
        {
            float d = Mathf.Abs(Mathf.Repeat(angle - g + Mathf.PI, Mathf.PI * 2f) - Mathf.PI);
            if (d < half) return true;
        }
        return false;
    }

    // A few broadleaf groves dotting the open battlefield (the central forest blobs in
    // the reference). Harvestable, but NOT obstacles — the centre stays passable.
    void BuildInteriorClumps()
    {
        var grove = new GameObject("Groves");
        grove.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        Vector3[] centers = { new(0, 0, 34), new(0, 0, -34), new(34, 0, 0), new(-34, 0, 0) };
        foreach (var ctr in centers)
        {
            int n = Random.Range(4, 7);
            for (int k = 0; k < n; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(0f, 4f);
                gm.RegisterNode(ResourceFactory.Tree(grove.transform,
                    ctr + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                    ResourceFactory.TreeKind.Broadleaf));
            }
        }
    }

    // Arena economy: each walled pocket carries its own starting resources so the base
    // is self-sufficient (boom map). Positions are in the base's local frame, kept
    // inside the WallHalf box and dodging the houses/barracks placed in BuildBase.
    void BuildBaseResources(Vector3 center)
    {
        var root = new GameObject("BaseEco");
        root.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        BaseFrame(center, out var forward, out var backward, out var right);

        // 2 gold at the front corners (toward the gate, off the centre corridor).
        gm.RegisterNode(ResourceFactory.GoldMine(root.transform, center + forward * 2f + right *  10f));
        gm.RegisterNode(ResourceFactory.GoldMine(root.transform, center + forward * 2f + right * -10f));
        // 1 stone on a mid side, a berry patch on the other.
        gm.RegisterNode(ResourceFactory.StoneMine(root.transform, center + right * 11f));
        gm.RegisterNode(ResourceFactory.BerryBush(root.transform, center + right * -11f + forward * 1.5f));
        gm.RegisterNode(ResourceFactory.BerryBush(root.transform, center + right * -11f + backward * 2.5f));
        // Starting wood line just inside the back wall (behind the barracks).
        for (int k = -2; k <= 2; k++)
            gm.RegisterNode(ResourceFactory.Tree(root.transform,
                center + backward * 10.5f + right * (k * 3.5f), ResourceFactory.TreeKind.Broadleaf));

        // 2 fish in the coastal shallows behind the base (reached via the beach gap).
        gm.RegisterNode(ResourceFactory.FishPond(root.transform, center + backward * 20f + right *  3f));
        gm.RegisterNode(ResourceFactory.FishPond(root.transform, center + backward * 20f + right * -3f));
    }

    // Extra deposits in the open centre — the prize that pulls armies out of the walls.
    void BuildContestedResources()
    {
        var root = new GameObject("ContestedMines");
        root.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        gm.RegisterNode(ResourceFactory.GoldMine(root.transform,  new Vector3( 17, 0,  17)));
        gm.RegisterNode(ResourceFactory.GoldMine(root.transform,  new Vector3(-17, 0, -17)));
        gm.RegisterNode(ResourceFactory.StoneMine(root.transform, new Vector3( 17, 0, -17)));
        gm.RegisterNode(ResourceFactory.StoneMine(root.transform, new Vector3(-17, 0,  17)));
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

    // Two NavMeshes baked from the ground meshes themselves:
    //  • Land  = the grass disc (an exact circle) minus wall + coastal-forest obstacles.
    //  • Naval = the ocean plane minus the island disc → Galleys ring the whole coast.
    void BakeNavMesh()
    {
        const float agentHeight = 2f;
        var bounds = new Bounds(Vector3.zero,
            new Vector3(OceanHalf * 2f + 4f, agentHeight * 2f + 4f, OceanHalf * 2f + 4f));

        // ── Land NavMesh (default agent): walkable on the disc, blocked by walls/forest.
        var landSources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape        = NavMeshBuildSourceShape.Mesh,
                sourceObject = _landMesh,
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
            // …minus the island disc, so naval units ring the coast only.
            new NavMeshBuildSource
            {
                shape        = NavMeshBuildSourceShape.Mesh,
                sourceObject = _landMesh,
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
        return gm;
    }

    void SetupGameplay(GameManager gm)
    {
        // CIVS: the player (team 0) keeps their chosen civ; only AI teams (1-3) are randomized.
        // GameBootstrap.PlayerCiv is None until the player picks via CivSelectScreen.
        var civValues = (Civilization[])System.Enum.GetValues(typeof(Civilization));
        gm.teamCivs[0] = GameBootstrap.PlayerCiv;
        for (int i = 1; i < 4; i++)
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
    }
}
