using UnityEngine;

/// <summary>
/// Procedural building meshes — a direct C# port of the Three.js BuildingFactory.
/// Each returns a parented GameObject placed at local origin (drop onto ground
/// by setting the returned transform's world position).
/// </summary>
public static class BuildingFactory
{
    static readonly Color Stone = Prims.Hex(0x7a7060);
    static readonly Color Wall  = Prims.Hex(0xc8b898);
    static readonly Color Dark  = Prims.Hex(0x8a7a6a);
    static readonly Color Timber = Prims.Hex(0x4a3020);
    static readonly Color Door  = Prims.Hex(0x5c3a1e);
    static readonly Color Window = Prims.Hex(0x8ab4d8);

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

        var stoneMat = Prims.Mat(Stone, 0.05f);
        var wallMat = Prims.Mat(Wall);
        var timberMat = Prims.Mat(Timber);
        var doorMat = Prims.Mat(Door);
        var roofMat = Prims.Mat(teamColor, 0.05f, 0.3f);
        var winMat = Prims.Mat(Window, 0.1f, 0.7f);

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

        // flag
        Prims.Cylinder(t, new Vector3(0, 4.9f, 0), 0.05f, 2.0f, Prims.Mat(Prims.Hex(0x4a3a2a), 0.15f));
        var flag = Prims.Box(t, new Vector3(0.45f, 5.5f, 0), new Vector3(0.9f, 0.6f, 0.04f), Prims.Mat(teamColor, 0, 0.4f));
        flag.name = "Flag";

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

        var stoneMat = Prims.Mat(Stone, 0.05f);
        var wallMat = Prims.Mat(Wall);
        var timberMat = Prims.Mat(Timber);
        var roofMat = Prims.Mat(roofColor, 0.05f, 0.25f);

        Prims.Box(t, new Vector3(0, 0.15f, 0), new Vector3(1.7f, 0.3f, 1.7f), stoneMat);
        Prims.Box(t, new Vector3(0, 1.05f, 0), new Vector3(1.5f, 1.5f, 1.5f), wallMat);
        foreach (var sx in new[] { -0.7f, 0.7f })
            foreach (var sz in new[] { -0.7f, 0.7f })
                Prims.Box(t, new Vector3(sx, 1.05f, sz), new Vector3(0.08f, 1.5f, 0.08f), timberMat);
        Prims.Box(t, new Vector3(0.3f, 0.65f, -0.76f), new Vector3(0.4f, 0.7f, 0.06f), Prims.Mat(Door));
        Prims.Cone(t, new Vector3(0, 1.8f, 0), 1.25f, 1.0f, 4, roofMat, 45f);

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

        Prims.EnableShadows(g);
        return g;
    }

    public static GameObject Mill(Transform parent, Vector3 worldPos, Color roofColor)
    {
        var g = NewBuilding("Mill", parent, worldPos, BuildingType.Mill,
            new Vector3(0, 1.0f, 0), new Vector3(2.0f, 2.0f, 2.0f));
        var t = g.transform;
        var wallMat = Prims.Mat(Wall);
        var woodMat = Prims.Mat(Prims.Hex(0x6e4a28), 0.05f);
        var roofMat = Prims.Mat(roofColor, 0.05f, 0.25f);

        Prims.Box(t, new Vector3(0, 0.1f, 0), new Vector3(2.0f, 0.2f, 2.0f), Prims.Mat(Stone, 0.05f)); // base
        Prims.Box(t, new Vector3(0, 0.9f, 0), new Vector3(1.4f, 1.4f, 1.4f), wallMat);                 // body
        Prims.Cone(t, new Vector3(0, 1.6f, 0), 1.2f, 0.8f, 4, roofMat, 45f);                           // roof
        // windmill sail hub + 4 blades on the front face
        Prims.Cylinder(t, new Vector3(0, 1.2f, -0.75f), 0.1f, 0.3f, woodMat)
             .transform.localRotation = Quaternion.Euler(90f, 0, 0);
        for (int i = 0; i < 4; i++)
        {
            var blade = Prims.Box(t, new Vector3(0, 1.2f, -0.85f), new Vector3(0.18f, 1.1f, 0.04f), woodMat);
            blade.transform.localRotation = Quaternion.Euler(0, 0, i * 45f + 45f);
        }

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
        return g;
    }

    /// <summary>
    /// Generic dispatcher used by the placement system and enemy AI to build any
    /// type by enum. Uses <paramref name="teamColor"/> for the roof so buildings
    /// read as team-owned.
    /// </summary>
    public static GameObject Create(BuildingType type, Transform parent, Vector3 worldPos, Color teamColor) => type switch
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
