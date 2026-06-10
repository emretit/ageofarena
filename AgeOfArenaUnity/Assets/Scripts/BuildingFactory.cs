using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Procedural building meshes — a direct C# port of the Three.js BuildingFactory.
/// Each returns a parented GameObject placed at local origin (drop onto ground
/// by setting the returned transform's world position).
/// </summary>
public static class BuildingFactory
{
    static readonly Color Stone  = Prims.Hex(0x625a4a); // darker, richer stone
    static readonly Color Plaster= Prims.Hex(0xd8c898); // warmer cream plaster
    static readonly Color Dark   = Prims.Hex(0x4e4438); // deep shadow tone
    static readonly Color Timber = Prims.Hex(0x3a2414); // rich dark timber
    static readonly Color Door   = Prims.Hex(0x4a2c10);
    static readonly Color Window = Prims.Hex(0x6aa0cc); // deeper window blue

    // ── Quaternius "Ultimate Fantasy RTS" building models ──────────────────
    // Purpose-built, visually distinct model per building type (CC0). Replaces
    // the old Kenney-keep reuse that made TownCenter/House/Barracks/towers look
    // identical. Missing-model and uncovered types fall through to the bespoke
    // procedural/Kenney factories below.
    struct QSpec
    {
        public string model;     // Resources/Quaternius/RTS/<model>
        public float width;      // target ground footprint (auto-scaled)
        public float height;     // when >0, fit by height instead (tall towers)
        public float yaw;        // model facing correction
        public float pitch;      // X-rotation to fix models authored lying down (Z-up)
        public bool flag;        // add a team-colour banner on top
        public Vector3 colCenter, colSize; // selection collider
    }

    static readonly Dictionary<BuildingType, QSpec> QSpecs = new()
    {
        [BuildingType.TownCenter]   = new QSpec { model = "TownCenter_SecondAge_Level1", width = 3.4f, flag = true,  colCenter = new(0, 1.5f, 0), colSize = new(3.2f, 3.2f, 3.2f) },
        [BuildingType.House]        = new QSpec { model = "Houses_FirstAge_1_Level1",    width = 1.8f, flag = false, colCenter = new(0, 0.9f, 0), colSize = new(1.8f, 1.8f, 1.8f) },
        [BuildingType.Barracks]     = new QSpec { model = "Barracks_FirstAge_Level1",    width = 2.6f, flag = true,  colCenter = new(0, 1.3f, 0), colSize = new(2.7f, 2.8f, 2.2f) },
        [BuildingType.ArcheryRange] = new QSpec { model = "Archery_FirstAge_Level1",     width = 2.6f, flag = true,  colCenter = new(0, 1.2f, 0), colSize = new(2.6f, 2.4f, 2.2f) },
        [BuildingType.Stable]       = new QSpec { model = "Houses_SecondAge_2_Level1",   width = 2.6f, flag = true,  colCenter = new(0, 1.2f, 0), colSize = new(2.8f, 2.4f, 2.4f) },
        [BuildingType.Farm]         = new QSpec { model = "Farm_FirstAge_Level1_Wheat",  width = 3.0f, flag = false, colCenter = new(0, 0.25f, 0), colSize = new(3.0f, 0.5f, 3.0f) },
        [BuildingType.LumberCamp]   = new QSpec { model = "Storage_FirstAge_Level1",     width = 2.2f, flag = false, colCenter = new(0, 0.7f, 0), colSize = new(2.2f, 1.4f, 2.2f) },
        [BuildingType.MiningCamp]   = new QSpec { model = "Mine",                        width = 2.4f, flag = false, colCenter = new(0, 0.7f, 0), colSize = new(2.2f, 1.4f, 2.2f) },
        [BuildingType.Mill]         = new QSpec { model = "Windmill_SecondAge",          height = 3.0f, pitch = -90f, flag = false, colCenter = new(0, 1.0f, 0), colSize = new(2.0f, 2.0f, 2.0f) },
        [BuildingType.Market]       = new QSpec { model = "Market_SecondAge_Level1",     width = 2.8f, flag = true,  colCenter = new(0, 1.0f, 0), colSize = new(2.8f, 2.0f, 2.6f) },
        [BuildingType.Monastery]    = new QSpec { model = "Temple_FirstAge_Level1",      width = 2.6f, flag = true,  colCenter = new(0, 1.2f, 0), colSize = new(2.4f, 2.4f, 2.4f) },
        [BuildingType.University]   = new QSpec { model = "Temple_SecondAge_Level1",     width = 2.8f, flag = true,  colCenter = new(0, 1.3f, 0), colSize = new(2.6f, 2.6f, 2.6f) },
        [BuildingType.Dock]         = new QSpec { model = "Port_SecondAge_Level1",        height = 3.2f, flag = true,  colCenter = new(0, 0.7f, 0), colSize = new(3.0f, 1.4f, 3.0f) },
        [BuildingType.Blacksmith]   = new QSpec { model = "Storage_SecondAge_Level1",    width = 2.4f, flag = true,  colCenter = new(0, 1.0f, 0), colSize = new(2.2f, 2.0f, 2.2f) },
        [BuildingType.SiegeWorkshop]= new QSpec { model = "Storage_SecondAge_Level2",    width = 2.8f, flag = true,  colCenter = new(0, 1.0f, 0), colSize = new(2.6f, 2.0f, 2.6f) },
        [BuildingType.Wonder]       = new QSpec { model = "Wonder_SecondAge_Level3",     width = 4.8f, flag = true,  colCenter = new(0, 3.0f, 0), colSize = new(5.0f, 6.0f, 5.0f) },
        [BuildingType.WatchTower]   = new QSpec { model = "WatchTower_SecondAge_Level1", height = 3.0f, flag = true,  colCenter = new(0, 1.5f, 0), colSize = new(1.4f, 3.2f, 1.4f) },
        [BuildingType.Outpost]      = new QSpec { model = "WatchTower_FirstAge_Level1",  height = 2.4f, flag = true,  colCenter = new(0, 1.2f, 0), colSize = new(1.0f, 2.6f, 1.0f) },
        [BuildingType.BombardTower] = new QSpec { model = "WatchTower_SecondAge_Level3", height = 3.4f, flag = true,  colCenter = new(0, 1.6f, 0), colSize = new(1.6f, 3.4f, 1.6f) },
        [BuildingType.Castle]       = new QSpec { model = "TowerHouse_SecondAge",        width = 3.8f, flag = true,  colCenter = new(0, 2.5f, 0), colSize = new(4.5f, 5.0f, 4.5f) },
    };

    /// <summary>
    /// Build a building from its Quaternius model when one is mapped. Returns
    /// <c>null</c> for unmapped types or a missing model so <see cref="Create"/>
    /// falls back to the procedural / Kenney factories.
    /// </summary>
    static GameObject TryQuaternius(BuildingType type, Transform parent, Vector3 worldPos, Color teamColor)
    {
        if (!QSpecs.TryGetValue(type, out var spec)) return null;

        string model = spec.model;
        if (type == BuildingType.House)
        {
            // Deterministic visual variety: pick one of three house meshes from
            // the plot position so saves/replays stay stable.
            int v = Mathf.Abs(Mathf.RoundToInt(worldPos.x * 7.3f + worldPos.z * 13.1f)) % 3;
            model = $"Houses_FirstAge_{v + 1}_Level1";
        }
        if (!QuaterniusBuildings.Has(model)) return null;

        var g = NewBuilding(type.ToString(), parent, worldPos, type, spec.colCenter, spec.colSize);
        var t = g.transform;
        bool byHeight = spec.height > 0f;
        float target = byHeight ? spec.height : spec.width;
        var built = QuaterniusBuildings.Spawn(model, t, target, spec.yaw, default, byHeight, spec.pitch);
        if (spec.flag && built != null)
            AddTeamFlag(t, QuaterniusBuildings.Height(built, t), teamColor);

        Prims.EnableShadows(g);
        return g;
    }

    /// <summary>A team-colour banner on a pole rising just above the roof.</summary>
    static void AddTeamFlag(Transform t, float roofTop, Color teamColor)
    {
        float poleH = Mathf.Clamp(roofTop * 0.3f, 0.7f, 1.3f);
        float baseY = roofTop;
        Prims.Cylinder(t, new Vector3(0f, baseY + poleH * 0.5f, 0f), 0.045f, poleH,
            Prims.Mat(Prims.Hex(0x4a3a2a), 0.15f));
        Prims.Box(t, new Vector3(0.33f, baseY + poleH * 0.78f, 0f), new Vector3(0.6f, 0.42f, 0.03f),
            Prims.Mat(teamColor, 0f, 0.4f)).name = "Flag";
    }

    static GameObject QuaterniusAnimal(string modelName, Transform parent, Vector3 localPos,
        float scale = 1f, float yaw = 0f)
    {
        var prefab = Resources.Load<GameObject>("Quaternius/Animals/" + modelName);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab);
        go.name = modelName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        AlignAnimalToLocalFootprint(go.transform, parent, localPos);
        return go;
    }

    static void AlignAnimalToLocalFootprint(Transform animal, Transform parent, Vector3 localFootprint)
    {
        var renderers = animal.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 localCenter = parent.InverseTransformPoint(bounds.center);
        Vector3 localBottom = parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        animal.localPosition += new Vector3(
            localFootprint.x - localCenter.x,
            localFootprint.y - localBottom.y,
            localFootprint.z - localCenter.z);
    }

    // Compose a keep from Castle-kit square-tower pieces at the building's local
    // origin. Native heights (scale 1): base 1.01, mid 1.01, top-roof 1.0. Returns
    // false when the asset pack is missing so callers fall back to procedural meshes.
    static bool KenneyKeep(Transform t, float scale, int midSections, bool turrets)
    {
        if (KenneyModels.Spawn("Castle/tower-square-base", t, Vector3.zero, scale) == null)
            return false;

        float y = 1.00f * scale;
        for (int i = 0; i < midSections; i++)
        {
            KenneyModels.Spawn("Castle/tower-square-mid", t, Vector3.zero, scale, 0f, y);
            y += 1.00f * scale;
        }
        KenneyModels.Spawn("Castle/tower-square-top-roof", t, Vector3.zero, scale, 0f, y);

        if (turrets)
        {
            float d  = 0.5f * scale;        // corner of the base footprint
            float ts = 0.5f * scale;        // turret scale
            foreach (var cx in new[] { -d, d })
                foreach (var cz in new[] { -d, d })
                    KenneyModels.Spawn("Castle/tower-square", t, new Vector3(cx, 0, cz), ts);
        }
        return true;
    }

    public static GameObject TownCenter(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = new GameObject("TownCenter");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;
        var be = g.AddComponent<BuildingEntity>();
        be.type = BuildingType.TownCenter;
        be.teamId = 0;
        // Root collider for reliable building selection raycasts.
        var col = g.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 1.5f, 0);
        col.size   = new Vector3(3.2f, 3.2f, 3.2f);

        // Kenney castle keep with four corner turrets; procedural keep otherwise.
        if (!KenneyKeep(t, 2.2f, midSections: 1, turrets: true))
        {
            var stoneMat = Prims.Mat(Stone, 0.05f);
            var wallMat = Prims.Mat(Plaster);
            var timberMat = Prims.Mat(Timber);
            var doorMat = Prims.Mat(Door);
            var roofMat = Prims.Mat(teamColor, 0.05f, 0.3f);
            var winMat = Prims.Mat(Window, 0.1f, 0.4f);

            Prims.Box(t, new Vector3(0, 0.07f, 0), new Vector3(4.0f, 0.15f, 4.0f), stoneMat);   // step
            Prims.Box(t, new Vector3(0, 0.2f, 0), new Vector3(3.6f, 0.4f, 3.6f), stoneMat);     // platform
            Prims.Box(t, new Vector3(0, 0.65f, 0), new Vector3(3.1f, 0.5f, 3.1f), stoneMat);    // base band
            Prims.Box(t, new Vector3(0, 1.4f, 0), new Vector3(3f, 2f, 3f), wallMat);            // body
            Prims.Box(t, new Vector3(0, 2.44f, 0), new Vector3(3.2f, 0.12f, 3.2f), timberMat);  // roof edge
            Prims.Cone(t, new Vector3(0, 2.45f, 0), 2.6f, 1.5f, 4, roofMat, 45f);               // roof

            // door + arch
            Prims.Box(t, new Vector3(0, 1.0f, -1.52f), new Vector3(0.7f, 1.2f, 0.08f), doorMat);
            Prims.Box(t, new Vector3(0, 1.65f, -1.52f), new Vector3(0.85f, 0.15f, 0.12f), stoneMat);

            // corner pillars
            foreach (var sx in new[] { -1.4f, 1.4f })
                foreach (var sz in new[] { -1.4f, 1.4f })
                    Prims.Box(t, new Vector3(sx, 1.45f, sz), new Vector3(0.25f, 2.1f, 0.25f), stoneMat);

            // windows on 4 sides
            AddWindow(t, new Vector3(0, 1.6f, -1.52f), 0, winMat, timberMat);
            AddWindow(t, new Vector3(0, 1.6f, 1.52f), 0, winMat, timberMat);
            AddWindow(t, new Vector3(-1.52f, 1.6f, 0), 90, winMat, timberMat);
            AddWindow(t, new Vector3(1.52f, 1.6f, 0), 90, winMat, timberMat);
        }

        // Team flag — always procedural so the team colour stays visible atop the keep.
        Prims.Cylinder(t, new Vector3(0, 4.9f, 0), 0.05f, 2.0f, Prims.Mat(Prims.Hex(0x4a3a2a), 0.15f));
        var flag = Prims.Box(t, new Vector3(0.45f, 5.5f, 0), new Vector3(0.9f, 0.6f, 0.04f), Prims.Mat(teamColor, 0, 0.4f));
        flag.name = "Flag";

        Prims.BlobShadow(t, 2.0f);
        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject House(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = new GameObject("House");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;
        var be = g.AddComponent<BuildingEntity>();
        be.type = BuildingType.House;
        be.teamId = 0;
        var hcol = g.AddComponent<BoxCollider>();
        hcol.center = new Vector3(0, 0.9f, 0);
        hcol.size   = new Vector3(1.8f, 1.8f, 1.8f);

        // Small Kenney keep (base + roof) reads as a fortified house; procedural otherwise.
        bool houseKenney = KenneyKeep(t, 1.5f, midSections: 0, turrets: false);
        if (houseKenney)
        {
            // Chimney poking out through the roof (native 0.21×1.00×0.34, scale 1.5).
            KenneyModels.Spawn("FantasyTown/chimney", t, new Vector3(0.28f, 2.2f, 0.18f), 1.5f);
        }
        if (!houseKenney)
        {
            var stoneMat = Prims.Mat(Stone, 0.05f);
            var wallMat = Prims.Mat(Plaster);
            var timberMat = Prims.Mat(Timber);
            var roofMat = Prims.Mat(roofColor, 0.05f, 0.25f);

            Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(1.7f, 0.3f, 1.7f), stoneMat);
            Prims.Box(t, new Vector3(0, 1.05f, 0), new Vector3(1.5f, 1.5f, 1.5f), wallMat);
            foreach (var sx in new[] { -0.7f, 0.7f })
                foreach (var sz in new[] { -0.7f, 0.7f })
                    Prims.Box(t, new Vector3(sx, 1.05f, sz), new Vector3(0.08f, 1.5f, 0.08f), timberMat);
            Prims.Box(t, new Vector3(0.3f, 0.65f, -0.76f), new Vector3(0.4f, 0.7f, 0.06f), Prims.Mat(Door));
            Prims.Cone(t, new Vector3(0, 1.8f, 0), 1.25f, 1.0f, 4, roofMat, 45f);
        }

        Prims.BlobShadow(t, 1.1f);
        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Barracks(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = new GameObject("Barracks");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;
        var be = g.AddComponent<BuildingEntity>();
        be.type = BuildingType.Barracks;
        be.teamId = 0;
        var col = g.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 1.3f, 0);
        col.size   = new Vector3(2.7f, 2.8f, 2.2f);

        // Kenney keep (base + mid + roof) as a fortified hall; procedural otherwise.
        if (!KenneyKeep(t, 1.8f, midSections: 1, turrets: false))
        {
            var stoneMat = Prims.Mat(Stone, 0.05f);
            var darkMat = Prims.Mat(Dark, 0.05f);
            var timberMat = Prims.Mat(Timber);
            var roofMat = Prims.Mat(roofColor, 0.08f, 0.3f);

            Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.7f, 0.3f, 2.2f), stoneMat);
            Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(2.55f, 0.4f, 2.05f), stoneMat);
            Prims.Box(t, new Vector3(0, 1.3f, 0), new Vector3(2.5f, 2.0f, 2.0f), darkMat);
            Prims.Box(t, new Vector3(0, 2.35f, 0), new Vector3(2.8f, 0.1f, 2.3f), timberMat);
            Prims.Box(t, new Vector3(0, 2.45f, 0), new Vector3(2.7f, 0.3f, 2.2f), roofMat);
            Prims.Box(t, new Vector3(0, 1.0f, -1.03f), new Vector3(0.8f, 1.4f, 0.06f), Prims.Mat(Door));
        }

        Prims.BlobShadow(t, 1.5f);
        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject ArcheryRange(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("ArcheryRange", parent, worldPos, BuildingType.ArcheryRange,
            new Vector3(0, 1.2f, 0), new Vector3(2.6f, 2.4f, 2.2f));
        var t = g.transform;
        var woodMat = Prims.Mat(Prims.Hex(0x7a5230), 0.05f);
        var darkMat = Prims.Mat(Prims.Hex(0x5a3a20), 0.05f);
        var roofMat = Prims.Mat(roofColor, 0.05f, 0.25f);

        Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.6f, 0.3f, 2.2f), Prims.Mat(Stone, 0.05f));
        Prims.Box(t, new Vector3(-0.6f, 1.1f, 0), new Vector3(1.2f, 1.6f, 2.0f), woodMat);   // covered half
        Prims.Box(t, new Vector3(-0.6f, 2.0f, 0), new Vector3(1.4f, 0.2f, 2.2f), roofMat);   // roof
        // open practice area posts + target butt
        foreach (var pz in new[] { -0.9f, 0.9f })
            Prims.Box(t, new Vector3(0.9f, 0.7f, pz), new Vector3(0.12f, 1.4f, 0.12f), darkMat);
        Prims.Cylinder(t, new Vector3(0.95f, 0.7f, 0), 0.4f, 0.2f, Prims.Mat(Prims.Hex(0xd9c9a0)));
        Prims.Cylinder(t, new Vector3(0.97f, 0.7f, 0), 0.18f, 0.22f, Prims.Mat(Prims.Hex(0xc0392b)));
        KenneyModels.Spawn("FantasyTown/fence", t, new Vector3(1.12f, 0, -0.92f), 0.75f, 90f);
        KenneyModels.Spawn("FantasyTown/fence", t, new Vector3(1.12f, 0,  0.92f), 0.75f, 90f);
        Prims.Box(t, new Vector3(0.34f, 0.52f, -0.82f), new Vector3(0.08f, 0.08f, 0.9f), Prims.Mat(Prims.Hex(0x6b4a2a)));
        Prims.Box(t, new Vector3(0.20f, 0.78f, -0.82f), new Vector3(0.05f, 0.05f, 0.75f), Prims.Mat(Prims.Hex(0xb8b8c0), 0.4f, 0.45f));
        Prims.Box(t, new Vector3(0.52f, 0.78f, -0.82f), new Vector3(0.05f, 0.05f, 0.75f), Prims.Mat(Prims.Hex(0xb8b8c0), 0.4f, 0.45f));

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Stable(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("Stable", parent, worldPos, BuildingType.Stable,
            new Vector3(0, 1.2f, 0), new Vector3(2.8f, 2.4f, 2.4f));
        var t = g.transform;
        var woodMat = Prims.Mat(Prims.Hex(0x6e4a28), 0.05f);
        var darkMat = Prims.Mat(Prims.Hex(0x4a3018), 0.05f);
        var roofMat = Prims.Mat(roofColor, 0.05f, 0.25f);

        Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.8f, 0.3f, 2.4f), Prims.Mat(Stone, 0.05f));
        Prims.Box(t, new Vector3(0, 1.1f, 0), new Vector3(2.6f, 1.6f, 2.2f), woodMat);        // barn body
        Prims.Box(t, new Vector3(0, 2.05f, 0), new Vector3(2.9f, 0.25f, 2.5f), roofMat);      // roof
        // two stall openings on the front
        foreach (var ox in new[] { -0.6f, 0.6f })
            Prims.Box(t, new Vector3(ox, 0.85f, -1.12f), new Vector3(0.7f, 1.1f, 0.06f), darkMat);
        // hay bale
        Prims.Box(t, new Vector3(1.05f, 0.55f, -1.0f), new Vector3(0.5f, 0.5f, 0.5f), Prims.Mat(Prims.Hex(0xc9a227)));
        KenneyModels.Spawn("FantasyTown/cart", t, new Vector3(-1.1f, 0, 0.6f), 1.1f, 20f); // hay cart
        // Low-risk visual polish: a tiny corral and animal prop beside the barn.
        KenneyModels.Spawn("FantasyTown/fence", t, new Vector3(1.25f, 0, 0.65f), 0.85f, 90f);
        KenneyModels.Spawn("FantasyTown/fence", t, new Vector3(0.65f, 0, 1.15f), 0.85f, 0f);
        if (QuaterniusAnimal("Horse", t, new Vector3(0.95f, 0f, 0.82f), 0.055f, 65f) == null)
            Prims.Box(t, new Vector3(0.95f, 0.42f, 0.82f), new Vector3(0.28f, 0.34f, 0.58f), Prims.Mat(Prims.Hex(0x5a4632)));

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Farm(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("Farm", parent, worldPos, BuildingType.Farm,
            new Vector3(0, 0.25f, 0), new Vector3(3.0f, 0.5f, 3.0f));
        var t = g.transform;
        var soil = Prims.Mat(Prims.Hex(0x6b4a2a), 0f, 0.05f);
        var crop = Prims.Mat(Prims.Hex(0x8fae45), 0f, 0.1f);
        var fence = Prims.Mat(Prims.Hex(0x6e4a28));

        Prims.Box(t, new Vector3(0, 0.06f, 0), new Vector3(3.0f, 0.12f, 3.0f), soil);          // tilled field
        for (int i = -1; i <= 1; i++)                                                          // crop rows
            Prims.Box(t, new Vector3(i * 0.8f, 0.18f, 0), new Vector3(0.4f, 0.18f, 2.6f), crop);
        foreach (var fx in new[] { -1.45f, 1.45f })                                            // fence posts
            foreach (var fz in new[] { -1.45f, 1.45f })
                Prims.Box(t, new Vector3(fx, 0.3f, fz), new Vector3(0.1f, 0.5f, 0.1f), fence);
        KenneyModels.Spawn("FantasyTown/fence", t, new Vector3(0, 0, -1.55f), 1.0f, 90f);
        KenneyModels.Spawn("FantasyTown/fence", t, new Vector3(0, 0,  1.55f), 1.0f, 90f);
        Prims.Cylinder(t, new Vector3(-1.05f, 0.55f, 1.0f), 0.04f, 0.9f, fence);
        Prims.Box(t, new Vector3(-1.05f, 0.82f, 1.0f), new Vector3(0.62f, 0.05f, 0.05f), fence);
        Prims.Box(t, new Vector3(-1.05f, 0.62f, 1.0f), new Vector3(0.34f, 0.40f, 0.08f), Prims.Mat(roofColor, 0.02f, 0.22f));
        Prims.Box(t, new Vector3(1.1f, 0.24f, 1.0f), new Vector3(0.38f, 0.28f, 0.34f), Prims.Mat(Prims.Hex(0xc9a227), 0.02f, 0.18f));

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject LumberCamp(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("LumberCamp", parent, worldPos, BuildingType.LumberCamp,
            new Vector3(0, 0.7f, 0), new Vector3(2.2f, 1.4f, 2.2f));
        var t = g.transform;
        var logMat  = Prims.Mat(Prims.Hex(0x6b4226), 0.05f);
        var plankMat = Prims.Mat(Prims.Hex(0x9b7240), 0.05f);
        var roofMat = Prims.Mat(roofColor, 0.05f, 0.25f);

        Prims.Box(t, new Vector3(0, 0.1f, 0), new Vector3(2.2f, 0.2f, 2.2f), Prims.Mat(Stone, 0.05f)); // base
        Prims.Box(t, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 0.8f, 1.4f), plankMat);                // hut
        Prims.Box(t, new Vector3(0, 1.15f, 0), new Vector3(1.7f, 0.18f, 1.7f), roofMat);               // roof
        // log pile beside the hut
        foreach (var lz in new[] { -0.85f, -0.6f, -0.35f })
            Prims.Cylinder(t, new Vector3(0.85f, 0.35f, lz), 0.16f, 1.0f, logMat)
                 .transform.localRotation = Quaternion.Euler(0, 0, 90f);
        // FantasyTown cart parked beside the camp
        KenneyModels.Spawn("FantasyTown/cart-high", t, new Vector3(-0.9f, 0, 0.8f), 1.2f, 45f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject MiningCamp(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("MiningCamp", parent, worldPos, BuildingType.MiningCamp,
            new Vector3(0, 0.7f, 0), new Vector3(2.2f, 1.4f, 2.2f));
        var t = g.transform;
        var stoneMat = Prims.Mat(Stone, 0.05f);
        var darkMat  = Prims.Mat(Prims.Hex(0x5a5048), 0.05f);
        var roofMat  = Prims.Mat(roofColor, 0.05f, 0.25f);

        Prims.Box(t, new Vector3(0, 0.1f, 0), new Vector3(2.2f, 0.2f, 2.2f), stoneMat);   // base
        Prims.Box(t, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 0.8f, 1.4f), darkMat);    // hut
        Prims.Box(t, new Vector3(0, 1.15f, 0), new Vector3(1.7f, 0.18f, 1.7f), roofMat);  // roof
        // ore sacks / rubble pile beside the hut
        Prims.Sphere(t, new Vector3(0.8f, 0.3f, -0.7f), 0.3f, stoneMat);
        Prims.Sphere(t, new Vector3(0.95f, 0.28f, -0.35f), 0.26f, darkMat);
        Prims.Box(t, new Vector3(0.8f, 0.25f, 0.6f), new Vector3(0.5f, 0.5f, 0.4f), Prims.Mat(Prims.Hex(0x6e4a28))); // crate
        KenneyModels.Spawn("FantasyTown/cart-high", t, new Vector3(-0.85f, 0, 0.75f), 1.0f, 35f); // ore cart
        Prims.Box(t, new Vector3(-0.82f, 0.58f, -0.92f), new Vector3(0.86f, 0.78f, 0.12f), darkMat);
        Prims.Cone(t, new Vector3(-0.82f, 0.92f, -0.98f), 0.50f, 0.46f, 6, stoneMat, 0f);
        Prims.Cylinder(t, new Vector3(-0.22f, 0.20f, 1.02f), 0.07f, 1.15f, Prims.Mat(Prims.Hex(0x4a3018), 0.05f))
            .transform.localRotation = Quaternion.Euler(0, 0, 90f);
        Prims.Cylinder(t, new Vector3(0.18f, 0.20f, 1.02f), 0.07f, 1.15f, Prims.Mat(Prims.Hex(0x4a3018), 0.05f))
            .transform.localRotation = Quaternion.Euler(0, 0, 90f);
        KenneyModels.Spawn("FantasyTown/wheel", t, new Vector3(-0.25f, 0.16f, 0.72f), 0.65f, 90f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Mill(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("Mill", parent, worldPos, BuildingType.Mill,
            new Vector3(0, 1.0f, 0), new Vector3(2.0f, 2.0f, 2.0f));
        var t = g.transform;
        var wallMat = Prims.Mat(Plaster);
        var woodMat = Prims.Mat(Prims.Hex(0x6e4a28), 0.05f);
        var roofMat = Prims.Mat(roofColor, 0.05f, 0.25f);

        // FantasyTown market stall as the mill building body; procedural otherwise.
        if (KenneyModels.Spawn("FantasyTown/stall-red", t, new Vector3(0, 0, 0), 1.8f) == null)
        {
            Prims.Box(t, new Vector3(0, 0.1f, 0), new Vector3(2.0f, 0.2f, 2.0f), Prims.Mat(Stone, 0.05f)); // base
            Prims.Box(t, new Vector3(0, 0.9f, 0), new Vector3(1.4f, 1.4f, 1.4f), wallMat);                 // body
            Prims.Cone(t, new Vector3(0, 1.6f, 0), 1.2f, 0.8f, 4, roofMat, 45f);                           // roof
        }
        // Windmill blades always show so the building reads as a Mill.
        Prims.Cylinder(t, new Vector3(0, 1.2f, -0.9f), 0.1f, 0.3f, woodMat)
             .transform.localRotation = Quaternion.Euler(90f, 0, 0);
        for (int i = 0; i < 4; i++)
        {
            var blade = Prims.Box(t, new Vector3(0, 1.2f, -1.0f), new Vector3(0.18f, 1.1f, 0.04f), woodMat);
            blade.transform.localRotation = Quaternion.Euler(0, 0, i * 45f + 45f);
        }

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Market(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("Market", parent, worldPos, BuildingType.Market,
            new Vector3(0, 1.0f, 0), new Vector3(2.8f, 2.0f, 2.6f));
        var t = g.transform;
        var woodMat  = Prims.Mat(Prims.Hex(0x8a5a2b), 0.05f);
        var plankMat = Prims.Mat(Prims.Hex(0xb98a4e), 0.05f);
        var awningMat = Prims.Mat(roofColor, 0.05f, 0.3f);

        // Two FantasyTown stalls side by side (red + green) make a market row.
        if (KenneyModels.Spawn("FantasyTown/stall-red",   t, new Vector3(-0.9f, 0, 0), 1.8f) == null)
        {
            Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.8f, 0.3f, 2.6f), Prims.Mat(Stone, 0.05f)); // platform
            Prims.Box(t, new Vector3(-0.7f, 0.95f, 0), new Vector3(1.2f, 1.4f, 2.2f), plankMat);            // back stall
            Prims.Box(t, new Vector3(0.5f, 0.7f, 0), new Vector3(0.9f, 0.5f, 2.0f), woodMat);               // counter
            foreach (var pz in new[] { -0.95f, 0.95f })
                Prims.Box(t, new Vector3(0.95f, 0.85f, pz), new Vector3(0.1f, 1.7f, 0.1f), woodMat);
            var awning = Prims.Box(t, new Vector3(0.35f, 1.75f, 0), new Vector3(1.9f, 0.1f, 2.4f), awningMat);
            awning.transform.localRotation = Quaternion.Euler(0, 0, 14f);
            Prims.Box(t, new Vector3(0.5f, 1.1f, -0.6f), new Vector3(0.4f, 0.4f, 0.4f), Prims.Mat(Prims.Hex(0xc9a227)));
            Prims.Box(t, new Vector3(0.5f, 1.1f, 0.0f),  new Vector3(0.4f, 0.4f, 0.4f), woodMat);
            Prims.Sphere(t, new Vector3(0.5f, 1.05f, 0.6f), 0.22f, Prims.Mat(Prims.Hex(0xb9b9b9)));
        }
        else
        {
            KenneyModels.Spawn("FantasyTown/stall-green", t, new Vector3( 0.9f, 0, 0), 1.8f);
            KenneyModels.Spawn("FantasyTown/cart",        t, new Vector3( 0.9f, 0, -1.4f), 1.4f);
        }
        // Trade cue: donkey/cart traffic staged outside the stalls.
        KenneyModels.Spawn("FantasyTown/cart-high", t, new Vector3(-1.20f, 0, -1.25f), 0.95f, -25f);
        if (QuaterniusAnimal("Donkey", t, new Vector3(-1.65f, 0f, -1.05f), 0.055f, 135f) == null)
            Prims.Box(t, new Vector3(-1.65f, 0.36f, -1.05f), new Vector3(0.24f, 0.30f, 0.48f), Prims.Mat(Prims.Hex(0x8a6a4a)));
        Prims.Box(t, new Vector3(-0.68f, 0.62f, -1.20f), new Vector3(0.38f, 0.26f, 0.05f), awningMat);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Castle(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("Castle", parent, worldPos, BuildingType.Castle,
            new Vector3(0, 2.5f, 0), new Vector3(4.5f, 5.0f, 4.5f));
        var t = g.transform;

        // Central keep: base + mid + roof stacked from Castle Kit.
        bool hasKenney =
            KenneyModels.Spawn("Castle/tower-square-base", t, new Vector3(0, 0, 0),    3.6f) != null;
        if (hasKenney)
        {
            KenneyModels.Spawn("Castle/tower-square-mid",  t, new Vector3(0, 1.55f, 0), 3.6f);
            KenneyModels.Spawn("Castle/tower-square-roof", t, new Vector3(0, 3.15f, 0), 3.6f);
            // Four corner towers.
            foreach (var cx in new[] { -2.0f, 2.0f })
                foreach (var cz in new[] { -2.0f, 2.0f })
                    KenneyModels.Spawn("Castle/tower-square", t, new Vector3(cx, 0, cz), 1.8f);
        }
        else
        {
            // Procedural fallback.
            var stoneMat = Prims.Mat(Stone, 0.05f);
            var roofMat  = Prims.Mat(roofColor, 0.05f, 0.3f);
            Prims.Box(t, new Vector3(0, 0.3f, 0), new Vector3(4.6f, 0.6f, 4.6f), stoneMat);
            Prims.Box(t, new Vector3(0, 2.2f, 0), new Vector3(3.6f, 3.6f, 3.6f), stoneMat);
            foreach (var cx in new[] { -2.0f, 2.0f })
                foreach (var cz in new[] { -2.0f, 2.0f })
                {
                    Prims.Cylinder(t, new Vector3(cx, 2.7f, cz), 0.9f, 5.4f, stoneMat);
                    Prims.Cone(t, new Vector3(cx, 5.4f, cz), 1.05f, 1.6f, 8, roofMat, 45f);
                }
        }

        // Team flag always procedural so team colour is visible.
        Prims.Cylinder(t, new Vector3(0, 5.6f, 0), 0.06f, 1.6f, Prims.Mat(Prims.Hex(0x4a3a2a), 0.15f));
        Prims.Box(t, new Vector3(0.45f, 6.1f, 0), new Vector3(0.9f, 0.55f, 0.04f),
            Prims.Mat(roofColor, 0, 0.4f)).name = "Flag";

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Wall(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("Wall", parent, worldPos, BuildingType.Wall,
            new Vector3(0, 0.8f, 0), new Vector3(1.6f, 1.6f, 1.6f));
        var t = g.transform;

        if (KenneyModels.Spawn("Castle/wall-narrow", t, new Vector3(0, 0, 0), 1.7f) == null)
        {
            // Procedural fallback.
            var stoneMat = Prims.Mat(Stone, 0.05f);
            var capMat   = Prims.Mat(Dark, 0.05f);
            Prims.Box(t, new Vector3(0, 0.1f, 0),  new Vector3(1.7f, 0.2f, 1.7f), stoneMat);
            Prims.Box(t, new Vector3(0, 0.85f, 0), new Vector3(1.5f, 1.5f, 1.5f), stoneMat);
            Prims.Box(t, new Vector3(0, 1.6f, 0),  new Vector3(1.7f, 0.16f, 1.7f), capMat);
            foreach (var cx in new[] { -0.6f, 0.6f })
                foreach (var cz in new[] { -0.6f, 0.6f })
                    Prims.Box(t, new Vector3(cx, 1.78f, cz), new Vector3(0.34f, 0.26f, 0.34f), stoneMat);
        }

        // NavMesh obstacle — always needed regardless of visual.
        var o = g.AddComponent<NavMeshObstacle>();
        o.carving = true;
        o.shape   = NavMeshObstacleShape.Box;
        o.center  = new Vector3(0, 0.8f, 0);
        o.size    = new Vector3(1.7f, 1.6f, 1.7f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Gate(Transform parent, Vector3 worldPos, Color teamColor)
    {
        // Passable cell — no NavMeshObstacle so units walk through.
        var g = NewBuilding("Gate", parent, worldPos, BuildingType.Gate,
            new Vector3(0, 1.1f, 0), new Vector3(1.6f, 2.4f, 1.6f));
        var t = g.transform;

        if (KenneyModels.Spawn("Castle/gate", t, new Vector3(0, 0, 0), 2.8f, 90f) == null)
        {
            // Procedural fallback.
            var stoneMat = Prims.Mat(Stone, 0.05f);
            var woodMat  = Prims.Mat(Door);
            var accent   = Prims.Mat(teamColor, 0.05f, 0.3f);
            foreach (var cx in new[] { -0.62f, 0.62f })
                foreach (var cz in new[] { -0.62f, 0.62f })
                {
                    Prims.Box(t, new Vector3(cx, 0.1f, cz), new Vector3(0.5f, 0.2f, 0.5f), stoneMat);
                    Prims.Box(t, new Vector3(cx, 1.1f, cz), new Vector3(0.4f, 2.0f, 0.4f), stoneMat);
                    Prims.Box(t, new Vector3(cx, 2.2f, cz), new Vector3(0.48f, 0.24f, 0.48f), accent);
                }
            Prims.Box(t, new Vector3(0, 2.15f,  0.62f), new Vector3(1.6f, 0.3f, 0.4f), woodMat);
            Prims.Box(t, new Vector3(0, 2.15f, -0.62f), new Vector3(1.6f, 0.3f, 0.4f), woodMat);
        }

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Wonder(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("Wonder", parent, worldPos, BuildingType.Wonder,
            new Vector3(0, 3.0f, 0), new Vector3(5.0f, 6.0f, 5.0f));
        var t = g.transform;

        var stoneMat = Prims.Mat(Stone, 0.05f);
        var goldMat  = Prims.Mat(Prims.Hex(0xf2c14e), 0.1f, 0.5f);
        var accent   = Prims.Mat(teamColor, 0.05f, 0.3f);

        // Stepped plinth + tapered tower topped with a golden spire — a monument
        // silhouette that reads clearly as the most important building on the map.
        Prims.Box(t, new Vector3(0, 0.3f, 0), new Vector3(5.0f, 0.6f, 5.0f), stoneMat);
        Prims.Box(t, new Vector3(0, 0.9f, 0), new Vector3(4.0f, 0.6f, 4.0f), stoneMat);
        Prims.Box(t, new Vector3(0, 2.6f, 0), new Vector3(3.0f, 3.0f, 3.0f), stoneMat);
        Prims.Box(t, new Vector3(0, 4.4f, 0), new Vector3(2.0f, 0.8f, 2.0f), accent);
        Prims.Cone(t, new Vector3(0, 5.2f, 0), 1.3f, 2.4f, 12, goldMat, 0f);

        // Four corner pillars for grandeur.
        foreach (var cx in new[] { -2.1f, 2.1f })
            foreach (var cz in new[] { -2.1f, 2.1f })
            {
                Prims.Cylinder(t, new Vector3(cx, 1.6f, cz), 0.45f, 3.2f, stoneMat);
                Prims.Box(t, new Vector3(cx, 3.3f, cz), new Vector3(1.0f, 0.3f, 1.0f), goldMat);
            }
        KenneyModels.Spawn("Castle/flag-banner-long", t, new Vector3(-1.65f, 3.2f, -0.05f), 1.2f, 90f);
        KenneyModels.Spawn("Castle/flag-banner-long", t, new Vector3( 1.65f, 3.2f, -0.05f), 1.2f, -90f);
        Prims.Box(t, new Vector3(0, 0.18f, -2.65f), new Vector3(2.6f, 0.22f, 0.7f), stoneMat);
        Prims.Box(t, new Vector3(0, 0.46f, -2.35f), new Vector3(2.1f, 0.24f, 0.55f), stoneMat);
        Prims.Box(t, new Vector3(0, 0.76f, -2.08f), new Vector3(1.5f, 0.22f, 0.45f), accent);

        Prims.EnableShadows(g);
        return g;
    }

    /// <summary>Shared root setup for the simple building factories above.</summary>
    static GameObject NewBuilding(string name, Transform parent, Vector3 worldPos,
        BuildingType type, Vector3 colCenter, Vector3 colSize)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var be = g.AddComponent<BuildingEntity>();
        be.type = type;
        be.teamId = 0;
        var col = g.AddComponent<BoxCollider>();
        col.center = colCenter;
        col.size   = colSize;
        Prims.BlobShadow(g.transform, Mathf.Max(colSize.x, colSize.z) * 0.55f);
        return g;
    }

    public static GameObject WatchTower(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("WatchTower", parent, worldPos, BuildingType.WatchTower,
            new Vector3(0, 1.5f, 0), new Vector3(1.4f, 3.2f, 1.4f));
        var t = g.transform;

        if (!KenneyKeep(t, 1.3f, midSections: 0, turrets: false))
        {
            var stoneMat = Prims.Mat(Stone, 0.05f);
            var roofMat  = Prims.Mat(teamColor, 0.05f, 0.3f);
            Prims.Cylinder(t, new Vector3(0, 1.6f, 0), 0.65f, 3.2f, stoneMat);
            Prims.Box(t, new Vector3(0, 3.3f, 0), new Vector3(1.5f, 0.15f, 1.5f), stoneMat);
            foreach (var cx in new[] { -0.55f, 0.55f })
                foreach (var cz in new[] { -0.55f, 0.55f })
                    Prims.Box(t, new Vector3(cx, 3.5f, cz), new Vector3(0.28f, 0.32f, 0.28f), stoneMat);
            Prims.Cone(t, new Vector3(0, 3.55f, 0), 0.85f, 0.9f, 8, roofMat, 0f);
        }
        AddWindow(t, new Vector3(0, 2.05f, -0.68f), 0, Prims.Mat(Window, 0.1f, 0.4f), Prims.Mat(Timber));
        Prims.Cylinder(t, new Vector3(0.52f, 3.9f, 0), 0.035f, 0.8f, Prims.Mat(Timber));
        Prims.Box(t, new Vector3(0.78f, 4.12f, 0), new Vector3(0.44f, 0.26f, 0.03f), Prims.Mat(teamColor, 0.05f, 0.3f));
        Prims.Box(t, new Vector3(-0.72f, 0.75f, -0.72f), new Vector3(0.08f, 1.1f, 0.08f), Prims.Mat(Timber))
            .transform.localRotation = Quaternion.Euler(0, 0, -12f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Outpost(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("Outpost", parent, worldPos, BuildingType.Outpost,
            new Vector3(0, 1.2f, 0), new Vector3(1.0f, 2.6f, 1.0f));
        var t = g.transform;
        var wood  = Prims.Mat(Prims.Hex(0x6b4a2a), 0.05f);
        var roof  = Prims.Mat(teamColor, 0.05f, 0.3f);

        Prims.Box(t, new Vector3(0, 0.1f, 0), new Vector3(0.9f, 0.2f, 0.9f), Prims.Mat(Stone, 0.05f));
        foreach (var cx in new[] { -0.32f, 0.32f })
            foreach (var cz in new[] { -0.32f, 0.32f })
                Prims.Box(t, new Vector3(cx, 1.1f, cz), new Vector3(0.14f, 2.0f, 0.14f), wood); // legs
        Prims.Box(t, new Vector3(0, 2.2f, 0), new Vector3(1.0f, 0.5f, 1.0f), wood);             // platform
        Prims.Cone(t, new Vector3(0, 2.7f, 0), 0.7f, 0.5f, 4, roof, 45f);                       // small roof
        Prims.Box(t, new Vector3(-0.52f, 1.15f, -0.52f), new Vector3(0.08f, 1.7f, 0.08f), wood)
            .transform.localRotation = Quaternion.Euler(0, 0, -18f);
        Prims.Box(t, new Vector3(0.52f, 1.15f, -0.52f), new Vector3(0.08f, 1.7f, 0.08f), wood)
            .transform.localRotation = Quaternion.Euler(0, 0, 18f);
        Prims.Cylinder(t, new Vector3(0.38f, 2.85f, 0), 0.03f, 0.65f, wood);
        Prims.Box(t, new Vector3(0.62f, 3.02f, 0), new Vector3(0.42f, 0.26f, 0.03f), roof);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject BombardTower(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("BombardTower", parent, worldPos, BuildingType.BombardTower,
            new Vector3(0, 1.6f, 0), new Vector3(1.6f, 3.4f, 1.6f));
        var t = g.transform;
        var darkMat  = Prims.Mat(Prims.Hex(0x33363c), 0.4f, 0.6f);
        var roofMat  = Prims.Mat(teamColor, 0.05f, 0.3f);

        if (!KenneyKeep(t, 1.3f, midSections: 1, turrets: false))
        {
            var stoneMat = Prims.Mat(Stone, 0.05f);
            Prims.Cylinder(t, new Vector3(0, 1.6f, 0), 0.8f, 3.2f, stoneMat);
            Prims.Box(t, new Vector3(0, 3.3f, 0), new Vector3(1.8f, 0.2f, 1.8f), stoneMat);
            foreach (var cx in new[] { -0.7f, 0.7f })
                foreach (var cz in new[] { -0.7f, 0.7f })
                    Prims.Box(t, new Vector3(cx, 3.55f, cz), new Vector3(0.32f, 0.4f, 0.32f), stoneMat);
        }
        // Cannon barrel and banner always show — they identify this as a BombardTower.
        Prims.Cylinder(t, new Vector3(0, 3.5f, 0.7f), 0.22f, 1.0f, darkMat)
            .transform.localRotation = Quaternion.Euler(70f, 0, 0);
        Prims.Box(t, new Vector3(0, 3.0f, 0), new Vector3(1.6f, 0.2f, 1.6f), roofMat);
        KenneyModels.Spawn("Castle/flag-wide", t, new Vector3(-0.72f, 3.25f, 0.05f), 0.75f, 90f);
        Prims.Sphere(t, new Vector3(0.62f, 0.45f, -0.65f), 0.18f, darkMat);
        Prims.Sphere(t, new Vector3(0.92f, 0.38f, -0.50f), 0.16f, darkMat);
        Prims.Box(t, new Vector3(-0.65f, 0.38f, -0.70f), new Vector3(0.45f, 0.32f, 0.35f), Prims.Mat(Prims.Hex(0x6b4a2a), 0.05f, 0.18f));
        KenneyModels.Spawn("FantasyTown/wheel", t, new Vector3(0.0f, 3.28f, 0.62f), 0.55f, 90f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Blacksmith(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("Blacksmith", parent, worldPos, BuildingType.Blacksmith,
            new Vector3(0, 1.0f, 0), new Vector3(2.2f, 2.0f, 2.2f));
        var t = g.transform;
        var darkMat  = Prims.Mat(Prims.Hex(0x2c2018), 0.05f);
        var stoneMat = Prims.Mat(Stone, 0.05f);
        var roofMat  = Prims.Mat(teamColor, 0.05f, 0.25f);

        Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.2f, 0.3f, 2.2f), stoneMat);
        Prims.Box(t, new Vector3(0, 1.0f, 0), new Vector3(2.0f, 1.4f, 2.0f), darkMat);
        Prims.Box(t, new Vector3(0, 1.8f, 0), new Vector3(2.2f, 0.15f, 2.2f), roofMat);
        if (KenneyModels.Spawn("FantasyTown/chimney", t, new Vector3(0.6f, 1.95f, 0.3f), 0.9f) == null)
            Prims.Cylinder(t, new Vector3(0.6f, 2.2f, 0.3f), 0.18f, 1.0f, darkMat); // fallback chimney
        var emberMat = Prims.Mat(Prims.Hex(0xff6a18), 0.05f, 0.8f);
        Prims.Box(t, new Vector3(-0.72f, 0.72f, -0.88f), new Vector3(0.55f, 0.32f, 0.34f), stoneMat); // forge mouth
        Prims.Box(t, new Vector3(-0.72f, 0.72f, -1.06f), new Vector3(0.42f, 0.20f, 0.04f), emberMat);
        Prims.Box(t, new Vector3(0.72f, 0.46f, -0.78f), new Vector3(0.42f, 0.18f, 0.28f), Prims.Mat(Prims.Hex(0x55585e), 0.5f, 0.55f)); // anvil
        Prims.Box(t, new Vector3(0.72f, 0.24f, -0.78f), new Vector3(0.18f, 0.28f, 0.18f), stoneMat);
        KenneyModels.Spawn("FantasyTown/wheel", t, new Vector3(1.03f, 0.2f, 0.86f), 0.75f, 20f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject SiegeWorkshop(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("SiegeWorkshop", parent, worldPos, BuildingType.SiegeWorkshop,
            new Vector3(0, 1.0f, 0), new Vector3(2.6f, 2.0f, 2.6f));
        var t = g.transform;
        var wood   = Prims.Mat(Prims.Hex(0x6b4a2a), 0.05f);
        var stone  = Prims.Mat(Stone, 0.05f);
        var metal  = Prims.Mat(Prims.Hex(0x55585e), 0.4f, 0.6f);
        var accent = Prims.Mat(teamColor, 0.05f, 0.3f);

        Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.6f, 0.3f, 2.6f), stone);   // foundation
        Prims.Box(t, new Vector3(0, 0.9f, -0.4f), new Vector3(2.4f, 1.2f, 1.6f), wood); // open workshop shed
        Prims.Box(t, new Vector3(0, 1.6f, -0.4f), new Vector3(2.6f, 0.18f, 1.8f), accent); // roof
        // A half-built ram/log on the work floor + a stacked boulder.
        Prims.Cylinder(t, new Vector3(0.2f, 0.55f, 0.7f), 0.16f, 1.4f, wood)
            .transform.localRotation = Quaternion.Euler(0, 0, 90f);
        Prims.Box(t, new Vector3(0.95f, 0.5f, 0.7f), new Vector3(0.3f, 0.3f, 0.3f), metal); // ram head
        Prims.Sphere(t, new Vector3(-0.9f, 0.4f, 0.8f), 0.25f, stone);                       // boulder
        KenneyModels.Spawn("FantasyTown/cart-high", t, new Vector3(-0.8f, 0, -0.85f), 1.0f); // supply cart
        KenneyModels.Spawn("FantasyTown/wheel", t, new Vector3(0.95f, 0.2f, -0.05f), 0.8f, 90f);
        KenneyModels.Spawn("FantasyTown/wheel", t, new Vector3(1.12f, 0.2f, -0.05f), 0.8f, 90f);
        Prims.Box(t, new Vector3(-1.05f, 0.48f, 0.18f), new Vector3(0.12f, 0.12f, 1.35f), wood)
            .transform.localRotation = Quaternion.Euler(0, 35f, 0);
        Prims.Box(t, new Vector3(-0.65f, 0.44f, 0.38f), new Vector3(0.12f, 0.12f, 1.05f), wood)
            .transform.localRotation = Quaternion.Euler(0, 35f, 0);
        Prims.Box(t, new Vector3(0.3f, 0.52f, -1.1f), new Vector3(0.75f, 0.35f, 0.32f), Prims.Mat(Prims.Hex(0x8a5a2b), 0.05f, 0.18f)); // parts crate

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Monastery(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("Monastery", parent, worldPos, BuildingType.Monastery,
            new Vector3(0, 1.2f, 0), new Vector3(2.4f, 2.4f, 2.4f));
        var t = g.transform;
        var wallMat  = Prims.Mat(Plaster);
        var stoneMat = Prims.Mat(Stone, 0.05f);
        var accent   = Prims.Mat(teamColor, 0.05f, 0.3f);

        Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.4f, 0.3f, 2.4f), stoneMat);
        Prims.Box(t, new Vector3(0, 1.1f, 0), new Vector3(2.2f, 1.6f, 2.2f), wallMat);
        Prims.Cone(t, new Vector3(0, 2.0f, 0), 1.8f, 1.0f, 8, accent, 0f);
        Prims.Cylinder(t, new Vector3(0, 3.5f, 0), 0.07f, 0.8f, stoneMat);    // cross post
        Prims.Box(t, new Vector3(0, 3.7f, 0), new Vector3(0.5f, 0.08f, 0.08f), stoneMat); // cross bar
        Prims.Box(t, new Vector3(0, 0.75f, -1.12f), new Vector3(0.58f, 0.9f, 0.06f), Prims.Mat(Door));
        Prims.Cylinder(t, new Vector3(0, 1.22f, -1.15f), 0.33f, 0.08f, stoneMat)
            .transform.localRotation = Quaternion.Euler(90f, 0, 0);
        AddWindow(t, new Vector3(-0.85f, 1.35f, -1.12f), 0, Prims.Mat(Window, 0.1f, 0.4f), stoneMat);
        AddWindow(t, new Vector3( 0.85f, 1.35f, -1.12f), 0, Prims.Mat(Window, 0.1f, 0.4f), stoneMat);
        KenneyModels.Spawn("Castle/flag-pennant", t, new Vector3(0.58f, 2.85f, 0.2f), 0.8f, 0f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject University(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("University", parent, worldPos, BuildingType.University,
            new Vector3(0, 1.3f, 0), new Vector3(2.6f, 2.6f, 2.6f));
        var t = g.transform;
        var wallMat  = Prims.Mat(Plaster);
        var stoneMat = Prims.Mat(Stone, 0.05f);
        var roofMat  = Prims.Mat(teamColor, 0.05f, 0.3f);
        var winMat   = Prims.Mat(Window, 0.1f, 0.4f);
        var timberMat = Prims.Mat(Timber);

        Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(2.6f, 0.3f, 2.6f), stoneMat);
        Prims.Box(t, new Vector3(0, 1.2f, 0), new Vector3(2.4f, 1.8f, 2.4f), wallMat);
        Prims.Box(t, new Vector3(0, 2.2f, 0), new Vector3(2.6f, 0.15f, 2.6f), timberMat);
        Prims.Cone(t, new Vector3(0, 2.25f, 0), 2.0f, 1.1f, 4, roofMat, 45f);
        AddWindow(t, new Vector3(0, 1.4f, -1.22f), 0, winMat, timberMat);
        AddWindow(t, new Vector3(0, 1.4f,  1.22f), 0, winMat, timberMat);
        foreach (var x in new[] { -1.0f, 1.0f })
        {
            Prims.Cylinder(t, new Vector3(x, 0.9f, -1.28f), 0.12f, 1.5f, stoneMat);
            Prims.Box(t, new Vector3(x, 0.14f, -1.28f), new Vector3(0.34f, 0.16f, 0.34f), stoneMat);
            Prims.Box(t, new Vector3(x, 1.68f, -1.28f), new Vector3(0.34f, 0.16f, 0.34f), stoneMat);
        }
        Prims.Box(t, new Vector3(-0.55f, 0.46f, -1.05f), new Vector3(0.34f, 0.22f, 0.42f), Prims.Mat(Prims.Hex(0x6e4a28), 0.04f, 0.2f)); // book chest
        Prims.Box(t, new Vector3(-0.55f, 0.68f, -1.05f), new Vector3(0.30f, 0.04f, 0.38f), Prims.Mat(Prims.Hex(0xd8c898), 0.02f, 0.18f));
        KenneyModels.Spawn("Castle/flag", t, new Vector3(0.9f, 2.5f, -0.2f), 0.7f, 0f);

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Dock(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("Dock", parent, worldPos, BuildingType.Dock,
            new Vector3(0, 0.7f, 0), new Vector3(3.0f, 1.4f, 3.0f));
        var t = g.transform;

        var plankMat = Prims.Mat(Prims.Hex(0x7a5230), 0.05f);
        var darkMat  = Prims.Mat(Prims.Hex(0x4a3018), 0.05f);
        var roofMat  = Prims.Mat(teamColor, 0.05f, 0.3f);
        var mastMat  = Prims.Mat(Prims.Hex(0x6e4a28));

        // Dock platform (wooden planks)
        Prims.Box(t, new Vector3(0, 0.3f, 0), new Vector3(3.0f, 0.15f, 3.0f), plankMat);
        // Support piles
        foreach (float px in new[] { -1.1f, 0f, 1.1f })
            foreach (float pz in new[] { -1.1f, 1.1f })
                Prims.Cylinder(t, new Vector3(px, -0.15f, pz), 0.1f, 1.0f, darkMat);
        // Small warehouse/office
        Prims.Box(t, new Vector3(-0.7f, 0.9f, 0), new Vector3(1.2f, 0.9f, 1.3f), plankMat);
        Prims.Box(t, new Vector3(-0.7f, 1.45f, 0), new Vector3(1.35f, 0.18f, 1.45f), roofMat);
        // Flagpole
        Prims.Cylinder(t, new Vector3(-0.7f, 2.4f, 0), 0.04f, 1.4f, mastMat);
        Prims.Box(t, new Vector3(-0.4f, 2.85f, 0), new Vector3(0.65f, 0.4f, 0.04f),
            Prims.Mat(teamColor, 0, 0.4f)).name = "Flag";
        KenneyModels.Spawn("FantasyTown/cart", t, new Vector3(0.9f, 0, -1.0f), 1.1f, -30f); // dockside cart
        KenneyModels.Spawn("FantasyTown/planks", t, new Vector3(0.85f, 0.34f, 0.85f), 0.9f, 90f);
        KenneyModels.Spawn("FantasyTown/planks-half", t, new Vector3(1.25f, 0.34f, -0.35f), 0.9f, 0f);
        Prims.Cylinder(t, new Vector3(1.05f, 0.50f, 1.05f), 0.22f, 0.08f, Prims.Mat(Prims.Hex(0xd9b880)))
            .transform.localRotation = Quaternion.Euler(90f, 0, 0);
        Prims.Box(t, new Vector3(0.55f, 0.56f, -1.18f), new Vector3(0.45f, 0.36f, 0.38f), Prims.Mat(Prims.Hex(0x8a5a2b), 0.05f, 0.18f));
        Prims.Box(t, new Vector3(1.15f, 0.50f, -1.20f), new Vector3(0.36f, 0.28f, 0.30f), Prims.Mat(Prims.Hex(0xc9a227), 0.02f, 0.2f));

        Prims.BlobShadow(t, 1.8f);
        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject FishTrap(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewBuilding("FishTrap", parent, worldPos, BuildingType.FishTrap,
            new Vector3(0, 0.15f, 0), new Vector3(1.2f, 0.3f, 1.2f));
        var t = g.transform;
        var wood  = Prims.Mat(Prims.Hex(0x7a5230), 0.05f);
        var rope  = Prims.Mat(Prims.Hex(0xd9b880));
        var water = Prims.Mat(Prims.Hex(0x4a8ab0), 0.05f, 0.6f);

        // Floating platform
        Prims.Box(t, new Vector3(0, 0.05f, 0), new Vector3(1.1f, 0.1f, 1.1f), wood);
        // Stakes driven into the water floor
        foreach (var px in new[] { -0.42f, 0.42f })
            foreach (var pz in new[] { -0.42f, 0.42f })
                Prims.Cylinder(t, new Vector3(px, -0.15f, pz), 0.06f, 0.5f, wood);
        // Wicker basket trap
        Prims.Cylinder(t, new Vector3(0, 0.22f, 0), 0.28f, 0.28f, rope);
        Prims.Cylinder(t, new Vector3(0, 0.08f, 0), 0.18f, 0.06f, water); // water hole opening
        // Small net marker pole
        Prims.Cylinder(t, new Vector3(0.4f, 0.35f, 0), 0.03f, 0.55f, wood);
        Prims.Box(t, new Vector3(0.55f, 0.6f, 0), new Vector3(0.3f, 0.25f, 0.02f), rope); // flag

        Prims.EnableShadows(g);
        return g;
    }

    /// <summary>
    /// Generic dispatcher used by the placement system and enemy AI to build any
    /// type by enum. Uses <paramref name="teamColor"/> for the roof so buildings
    /// read as team-owned.
    /// </summary>
    public static GameObject Create(BuildingType type, Transform parent, Vector3 worldPos, Color teamColor)
    {
        // Prefer the purpose-built Quaternius model; fall back to the bespoke
        // procedural / Kenney factories for unmapped types (Wall, Gate,
        // Blacksmith, SiegeWorkshop, FishTrap).
        var q = TryQuaternius(type, parent, worldPos, teamColor);
        if (q != null) return q;
        return CreateProcedural(type, parent, worldPos, teamColor);
    }

    static GameObject CreateProcedural(BuildingType type, Transform parent, Vector3 worldPos, Color teamColor) => type switch
    {
        BuildingType.TownCenter   => TownCenter(parent, worldPos, teamColor),
        BuildingType.House        => House(parent, worldPos, teamColor),
        BuildingType.Barracks     => Barracks(parent, worldPos, teamColor),
        BuildingType.ArcheryRange => ArcheryRange(parent, worldPos, teamColor),
        BuildingType.Stable       => Stable(parent, worldPos, teamColor),
        BuildingType.Farm         => Farm(parent, worldPos, teamColor),
        BuildingType.LumberCamp   => LumberCamp(parent, worldPos, teamColor),
        BuildingType.MiningCamp   => MiningCamp(parent, worldPos, teamColor),
        BuildingType.Mill         => Mill(parent, worldPos, teamColor),
        BuildingType.Market       => Market(parent, worldPos, teamColor),
        BuildingType.Castle       => Castle(parent, worldPos, teamColor),
        BuildingType.Wall         => Wall(parent, worldPos, teamColor),
        BuildingType.Gate         => Gate(parent, worldPos, teamColor),
        BuildingType.Wonder       => Wonder(parent, worldPos, teamColor),
        BuildingType.WatchTower   => WatchTower(parent, worldPos, teamColor),
        BuildingType.Blacksmith   => Blacksmith(parent, worldPos, teamColor),
        BuildingType.Monastery    => Monastery(parent, worldPos, teamColor),
        BuildingType.University   => University(parent, worldPos, teamColor),
        BuildingType.Dock         => Dock(parent, worldPos, teamColor),
        BuildingType.SiegeWorkshop => SiegeWorkshop(parent, worldPos, teamColor),
        BuildingType.Outpost      => Outpost(parent, worldPos, teamColor),
        BuildingType.BombardTower => BombardTower(parent, worldPos, teamColor),
        BuildingType.FishTrap     => FishTrap(parent, worldPos, teamColor),
        _                         => House(parent, worldPos, teamColor),
    };

    static void AddWindow(Transform t, Vector3 pos, float yaw, Material winMat, Material frameMat)
    {
        var w = Prims.Box(t, pos, new Vector3(0.5f, 0.6f, 0.02f), winMat);
        w.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var f = Prims.Box(t, pos, new Vector3(0.58f, 0.68f, 0.04f), frameMat);
        f.transform.localRotation = Quaternion.Euler(0, yaw, 0);
    }
}
