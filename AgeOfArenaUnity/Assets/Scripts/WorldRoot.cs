using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds the full 4-player arena at runtime: 4 walled bases in a diamond layout,
/// shared forest ring, central mines, NavMesh, and gameplay systems.
/// Only team 0 (south) is player-controlled; teams 1-3 are static enemy placeholders.
/// </summary>
public class WorldRoot : MonoBehaviour
{
    const float ArenaRadiusX = 11f;
    const float ArenaRadiusZ = 10f;
    const float WallHeight   = 3f;
    const int   WallSegments = 36;

    // Base centers: south (player), north, west, east.
    static readonly Vector3[] BasePositions =
    {
        new( 0, 0, -40), // team 0 – player (south)
        new( 0, 0,  40), // team 1 – red
        new(-40, 0,  0), // team 2 – green
        new( 40, 0,  0), // team 3 – yellow
    };

    static readonly Color[] TeamColors =
    {
        Prims.Hex(0x2a5db0), // blue
        Prims.Hex(0xc0392b), // red
        Prims.Hex(0x27ae60), // green
        Prims.Hex(0xf39c12), // yellow
    };

    static readonly Color RoofColor = Prims.Hex(0xa6402f);

    public void Build()
    {
        // Keep simulating when the window is unfocused (alt-tab) so the AI/economy
        // don't freeze — also makes headless/automated runs behave.
        Application.runInBackground = true;

        SetupEnvironment();
        SetupGround();
        var cam = SetupCamera();
        var gm  = SetupGameManager();

        for (int i = 0; i < 4; i++)
            BuildBase(BasePositions[i], TeamColors[i], i);

        BuildForestRing();
        BuildMines();
        BakeNavMesh();
        SetupGameplay(gm);
        cam.Init(BasePositions[0]);
    }

    // ── Environment ──────────────────────────────────────────────────────────

    void SetupEnvironment()
    {
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(transform, false);
        var sun = sunGo.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.color     = Prims.Hex(0xfff0d0);
        sun.intensity = 1.5f;
        sun.shadows   = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(50f, 30f, 0f);

        RenderSettings.ambientMode        = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor    = Prims.Hex(0xbfe3ff);
        RenderSettings.ambientEquatorColor= Prims.Hex(0x9fb08a);
        RenderSettings.ambientGroundColor = Prims.Hex(0x6b5a3a);
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
        ground.transform.localScale = new Vector3(12f, 1f, 12f); // 120×120
        ground.GetComponent<MeshRenderer>().sharedMaterial = Prims.Mat(Prims.Hex(0x5b8c3e), 0f, 0.05f);
        ground.GetComponent<MeshRenderer>().receiveShadows = true;
    }

    IsometricCameraRig SetupCamera()
    {
        var camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.backgroundColor = Prims.Hex(0xa9d4e8);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        return camGo.AddComponent<IsometricCameraRig>();
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
            BuildingFactory.House(baseGo.transform, center + right * 4f,  RoofColor),
            BuildingFactory.House(baseGo.transform, center - right * 4f,  RoofColor),
            BuildingFactory.House(baseGo.transform, center + backward * 5f, RoofColor),
            BuildingFactory.House(baseGo.transform, center + right * 4f + backward * 5f, RoofColor),
        };
        foreach (var h in houses)
        {
            SetBuildingTeam(h, teamId);
            gm.RegisterBuilding(h.GetComponent<BuildingEntity>());
        }

        // Barracks behind the houses.
        var barracks = BuildingFactory.Barracks(baseGo.transform, center + backward * 6.5f, RoofColor);
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
        var wallMat  = Prims.Mat(Prims.Hex(0xa79b7d));
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
                1.1f, 1.2f, 8, Prims.Mat(Prims.Hex(0x5a3a1e)));
        }

        Prims.EnableShadows(wallsGo);
    }

    // ── Resources ────────────────────────────────────────────────────────────

    void BuildForestRing()
    {
        var forest = new GameObject("Forest");
        forest.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        for (int i = 0; i < 80; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(14f, 28f);
            var pos = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            gm.RegisterNode(ResourceFactory.Tree(forest.transform, pos));
        }
    }

    void BuildMines()
    {
        var mines = new GameObject("Mines");
        mines.transform.SetParent(transform, false);
        var gm = GameManager.Instance;
        gm.RegisterNode(ResourceFactory.GoldMine(mines.transform, new Vector3(-8, 0, 0)));
        gm.RegisterNode(ResourceFactory.GoldMine(mines.transform, new Vector3( 8, 0, 0)));
        gm.RegisterNode(ResourceFactory.StoneMine(mines.transform, new Vector3( 0, 0, 8)));
        gm.RegisterNode(ResourceFactory.StoneMine(mines.transform, new Vector3( 0, 0,-8)));
    }

    // ── NavMesh ───────────────────────────────────────────────────────────────

    void BakeNavMesh()
    {
        const float groundHalf  = 60f;
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
        gm.build         = go.AddComponent<BuildSystem>();
        gm.placement     = go.AddComponent<BuildingPlacement>();
        gm.selection     = go.AddComponent<SelectionSystem>();
        gm.command       = go.AddComponent<CommandSystem>();
        gm.trainingQueue = go.AddComponent<TrainingQueue>();
        gm.hud           = go.AddComponent<HUD>();
        gm.minimap       = go.AddComponent<MinimapSystem>();
        gm.match         = go.AddComponent<MatchSystem>();
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

            var aiGo = new GameObject($"EnemyAI_T{t}");
            aiGo.transform.SetParent(transform, false);
            aiGo.AddComponent<EnemyAI>().Init(t, TeamColors[t], BasePositions[t], unitsRoot.transform);
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
