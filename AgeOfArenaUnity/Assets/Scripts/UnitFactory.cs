using UnityEngine;

/// <summary>
/// Procedural low-poly unit meshes in the same stylized vein as the building/
/// resource factories. Each unit gets a CapsuleCollider on the root (Prims.Spawn
/// strips primitive colliders, so selection raycasts need one re-added), a
/// <see cref="UnitEntity"/>, and a <see cref="SelectionRing"/> child at its feet.
/// </summary>
public static class UnitFactory
{
    static int _nextId = 1;

    // Animated visual prefabs (KayKit), loaded once from Resources. Heavy FBX live outside
    // Resources/, so only prefabs referenced by this library ship in the WebGL build.
    static UnitVisualLibrary _visualLib;
    static bool _visualLibLoaded;
    static UnitVisualLibrary VisualLib
    {
        get
        {
            if (!_visualLibLoaded)
            {
                _visualLib = Resources.Load<UnitVisualLibrary>("UnitVisualLibrary");
                _visualLibLoaded = true;
            }
            return _visualLib;
        }
    }

    static GameObject VisualFor(UnitType type, int teamId = 0) => VisualLib != null ? VisualLib.VisualFor(type, teamId) : null;

    public static UnitEntity Villager(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Villager", parent, worldPos);
        var visual = VisualFor(UnitType.Villager, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var skin = Prims.Mat(Prims.Hex(0xe0ac69));
            var cloth = Prims.Mat(Prims.Hex(0x8a6b3f));
            var accent = Prims.Mat(teamColor, 0f, 0.3f);
            Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.4f, 0.7f, 0.3f), cloth);
            Prims.Box(t, new Vector3(0, 0.18f, 0.02f), new Vector3(0.42f, 0.25f, 0.34f), accent);
            Prims.Sphere(t, new Vector3(0, 0.95f, 0), 0.18f, skin);
            Prims.Box(t, new Vector3(0, 1.02f, 0), new Vector3(0.34f, 0.16f, 0.34f), cloth);
        }
        var e = Finish(g, UnitType.Villager, teamColor, visual, teamId);
        e.hp = e.maxHp = 25f;
        return e;
    }

    public static UnitEntity Militia(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Militia", parent, worldPos);
        var visual = VisualFor(UnitType.Militia, teamId);   // KayKit Knight/Skeleton, or null → primitives
        if (visual == null)
        {
            var t = g.transform;

            var armor = Prims.Mat(teamColor, 0.2f, 0.4f);
            var metal = Prims.Mat(Prims.Hex(0xc0c0c8), 0.6f, 0.6f);
            var skin = Prims.Mat(Prims.Hex(0xe0ac69));

            Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.46f, 0.8f, 0.34f), armor);     // body
            Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.18f, skin);                            // head
            Prims.Box(t, new Vector3(0, 1.08f, 0), new Vector3(0.36f, 0.2f, 0.36f), metal);    // helmet
            // sword: blade + crossguard held at the side
            Prims.Box(t, new Vector3(0.34f, 0.7f, 0.1f), new Vector3(0.06f, 0.9f, 0.06f), metal);
            Prims.Box(t, new Vector3(0.34f, 0.32f, 0.1f), new Vector3(0.2f, 0.06f, 0.06f), metal);
        }

        var e = Finish(g, UnitType.Militia, teamColor, visual, teamId);
        e.hp = e.maxHp = 40f;
        e.pierceArmor = 1f;
        return e;
    }

    public static UnitEntity Archer(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Archer", parent, worldPos);
        var visual = VisualFor(UnitType.Archer, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var skin = Prims.Mat(Prims.Hex(0xe0ac69));
            var tunic = Prims.Mat(teamColor, 0f, 0.3f);
            var wood = Prims.Mat(Prims.Hex(0x6b4a2a));
            Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.4f, 0.7f, 0.3f), tunic);
            Prims.Sphere(t, new Vector3(0, 0.95f, 0), 0.18f, skin);
            Prims.Box(t, new Vector3(0, 1.04f, 0), new Vector3(0.3f, 0.18f, 0.3f), Prims.Mat(Prims.Hex(0x3a5a2a)));
            Prims.Box(t, new Vector3(-0.32f, 0.7f, 0.05f), new Vector3(0.05f, 1.0f, 0.08f), wood);
            Prims.Box(t, new Vector3(0.12f, 0.7f, -0.18f), new Vector3(0.12f, 0.5f, 0.12f), wood);
        }
        var e = Finish(g, UnitType.Archer, teamColor, visual, teamId);
        e.hp = e.maxHp = 30f;
        e.moveSpeed = 3.2f;
        return e;
    }

    public static UnitEntity Cavalry(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Cavalry", parent, worldPos);
        var t = g.transform;

        var horse = Prims.Mat(Prims.Hex(0x5a4632));
        var metal = Prims.Mat(Prims.Hex(0xc0c0c8), 0.6f, 0.6f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var cloth = Prims.Mat(teamColor, 0.2f, 0.4f);

        // Horse body + neck + head + legs.
        Prims.Box(t, new Vector3(0, 0.85f, 0), new Vector3(0.5f, 0.5f, 1.2f), horse);       // barrel
        Prims.Box(t, new Vector3(0, 1.15f, 0.6f), new Vector3(0.32f, 0.55f, 0.35f), horse); // neck
        Prims.Box(t, new Vector3(0, 1.4f, 0.78f), new Vector3(0.28f, 0.3f, 0.5f), horse);   // head
        foreach (var lx in new[] { -0.18f, 0.18f })
            foreach (var lz in new[] { -0.45f, 0.45f })
                Prims.Box(t, new Vector3(lx, 0.35f, lz), new Vector3(0.12f, 0.7f, 0.12f), horse); // legs
        // Rider on top.
        Prims.Box(t, new Vector3(0, 1.45f, -0.1f), new Vector3(0.36f, 0.6f, 0.28f), cloth); // rider torso
        Prims.Sphere(t, new Vector3(0, 1.9f, -0.1f), 0.16f, skin);                          // rider head
        Prims.Box(t, new Vector3(0, 1.96f, -0.1f), new Vector3(0.32f, 0.16f, 0.32f), metal); // helmet
        // lance
        Prims.Box(t, new Vector3(0.28f, 1.5f, 0.3f), new Vector3(0.05f, 0.05f, 1.4f), metal);

        var e = Finish(g, UnitType.Cavalry, teamColor);
        e.hp = e.maxHp = 75f;
        e.moveSpeed = 5.5f;
        e.meleeArmor = 2f;
        e.pierceArmor = 2f;
        return e;
    }

    public static UnitEntity Trebuchet(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Trebuchet", parent, worldPos);
        var t = g.transform;

        if (KenneyModels.Spawn("Castle/siege-trebuchet", t, Vector3.zero, 1.1f) == null)
        {
            // Primitive fallback
            var wood   = Prims.Mat(Prims.Hex(0x7a5c3a));
            var metal  = Prims.Mat(Prims.Hex(0x888890), 0.4f, 0.6f);
            var rope   = Prims.Mat(Prims.Hex(0xc8a878));
            Prims.Box(t, new Vector3(0, 0.20f, 0),     new Vector3(1.2f, 0.14f, 0.7f), wood);
            Prims.Box(t, new Vector3(-0.45f, 0.65f, 0), new Vector3(0.12f, 0.82f, 0.45f), wood);
            Prims.Box(t, new Vector3( 0.45f, 0.65f, 0), new Vector3(0.12f, 0.82f, 0.45f), wood);
            Prims.Box(t, new Vector3(0, 1.05f, 0),     new Vector3(1.1f, 0.12f, 0.18f), wood);
            Prims.Box(t, new Vector3(0, 1.18f, 0.28f), new Vector3(0.1f, 0.11f, 1.6f), wood);
            Prims.Box(t, new Vector3(0, 1.32f, -0.42f), new Vector3(0.3f, 0.28f, 0.24f), metal);
            Prims.Box(t, new Vector3(0, 1.48f, 0.96f), new Vector3(0.16f, 0.16f, 0.16f), rope);
            foreach (float wx in new[] { -0.62f, 0.62f })
                Prims.Box(t, new Vector3(wx, 0.16f, 0), new Vector3(0.14f, 0.32f, 0.34f), metal);
        }
        // Team-colour pennant above the frame (visible on both Kenney and fallback mesh)
        Prims.Box(t, new Vector3(0, 1.7f, 0), new Vector3(0.06f, 0.5f, 0.28f),
            Prims.Mat(teamColor, 0.1f, 0.4f));

        var e = Finish(g, UnitType.Trebuchet, teamColor);
        e.hp = e.maxHp = 150f;
        e.moveSpeed = 1.8f;
        e.demolishedPrefab = Resources.Load<GameObject>("Kenney/Castle/siege-trebuchet-demolished");
        return e;
    }

    public static UnitEntity Scout(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Scout", parent, worldPos);
        var visual = VisualFor(UnitType.Scout, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
            var cloak = Prims.Mat(teamColor, 0f, 0.25f);
            var leather = Prims.Mat(Prims.Hex(0x6b4a2a));
            Prims.Box(t, new Vector3(0, 0.48f, 0), new Vector3(0.32f, 0.66f, 0.24f), leather);
            Prims.Sphere(t, new Vector3(0, 0.92f, 0), 0.16f, skin);
            Prims.Box(t, new Vector3(0, 1.0f, 0), new Vector3(0.28f, 0.12f, 0.28f), cloak);
            Prims.Box(t, new Vector3(0, 1.12f, -0.02f), new Vector3(0.05f, 0.2f, 0.05f), cloak);
            Prims.Box(t, new Vector3(0, 0.55f, -0.16f), new Vector3(0.34f, 0.62f, 0.06f), cloak);
        }
        var e = Finish(g, UnitType.Scout, teamColor, visual, teamId);
        e.hp = e.maxHp = 40f;
        e.moveSpeed = 6.5f;
        e.pierceArmor = 2f;
        return e;
    }

    public static UnitEntity Medic(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Medic", parent, worldPos);
        var visual = VisualFor(UnitType.Medic, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var skin = Prims.Mat(Prims.Hex(0xe0ac69));
            var robe = Prims.Mat(Prims.Hex(0xe8e4d8), 0f, 0.2f);
            var trim = Prims.Mat(teamColor, 0f, 0.3f);
            var staff = Prims.Mat(Prims.Hex(0x6b4a2a));
            Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.46f, 0.92f, 0.4f), robe);
            Prims.Box(t, new Vector3(0, 0.55f, 0), new Vector3(0.12f, 0.84f, 0.42f), trim);
            Prims.Sphere(t, new Vector3(0, 1.04f, 0), 0.17f, skin);
            Prims.Box(t, new Vector3(0, 1.12f, 0), new Vector3(0.34f, 0.2f, 0.34f), robe);
            Prims.Box(t, new Vector3(0.3f, 0.7f, 0.08f), new Vector3(0.05f, 1.1f, 0.05f), staff);
            Prims.Box(t, new Vector3(0.3f, 1.18f, 0.08f), new Vector3(0.2f, 0.06f, 0.06f), trim);
        }
        var e = Finish(g, UnitType.Medic, teamColor, visual, teamId);
        e.hp = e.maxHp = 35f;
        e.moveSpeed = 3.2f;
        return e;
    }

    public static UnitEntity Spearman(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Spearman", parent, worldPos);
        var visual = VisualFor(UnitType.Spearman, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var cloth  = Prims.Mat(teamColor, 0.1f, 0.35f);
            var metal  = Prims.Mat(Prims.Hex(0xb8b8c0), 0.6f, 0.6f);
            var skin   = Prims.Mat(Prims.Hex(0xe0ac69));
            var wood   = Prims.Mat(Prims.Hex(0x6b4a2a));
            Prims.Box(t, new Vector3(0, 0.5f, 0),     new Vector3(0.42f, 0.78f, 0.32f), cloth);
            Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.17f, skin);
            Prims.Box(t, new Vector3(0, 1.08f, 0),    new Vector3(0.30f, 0.16f, 0.30f), metal);
            Prims.Box(t, new Vector3(0.34f, 1.0f, 0.08f),  new Vector3(0.05f, 1.9f, 0.05f), wood);
            Prims.Box(t, new Vector3(0.34f, 1.95f, 0.08f), new Vector3(0.08f, 0.25f, 0.08f), metal);
        }
        var e = Finish(g, UnitType.Spearman, teamColor, visual, teamId);
        e.hp = e.maxHp = 25f;
        e.moveSpeed = 3.3f;
        e.pierceArmor = 3f;
        return e;
    }

    public static UnitEntity Skirmisher(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Skirmisher", parent, worldPos);
        var visual = VisualFor(UnitType.Skirmisher, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
            var tunic = Prims.Mat(teamColor, 0f, 0.3f);
            var hide  = Prims.Mat(Prims.Hex(0x9a7b4f));
            var wood  = Prims.Mat(Prims.Hex(0x6b4a2a));
            var metal = Prims.Mat(Prims.Hex(0xc0c0c8), 0.6f, 0.6f);
            Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.7f, 0.32f), tunic);
            Prims.Box(t, new Vector3(0, 0.55f, 0.18f), new Vector3(0.3f, 0.42f, 0.08f), hide);
            Prims.Sphere(t, new Vector3(0, 0.95f, 0), 0.18f, skin);
            Prims.Box(t, new Vector3(0, 1.02f, 0), new Vector3(0.3f, 0.16f, 0.3f), hide);
            Prims.Box(t, new Vector3(-0.18f, 0.8f, -0.16f), new Vector3(0.05f, 0.9f, 0.05f), wood);
            Prims.Box(t, new Vector3(0.32f, 0.9f, 0.1f),  new Vector3(0.04f, 1.0f, 0.04f), wood);
            Prims.Box(t, new Vector3(0.32f, 1.42f, 0.1f), new Vector3(0.07f, 0.18f, 0.07f), metal);
        }
        var e = Finish(g, UnitType.Skirmisher, teamColor, visual, teamId);
        e.hp = e.maxHp = 30f;
        e.moveSpeed = 3.2f;
        e.pierceArmor = 3f;
        return e;
    }

    public static UnitEntity Camel(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Camel", parent, worldPos);
        var t = g.transform;

        var camel = Prims.Mat(Prims.Hex(0xc8a86a));
        var metal = Prims.Mat(Prims.Hex(0xc0c0c8), 0.6f, 0.6f);
        var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
        var cloth = Prims.Mat(teamColor, 0.2f, 0.4f);

        // Camel body + hump + neck + head + long legs.
        Prims.Box(t, new Vector3(0, 0.9f, 0),     new Vector3(0.48f, 0.5f, 1.1f), camel);    // barrel
        Prims.Box(t, new Vector3(0, 1.25f, -0.1f),new Vector3(0.42f, 0.35f, 0.5f), camel);   // hump
        Prims.Box(t, new Vector3(0, 1.35f, 0.6f), new Vector3(0.26f, 0.6f, 0.28f), camel);   // neck
        Prims.Box(t, new Vector3(0, 1.7f, 0.72f), new Vector3(0.24f, 0.26f, 0.42f), camel);  // head
        foreach (var lx in new[] { -0.16f, 0.16f })
            foreach (var lz in new[] { -0.4f, 0.4f })
                Prims.Box(t, new Vector3(lx, 0.4f, lz), new Vector3(0.1f, 0.85f, 0.1f), camel); // legs
        // Rider on top.
        Prims.Box(t, new Vector3(0, 1.55f, -0.1f), new Vector3(0.34f, 0.55f, 0.26f), cloth); // rider torso
        Prims.Sphere(t, new Vector3(0, 1.95f, -0.1f), 0.15f, skin);                          // rider head
        Prims.Box(t, new Vector3(0, 2.0f, -0.1f), new Vector3(0.3f, 0.15f, 0.3f), metal);    // helmet
        // spear (anti-cavalry weapon)
        Prims.Box(t, new Vector3(0.26f, 1.6f, 0.3f), new Vector3(0.05f, 0.05f, 1.5f), metal);

        var e = Finish(g, UnitType.Camel, teamColor);
        e.hp = e.maxHp = 80f;
        e.moveSpeed = 5.8f;
        e.meleeArmor = 1f;
        e.pierceArmor = 1f;
        return e;
    }

    public static UnitEntity Ram(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Ram", parent, worldPos);
        var t = g.transform;

        if (KenneyModels.Spawn("Castle/siege-ram", t, Vector3.zero, 1.1f) == null)
        {
            // Primitive fallback
            var wood  = Prims.Mat(Prims.Hex(0x6b4a2a));
            var roof  = Prims.Mat(Prims.Hex(0x4a3520));
            var metal = Prims.Mat(Prims.Hex(0x55585e), 0.5f, 0.6f);
            Prims.Box(t, new Vector3(0, 0.5f, 0),    new Vector3(0.9f, 0.6f, 1.7f), wood);
            Prims.Box(t, new Vector3(0, 0.95f, 0),   new Vector3(1.0f, 0.3f, 1.9f), roof);
            Prims.Cylinder(t, new Vector3(0, 0.55f, 1.05f), 0.16f, 1.2f, wood);
            Prims.Box(t, new Vector3(0, 0.55f, 1.6f), new Vector3(0.34f, 0.34f, 0.3f), metal);
            foreach (var lx in new[] { -0.45f, 0.45f })
                foreach (var lz in new[] { -0.6f, 0.6f })
                    Prims.Cylinder(t, new Vector3(lx, 0.2f, lz), 0.22f, 0.12f, metal);
        }
        // Team-colour banner on the roof ridge
        Prims.Box(t, new Vector3(0, 1.25f, 0), new Vector3(0.5f, 0.12f, 1.1f),
            Prims.Mat(teamColor, 0.1f, 0.3f));

        var e = Finish(g, UnitType.Ram, teamColor);
        e.hp = e.maxHp = 200f;
        e.moveSpeed = 2.2f;
        e.meleeArmor = 3f;
        e.pierceArmor = 180f;
        e.demolishedPrefab = Resources.Load<GameObject>("Kenney/Castle/siege-ram-demolished");
        return e;
    }

    public static UnitEntity Mangonel(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Mangonel", parent, worldPos);
        var t = g.transform;

        if (KenneyModels.Spawn("Castle/siege-catapult", t, Vector3.zero, 1.1f) == null)
        {
            // Primitive fallback
            var wood  = Prims.Mat(Prims.Hex(0x7a5c3a));
            var metal = Prims.Mat(Prims.Hex(0x888890), 0.4f, 0.6f);
            var stone = Prims.Mat(Prims.Hex(0x8a8a8a), 0.05f);
            Prims.Box(t, new Vector3(0, 0.3f, 0), new Vector3(1.0f, 0.25f, 1.4f), wood);
            foreach (var lz in new[] { -0.5f, 0.5f })
                foreach (var lx in new[] { -0.5f, 0.5f })
                    Prims.Cylinder(t, new Vector3(lx, 0.2f, lz), 0.22f, 0.12f, metal);
            Prims.Box(t, new Vector3(0, 0.75f, -0.1f), new Vector3(0.12f, 0.12f, 1.1f), wood)
                .transform.localRotation = Quaternion.Euler(35f, 0, 0);
            Prims.Box(t, new Vector3(0, 1.25f, 0.45f), new Vector3(0.3f, 0.18f, 0.3f), wood);
            Prims.Sphere(t, new Vector3(0, 1.42f, 0.45f), 0.18f, stone);
        }
        // Team-colour banner on the side frame
        Prims.Box(t, new Vector3(0, 0.6f, 0.35f), new Vector3(0.6f, 0.1f, 0.1f),
            Prims.Mat(teamColor, 0.1f, 0.3f));

        var e = Finish(g, UnitType.Mangonel, teamColor);
        e.hp = e.maxHp = 50f;
        e.moveSpeed = 2.4f;
        e.pierceArmor = 4f;
        e.demolishedPrefab = Resources.Load<GameObject>("Kenney/Castle/siege-catapult-demolished");
        return e;
    }

    public static UnitEntity CavalryArcher(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("CavalryArcher", parent, worldPos);
        var t = g.transform;

        var horse = Prims.Mat(Prims.Hex(0x5a4632));
        var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
        var cloth = Prims.Mat(teamColor, 0.1f, 0.3f);
        var wood  = Prims.Mat(Prims.Hex(0x6b4a2a));

        // Horse (same proportions as Cavalry).
        Prims.Box(t, new Vector3(0, 0.85f, 0), new Vector3(0.5f, 0.5f, 1.2f), horse);       // barrel
        Prims.Box(t, new Vector3(0, 1.15f, 0.6f), new Vector3(0.32f, 0.55f, 0.35f), horse); // neck
        Prims.Box(t, new Vector3(0, 1.4f, 0.78f), new Vector3(0.28f, 0.3f, 0.5f), horse);   // head
        foreach (var lx in new[] { -0.18f, 0.18f })
            foreach (var lz in new[] { -0.45f, 0.45f })
                Prims.Box(t, new Vector3(lx, 0.35f, lz), new Vector3(0.12f, 0.7f, 0.12f), horse); // legs
        // Mounted archer rider.
        Prims.Box(t, new Vector3(0, 1.45f, -0.1f), new Vector3(0.34f, 0.6f, 0.26f), cloth); // torso
        Prims.Sphere(t, new Vector3(0, 1.9f, -0.1f), 0.16f, skin);                          // head
        Prims.Box(t, new Vector3(0, 1.96f, -0.1f), new Vector3(0.3f, 0.16f, 0.3f), Prims.Mat(Prims.Hex(0x3a5a2a))); // hood
        // Bow held across the body + quiver.
        Prims.Box(t, new Vector3(-0.34f, 1.5f, 0.15f), new Vector3(0.05f, 0.9f, 0.08f), wood);
        Prims.Box(t, new Vector3(0.16f, 1.55f, -0.22f), new Vector3(0.1f, 0.4f, 0.1f), wood);

        var e = Finish(g, UnitType.CavalryArcher, teamColor);
        e.hp = e.maxHp = 50f;
        e.moveSpeed = 5.2f;     // fast like cavalry
        e.pierceArmor = 1f;
        return e;
    }

    // ── M9/CIVU: civilization unique units (Castle) ───────────────────────────

    /// <summary>Teutons unique: slow, heavily armored melee infantry.</summary>
    public static UnitEntity TeutonicKnight(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("TeutonicKnight", parent, worldPos);
        var t = g.transform;
        var plate = Prims.Mat(Prims.Hex(0x9a9aa2), 0.6f, 0.5f);
        var accent = Prims.Mat(teamColor, 0.2f, 0.4f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        Prims.Box(t, new Vector3(0, 0.55f, 0), new Vector3(0.6f, 0.95f, 0.45f), plate);   // bulky body
        Prims.Box(t, new Vector3(0, 0.3f, 0.02f), new Vector3(0.62f, 0.3f, 0.46f), accent);
        Prims.Sphere(t, new Vector3(0, 1.16f, 0), 0.2f, skin);                             // head
        Prims.Box(t, new Vector3(0, 1.24f, 0), new Vector3(0.4f, 0.24f, 0.4f), plate);     // great helm
        Prims.Box(t, new Vector3(0.4f, 0.7f, 0.1f), new Vector3(0.08f, 1.0f, 0.08f), plate); // sword
        var e = Finish(g, UnitType.TeutonicKnight, teamColor);
        e.hp = e.maxHp = 100f;
        e.moveSpeed = 2.5f;      // very slow
        e.meleeArmor = 5f;
        e.pierceArmor = 2f;
        return e;
    }

    /// <summary>Persians unique: massive war elephant, huge HP, bonus vs buildings.</summary>
    public static UnitEntity WarElephant(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("WarElephant", parent, worldPos);
        var t = g.transform;
        var hide = Prims.Mat(Prims.Hex(0x8a8a92));
        var cloth = Prims.Mat(teamColor, 0.1f, 0.3f);
        var tusk = Prims.Mat(Prims.Hex(0xeae0c8));
        Prims.Box(t, new Vector3(0, 1.1f, 0), new Vector3(1.1f, 1.0f, 1.9f), hide);        // body
        Prims.Box(t, new Vector3(0, 1.3f, 1.0f), new Vector3(0.7f, 0.7f, 0.5f), hide);     // head
        Prims.Box(t, new Vector3(0, 1.0f, 1.5f), new Vector3(0.2f, 0.2f, 0.7f), hide);     // trunk
        Prims.Box(t, new Vector3(-0.25f, 0.95f, 1.45f), new Vector3(0.08f, 0.08f, 0.5f), tusk);
        Prims.Box(t, new Vector3(0.25f, 0.95f, 1.45f), new Vector3(0.08f, 0.08f, 0.5f), tusk);
        foreach (var lx in new[] { -0.4f, 0.4f })
            foreach (var lz in new[] { -0.6f, 0.6f })
                Prims.Box(t, new Vector3(lx, 0.35f, lz), new Vector3(0.22f, 0.7f, 0.22f), hide);
        Prims.Box(t, new Vector3(0, 1.85f, -0.1f), new Vector3(0.9f, 0.5f, 1.0f), cloth);  // howdah
        var e = Finish(g, UnitType.WarElephant, teamColor);
        e.hp = e.maxHp = 250f;
        e.moveSpeed = 2.2f;
        e.meleeArmor = 3f;
        e.pierceArmor = 3f;
        return e;
    }

    /// <summary>Mongols unique: fast mounted archer, bonus vs siege.</summary>
    public static UnitEntity Mangudai(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Mangudai", parent, worldPos);
        var t = g.transform;
        var horse = Prims.Mat(Prims.Hex(0x4a3a28));
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var cloth = Prims.Mat(teamColor, 0.1f, 0.3f);
        var wood = Prims.Mat(Prims.Hex(0x6b4a2a));
        Prims.Box(t, new Vector3(0, 0.85f, 0), new Vector3(0.5f, 0.5f, 1.2f), horse);
        Prims.Box(t, new Vector3(0, 1.15f, 0.6f), new Vector3(0.32f, 0.55f, 0.35f), horse);
        foreach (var lx in new[] { -0.18f, 0.18f })
            foreach (var lz in new[] { -0.45f, 0.45f })
                Prims.Box(t, new Vector3(lx, 0.4f, lz), new Vector3(0.1f, 0.85f, 0.1f), horse);
        Prims.Box(t, new Vector3(0, 1.5f, -0.1f), new Vector3(0.32f, 0.5f, 0.26f), cloth); // rider
        Prims.Sphere(t, new Vector3(0, 1.85f, -0.1f), 0.15f, skin);
        Prims.Box(t, new Vector3(-0.3f, 1.55f, 0.0f), new Vector3(0.05f, 0.6f, 0.1f), wood); // bow
        var e = Finish(g, UnitType.Mangudai, teamColor);
        e.hp = e.maxHp = 60f;
        e.moveSpeed = 5.5f;
        e.pierceArmor = 1f;
        return e;
    }

    /// <summary>Japanese unique: fast-attacking infantry swordsman.</summary>
    public static UnitEntity Samurai(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Samurai", parent, worldPos);
        var t = g.transform;
        var armor = Prims.Mat(teamColor, 0.2f, 0.4f);
        var metal = Prims.Mat(Prims.Hex(0xc0c0c8), 0.6f, 0.6f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.44f, 0.8f, 0.32f), armor);
        Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.17f, skin);
        Prims.Box(t, new Vector3(0, 1.12f, 0), new Vector3(0.34f, 0.22f, 0.34f), metal);   // kabuto helmet
        Prims.Box(t, new Vector3(0.32f, 0.8f, 0.12f), new Vector3(0.05f, 0.95f, 0.05f), metal); // katana
        var e = Finish(g, UnitType.Samurai, teamColor);
        e.hp = e.maxHp = 80f;
        e.moveSpeed = 4.0f;
        e.meleeArmor = 2f;
        e.pierceArmor = 2f;
        return e;
    }

    /// <summary>Aztecs unique (EAGLE): fast, lightly-armored scout-warrior.</summary>
    public static UnitEntity Eagle(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Eagle", parent, worldPos);
        var t = g.transform;
        var skin = Prims.Mat(Prims.Hex(0xc8884a));
        var feather = Prims.Mat(teamColor, 0.1f, 0.3f);
        var beak = Prims.Mat(Prims.Hex(0xeac24a));
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.38f, 0.75f, 0.28f), skin);
        Prims.Sphere(t, new Vector3(0, 1.0f, 0), 0.17f, skin);
        Prims.Box(t, new Vector3(0, 1.16f, 0.02f), new Vector3(0.36f, 0.22f, 0.36f), feather); // eagle headdress
        Prims.Box(t, new Vector3(0, 1.12f, 0.18f), new Vector3(0.08f, 0.08f, 0.18f), beak);    // beak
        Prims.Box(t, new Vector3(0.3f, 0.7f, 0.06f), new Vector3(0.05f, 0.8f, 0.05f), Prims.Mat(Prims.Hex(0x6b4a2a))); // club
        var e = Finish(g, UnitType.Eagle, teamColor);
        e.hp = e.maxHp = 55f;
        e.moveSpeed = 4.5f;      // fast
        e.pierceArmor = 2f;
        return e;
    }

    // ── N4/CIVU: second wave of unique units ─────────────────────────────────

    /// <summary>Franks unique: ranged infantry that hurls axes (short range, melee damage).</summary>
    public static UnitEntity ThrowingAxeman(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("ThrowingAxeman", parent, worldPos);
        var t = g.transform;
        var tunic = Prims.Mat(teamColor, 0.1f, 0.3f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var steel = Prims.Mat(Prims.Hex(0xb8b8c0), 0.6f, 0.6f);
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.42f, 0.8f, 0.3f), tunic);
        Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.17f, skin);
        Prims.Box(t, new Vector3(0, 1.12f, 0), new Vector3(0.32f, 0.18f, 0.32f), steel);   // helmet
        Prims.Box(t, new Vector3(0.3f, 0.85f, 0.12f), new Vector3(0.18f, 0.14f, 0.05f), steel); // axe head
        var e = Finish(g, UnitType.ThrowingAxeman, teamColor);
        e.hp = e.maxHp = 60f;
        e.moveSpeed = 4.0f;
        e.meleeArmor = 1f;
        e.pierceArmor = 1f;
        return e;
    }

    /// <summary>Byzantines unique: heavily-armoured cataphract cavalry, bonus vs infantry.</summary>
    public static UnitEntity Cataphract(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Cataphract", parent, worldPos);
        var t = g.transform;
        var horse = Prims.Mat(Prims.Hex(0x5a5a64), 0.4f, 0.4f);
        var barding = Prims.Mat(teamColor, 0.2f, 0.4f);
        var steel = Prims.Mat(Prims.Hex(0xc0c0c8), 0.7f, 0.6f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        Prims.Box(t, new Vector3(0, 0.9f, 0), new Vector3(0.55f, 0.55f, 1.25f), horse);    // armoured horse
        Prims.Box(t, new Vector3(0, 0.62f, 0.1f), new Vector3(0.6f, 0.25f, 1.1f), barding); // caparison
        Prims.Box(t, new Vector3(0, 1.2f, 0.62f), new Vector3(0.34f, 0.5f, 0.36f), horse);
        foreach (var lx in new[] { -0.2f, 0.2f })
            foreach (var lz in new[] { -0.48f, 0.48f })
                Prims.Box(t, new Vector3(lx, 0.38f, lz), new Vector3(0.12f, 0.78f, 0.12f), horse);
        Prims.Box(t, new Vector3(0, 1.5f, -0.05f), new Vector3(0.36f, 0.55f, 0.3f), steel);  // mailed rider
        Prims.Sphere(t, new Vector3(0, 1.88f, -0.05f), 0.16f, skin);
        Prims.Box(t, new Vector3(0, 1.96f, -0.05f), new Vector3(0.3f, 0.18f, 0.3f), steel);  // helmet
        var e = Finish(g, UnitType.Cataphract, teamColor);
        e.hp = e.maxHp = 110f;
        e.moveSpeed = 4.0f;
        e.meleeArmor = 2f;
        e.pierceArmor = 1f;
        return e;
    }

    /// <summary>Vikings unique: ferocious infantry that regenerates its own HP.</summary>
    public static UnitEntity Berserk(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Berserk", parent, worldPos);
        var t = g.transform;
        var fur = Prims.Mat(Prims.Hex(0x6b4a2a));
        var cloth = Prims.Mat(teamColor, 0.1f, 0.3f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var steel = Prims.Mat(Prims.Hex(0xb8b8c0), 0.6f, 0.6f);
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.46f, 0.82f, 0.32f), cloth);
        Prims.Box(t, new Vector3(0, 0.78f, 0), new Vector3(0.52f, 0.28f, 0.4f), fur);       // fur cloak
        Prims.Sphere(t, new Vector3(0, 1.04f, 0), 0.18f, skin);
        Prims.Box(t, new Vector3(0, 1.18f, 0), new Vector3(0.18f, 0.22f, 0.18f), fur);      // wild hair
        Prims.Box(t, new Vector3(0.32f, 0.85f, 0.1f), new Vector3(0.06f, 0.9f, 0.12f), steel); // great axe
        var e = Finish(g, UnitType.Berserk, teamColor);
        e.hp = e.maxHp = 65f;
        e.moveSpeed = 4.2f;
        e.meleeArmor = 1f;
        e.pierceArmor = 1f;
        return e;
    }

    /// <summary>Saracens unique: camel rider that throws scimitars (ranged, anti-cavalry).</summary>
    public static UnitEntity Mameluke(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Mameluke", parent, worldPos);
        var t = g.transform;
        var camel = Prims.Mat(Prims.Hex(0xc8a86a));
        var robe = Prims.Mat(teamColor, 0.1f, 0.3f);
        var skin = Prims.Mat(Prims.Hex(0xd0a060));
        var steel = Prims.Mat(Prims.Hex(0xc0c0c8), 0.7f, 0.6f);
        Prims.Box(t, new Vector3(0, 0.95f, 0), new Vector3(0.5f, 0.55f, 1.15f), camel);     // camel body
        Prims.Box(t, new Vector3(0, 1.2f, -0.1f), new Vector3(0.42f, 0.4f, 0.5f), camel);   // hump
        Prims.Box(t, new Vector3(0, 1.35f, 0.6f), new Vector3(0.28f, 0.5f, 0.32f), camel);  // neck/head
        foreach (var lx in new[] { -0.18f, 0.18f })
            foreach (var lz in new[] { -0.42f, 0.42f })
                Prims.Box(t, new Vector3(lx, 0.42f, lz), new Vector3(0.11f, 0.85f, 0.11f), camel);
        Prims.Box(t, new Vector3(0, 1.6f, -0.05f), new Vector3(0.32f, 0.5f, 0.28f), robe);  // rider
        Prims.Sphere(t, new Vector3(0, 1.95f, -0.05f), 0.15f, skin);
        Prims.Box(t, new Vector3(0.3f, 1.7f, 0.1f), new Vector3(0.05f, 0.06f, 0.4f), steel); // scimitar
        var e = Finish(g, UnitType.Mameluke, teamColor);
        e.hp = e.maxHp = 65f;
        e.moveSpeed = 4.5f;
        e.meleeArmor = 1f;
        e.pierceArmor = 1f;
        return e;
    }

    // ── N4/CIVC13: AoK-13 unique units ───────────────────────────────────────

    /// <summary>Celts unique: very fast infantry raider with a heavy blade.</summary>
    public static UnitEntity WoadRaider(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("WoadRaider", parent, worldPos);
        var t = g.transform;
        var skin = Prims.Mat(Prims.Hex(0x6fa3c8));   // woad-blue painted skin
        var cloth = Prims.Mat(teamColor, 0.1f, 0.3f);
        var steel = Prims.Mat(Prims.Hex(0xb8b8c0), 0.6f, 0.6f);
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.42f, 0.8f, 0.3f), cloth);
        Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.17f, skin);
        Prims.Box(t, new Vector3(0.3f, 0.85f, 0.1f), new Vector3(0.06f, 0.85f, 0.12f), steel); // sword
        var e = Finish(g, UnitType.WoadRaider, teamColor);
        e.hp = e.maxHp = 65f;
        e.moveSpeed = 4.8f;      // very fast
        e.meleeArmor = 0f;
        e.pierceArmor = 1f;
        return e;
    }

    /// <summary>Chinese unique: rapid-fire repeating crossbow archer.</summary>
    public static UnitEntity ChuKoNu(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("ChuKoNu", parent, worldPos);
        var t = g.transform;
        var robe = Prims.Mat(teamColor, 0.1f, 0.3f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var wood = Prims.Mat(Prims.Hex(0x6b4a2a));
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.4f, 0.8f, 0.3f), robe);
        Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.16f, skin);
        Prims.Box(t, new Vector3(0, 1.14f, 0), new Vector3(0.34f, 0.14f, 0.34f), wood);   // conical hat
        Prims.Box(t, new Vector3(0.26f, 0.7f, 0.12f), new Vector3(0.06f, 0.4f, 0.1f), wood); // repeating crossbow
        var e = Finish(g, UnitType.ChuKoNu, teamColor);
        e.hp = e.maxHp = 45f;
        e.moveSpeed = 3.6f;
        e.pierceArmor = 0f;
        return e;
    }

    /// <summary>Goths unique: heavily-armoured Huskarl, high pierce armor (anti-archer).</summary>
    public static UnitEntity Huskarl(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Huskarl", parent, worldPos);
        var t = g.transform;
        var leather = Prims.Mat(Prims.Hex(0x7a5230));
        var cloth = Prims.Mat(teamColor, 0.1f, 0.3f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var steel = Prims.Mat(Prims.Hex(0xb8b8c0), 0.6f, 0.6f);
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.46f, 0.82f, 0.34f), leather);
        Prims.Box(t, new Vector3(0, 0.5f, 0.18f), new Vector3(0.5f, 0.5f, 0.06f), cloth);  // round shield
        Prims.Sphere(t, new Vector3(0, 1.04f, 0), 0.18f, skin);
        Prims.Box(t, new Vector3(0, 1.16f, 0), new Vector3(0.34f, 0.2f, 0.34f), leather);  // helm
        Prims.Box(t, new Vector3(0.32f, 0.85f, 0.1f), new Vector3(0.05f, 0.8f, 0.05f), steel); // axe
        var e = Finish(g, UnitType.Huskarl, teamColor);
        e.hp = e.maxHp = 70f;
        e.moveSpeed = 4.2f;
        e.meleeArmor = 0f;
        e.pierceArmor = 6f;      // signature: shrugs off arrows
        return e;
    }

    /// <summary>Turks unique: gunpowder Janissary, slow but very high damage.</summary>
    public static UnitEntity Janissary(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Janissary", parent, worldPos);
        var t = g.transform;
        var robe = Prims.Mat(teamColor, 0.1f, 0.3f);
        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var iron = Prims.Mat(Prims.Hex(0x3a3a40), 0.7f, 0.5f);
        var white = Prims.Mat(Prims.Hex(0xe8e4dc));
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.42f, 0.8f, 0.3f), robe);
        Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.16f, skin);
        Prims.Box(t, new Vector3(0, 1.18f, 0), new Vector3(0.22f, 0.3f, 0.22f), white);   // tall cap
        Prims.Box(t, new Vector3(0.24f, 0.78f, 0.18f), new Vector3(0.05f, 0.05f, 0.55f), iron); // hand cannon
        var e = Finish(g, UnitType.Janissary, teamColor);
        e.hp = e.maxHp = 55f;
        e.moveSpeed = 3.6f;
        e.pierceArmor = 1f;
        return e;
    }

    // ── M10/VREGI: Regicide King ─────────────────────────────────────────────

    /// <summary>Regicide mode: each team has one King. Its death eliminates the team.</summary>
    public static UnitEntity King(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("King", parent, worldPos);
        var visual = VisualFor(UnitType.Militia, teamId); // regal warrior silhouette
        if (visual == null)
        {
            var t = g.transform;
            var crown = Prims.Mat(Prims.Hex(0xf5c842), 0.5f, 0.8f); // gold
            var robe  = Prims.Mat(teamColor, 0.1f, 0.3f);
            var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
            Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.46f, 0.85f, 0.38f), robe);
            Prims.Sphere(t, new Vector3(0, 1.04f, 0), 0.19f, skin);
            Prims.Box(t, new Vector3(0, 1.2f, 0), new Vector3(0.38f, 0.2f, 0.38f), crown);
        }
        var e = Finish(g, UnitType.King, teamColor, visual, teamId);
        e.hp = e.maxHp = 75f;
        e.moveSpeed = 3.2f;
        e.meleeArmor = 1f;
        e.pierceArmor = 1f;
        return e;
    }

    public static UnitEntity Monk(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Monk", parent, worldPos);
        var visual = VisualFor(UnitType.Monk, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var robe  = Prims.Mat(Prims.Hex(0x8b6914), 0f, 0.15f);
            var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
            var staff = Prims.Mat(Prims.Hex(0x6b4a2a));
            Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.44f, 0.85f, 0.36f), robe);
            Prims.Sphere(t, new Vector3(0, 1.04f, 0), 0.17f, skin);
            Prims.Box(t, new Vector3(0, 1.12f, 0), new Vector3(0.36f, 0.22f, 0.36f), robe);
            Prims.Box(t, new Vector3(0.32f, 0.8f, 0.06f), new Vector3(0.05f, 1.2f, 0.05f), staff);
            Prims.Cone(t, new Vector3(0.32f, 1.42f, 0.06f), 0.12f, 0.25f, 6, Prims.Mat(Prims.Hex(0xf2c14e), 0.2f, 0.6f), 0f);
        }
        var e = Finish(g, UnitType.Monk, teamColor, visual, teamId);
        e.hp = e.maxHp = 30f;
        e.moveSpeed = 2.8f;
        return e;
    }

    /// <summary>Britons unique unit: longer range than Archer, higher HP, slower move.</summary>
    public static UnitEntity Longbowman(Transform parent, Vector3 worldPos, Color teamColor, int teamId = 0)
    {
        var g = NewUnit("Longbowman", parent, worldPos);
        var visual = VisualFor(UnitType.Longbowman, teamId);
        if (visual == null)
        {
            var t = g.transform;
            var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
            var tunic = Prims.Mat(teamColor, 0f, 0.3f);
            var wood  = Prims.Mat(Prims.Hex(0x5a3a18));
            Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.72f, 0.32f), tunic);
            Prims.Sphere(t, new Vector3(0, 0.97f, 0), 0.19f, skin);
            Prims.Box(t, new Vector3(0, 1.06f, 0), new Vector3(0.28f, 0.14f, 0.28f), Prims.Mat(Prims.Hex(0x2a4822)));
            Prims.Box(t, new Vector3(-0.34f, 0.7f, 0.05f), new Vector3(0.04f, 1.3f, 0.07f), wood);
            Prims.Box(t, new Vector3(0.14f, 0.72f, -0.20f), new Vector3(0.14f, 0.55f, 0.14f), wood);
        }
        var e = Finish(g, UnitType.Longbowman, teamColor, visual, teamId);
        e.hp = e.maxHp = 35f;
        e.moveSpeed = 3.0f;
        return e;
    }

    public static UnitEntity Galley(Transform parent, Vector3 worldPos, Color teamColor, int navalAgentTypeId)
    {
        var g = new GameObject("Galley");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var wood  = Prims.Mat(Prims.Hex(0x7a5c3a));
        var sail  = Prims.Mat(teamColor, 0f, 0.3f);
        var dark  = Prims.Mat(Prims.Hex(0x4a3a22), 0.05f);

        // Boat hull
        Prims.Box(t, new Vector3(0, 0.2f, 0),    new Vector3(1.6f, 0.5f, 3.5f), wood);
        // Raised bow and stern
        Prims.Box(t, new Vector3(0, 0.45f, 1.6f), new Vector3(1.3f, 0.4f, 0.4f), dark);
        Prims.Box(t, new Vector3(0, 0.55f,-1.6f), new Vector3(1.0f, 0.3f, 0.4f), dark);
        // Mast
        Prims.Cylinder(t, new Vector3(0, 1.4f, 0.3f), 0.08f, 2.0f, dark);
        // Sail
        Prims.Box(t, new Vector3(0.4f, 1.7f, 0.3f), new Vector3(0.05f, 0.9f, 1.2f), sail);
        // Oars
        foreach (float rx in new[] { -0.85f, 0.85f })
            Prims.Box(t, new Vector3(rx, 0.15f, 0), new Vector3(0.06f, 0.06f, 2.8f), dark);

        g.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);
        Prims.EnableShadows(g);
        Prims.BlobShadow(t, 0.9f);

        var ringGo = new GameObject("SelectionRing");
        ringGo.transform.SetParent(g.transform, false);
        ringGo.AddComponent<UnityEngine.LineRenderer>();
        ringGo.AddComponent<SelectionRing>();

        var col = g.AddComponent<UnityEngine.CapsuleCollider>();
        col.center = new Vector3(0, 0.6f, 0);
        col.radius = 0.4f;
        col.height = 1.2f;

        var e = g.AddComponent<UnitEntity>();
        e.unitId  = _nextId++;
        e.teamId  = 0;
        e.type    = UnitType.Galley;
        e.state   = UnitState.Idle;
        e.targetPos = g.transform.position;
        e.isNaval = true;
        e.navalAgentTypeId = navalAgentTypeId;
        e.hp = e.maxHp = 120f;
        e.moveSpeed = 4.5f;
        e.pierceArmor = 1f;
        return e;
    }

    /// <summary>Shared naval entity finalization (collider + ring + naval UnitEntity).
    /// Mirrors Galley's setup so Fire/Demo ships behave identically on the water mesh.</summary>
    static UnitEntity FinishBoat(GameObject g, UnitType type, int navalAgentTypeId)
    {
        g.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);
        Prims.EnableShadows(g);
        Prims.BlobShadow(g.transform, 0.9f);

        var ringGo = new GameObject("SelectionRing");
        ringGo.transform.SetParent(g.transform, false);
        ringGo.AddComponent<UnityEngine.LineRenderer>();
        ringGo.AddComponent<SelectionRing>();

        var col = g.AddComponent<UnityEngine.CapsuleCollider>();
        col.center = new Vector3(0, 0.6f, 0);
        col.radius = 0.4f;
        col.height = 1.2f;

        var e = g.AddComponent<UnitEntity>();
        e.unitId = _nextId++;
        e.teamId = 0;
        e.type = type;
        e.state = UnitState.Idle;
        e.targetPos = g.transform.position;
        e.isNaval = true;
        e.navalAgentTypeId = navalAgentTypeId;
        return e;
    }

    public static UnitEntity FireShip(Transform parent, Vector3 worldPos, Color teamColor, int navalAgentTypeId)
    {
        var g = new GameObject("FireShip");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var wood  = Prims.Mat(Prims.Hex(0x5a3a20));
        var dark  = Prims.Mat(Prims.Hex(0x3a2a18), 0.05f);
        var flame = Prims.Mat(Prims.Hex(0xf06010), 0.3f, 0.6f);
        var sail  = Prims.Mat(teamColor, 0f, 0.3f);

        Prims.Box(t, new Vector3(0, 0.2f, 0),     new Vector3(1.4f, 0.45f, 2.8f), wood);  // hull
        Prims.Box(t, new Vector3(0, 0.45f, 1.3f), new Vector3(1.1f, 0.4f, 0.4f), dark);   // bow
        Prims.Cylinder(t, new Vector3(0, 1.2f, 0.2f), 0.07f, 1.6f, dark);                 // mast
        Prims.Box(t, new Vector3(0.35f, 1.45f, 0.2f), new Vector3(0.05f, 0.7f, 1.0f), sail);
        Prims.Cone(t, new Vector3(0, 0.9f, 1.25f), 0.35f, 0.8f, 6, flame, 0f);            // fire pot at the bow

        var e = FinishBoat(g, UnitType.FireShip, navalAgentTypeId);
        e.hp = e.maxHp = 100f;
        e.moveSpeed = 5.5f;     // fast — closes on enemy ships
        e.pierceArmor = 2f;
        return e;
    }

    public static UnitEntity DemoShip(Transform parent, Vector3 worldPos, Color teamColor, int navalAgentTypeId)
    {
        var g = new GameObject("DemoShip");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var wood   = Prims.Mat(Prims.Hex(0x4a3520));
        var dark   = Prims.Mat(Prims.Hex(0x2a1e12), 0.05f);
        var barrel = Prims.Mat(Prims.Hex(0x8a3018), 0.1f);

        Prims.Box(t, new Vector3(0, 0.2f, 0),     new Vector3(1.3f, 0.45f, 2.4f), wood); // small hull
        Prims.Box(t, new Vector3(0, 0.45f, 1.1f), new Vector3(1.0f, 0.4f, 0.4f), dark);  // bow
        // Stacked explosive barrels.
        Prims.Cylinder(t, new Vector3(-0.25f, 0.65f, 0), 0.22f, 0.5f, barrel);
        Prims.Cylinder(t, new Vector3(0.25f, 0.65f, 0),  0.22f, 0.5f, barrel);
        Prims.Cylinder(t, new Vector3(0, 0.9f, -0.3f),   0.22f, 0.5f, barrel);

        var e = FinishBoat(g, UnitType.DemoShip, navalAgentTypeId);
        e.hp = e.maxHp = 50f;
        e.moveSpeed = 4.5f;
        e.pierceArmor = 1f;
        return e;
    }

    /// <summary>M14/FISH: civilian fishing boat — gathers food from Fish ponds/Traps,
    /// deposits at the Dock. Naval (water NavMesh), unarmed.</summary>
    public static UnitEntity FishingShip(Transform parent, Vector3 worldPos, Color teamColor, int navalAgentTypeId)
    {
        var g = new GameObject("FishingShip");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var wood = Prims.Mat(Prims.Hex(0x6b4a2a));
        var deck = Prims.Mat(Prims.Hex(0x8a6b3f));
        var sail = Prims.Mat(teamColor, 0f, 0.3f);
        var net  = Prims.Mat(Prims.Hex(0xc8c0a0), 0f, 0.2f);

        Prims.Box(t, new Vector3(0, 0.2f, 0),     new Vector3(1.1f, 0.4f, 2.2f), wood);  // small hull
        Prims.Box(t, new Vector3(0, 0.42f, 0.9f), new Vector3(0.85f, 0.32f, 0.5f), deck); // bow deck
        Prims.Cylinder(t, new Vector3(0, 1.0f, -0.2f), 0.06f, 1.3f, wood);                // short mast
        Prims.Box(t, new Vector3(0.25f, 1.2f, -0.2f), new Vector3(0.04f, 0.55f, 0.8f), sail);
        Prims.Box(t, new Vector3(0, 0.5f, -0.9f), new Vector3(0.7f, 0.5f, 0.1f), net);    // hanging net at the stern

        var e = FinishBoat(g, UnitType.FishingShip, navalAgentTypeId);
        e.hp = e.maxHp = 60f;
        e.moveSpeed = 3.6f;     // steady worker pace
        e.pierceArmor = 0f;
        return e;
    }

    public static UnitEntity TradeCart(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = new GameObject("TradeCart");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var woodMat = Prims.Mat(Prims.Hex(0x7a5030), 0f, 0.2f);
        var coverMat = Prims.Mat(teamColor, 0f, 0.3f);
        var wheelMat = Prims.Mat(Prims.Hex(0x3a2010), 0.05f, 0.2f);

        Prims.Box(t, new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.3f, 0.5f), woodMat);   // cart body
        Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.65f, 0.2f, 0.45f), coverMat); // cover
        foreach (float cx in new[] { -0.35f, 0.35f })
        foreach (float cz in new[] { -0.28f, 0.28f })
            Prims.Cylinder(t, new Vector3(cx, 0.2f, cz), 0.18f, 0.05f, wheelMat);

        var e = Finish(g, UnitType.TradeCart, teamColor);
        e.hp = e.maxHp = 25f;
        e.moveSpeed = 4.5f;
        return e;
    }

    /// <summary>
    /// Central type→factory dispatch. The single source of truth for "spawn a unit
    /// of this type." Always assigns <c>teamId</c> on the result — fixing two bugs:
    /// (1) factory methods that take no teamId param (Cavalry, Trebuchet, Camel,
    /// uniques, TradeCart…) used to leave teamId=0, so AI-trained Cavalry silently
    /// joined the player's team; (2) save-restore previously only knew ~10 types and
    /// turned everything else (Trebuchet, Monk, uniques, the King!) into a Villager.
    /// Ship types need <paramref name="navalAgentTypeId"/>; everything else ignores it.
    /// </summary>
    public static UnitEntity Spawn(UnitType type, Transform parent, Vector3 pos, int teamId, int navalAgentTypeId = -1)
    {
        Color c = TeamPalette.For(teamId);
        UnitEntity e = type switch
        {
            UnitType.Villager       => Villager(parent, pos, c, teamId),
            UnitType.Militia        => Militia(parent, pos, c, teamId),
            UnitType.Archer         => Archer(parent, pos, c, teamId),
            UnitType.Cavalry        => Cavalry(parent, pos, c),
            UnitType.Trebuchet      => Trebuchet(parent, pos, c),
            UnitType.Scout          => Scout(parent, pos, c, teamId),
            UnitType.Medic          => Medic(parent, pos, c, teamId),
            UnitType.Spearman       => Spearman(parent, pos, c, teamId),
            UnitType.Monk           => Monk(parent, pos, c, teamId),
            UnitType.TradeCart      => TradeCart(parent, pos, c),
            UnitType.Longbowman     => Longbowman(parent, pos, c, teamId),
            UnitType.Skirmisher     => Skirmisher(parent, pos, c, teamId),
            UnitType.Camel          => Camel(parent, pos, c),
            UnitType.Ram            => Ram(parent, pos, c),
            UnitType.Mangonel       => Mangonel(parent, pos, c),
            UnitType.CavalryArcher  => CavalryArcher(parent, pos, c),
            UnitType.Galley         => Galley(parent, pos, c, navalAgentTypeId),
            UnitType.FireShip       => FireShip(parent, pos, c, navalAgentTypeId),
            UnitType.DemoShip       => DemoShip(parent, pos, c, navalAgentTypeId),
            UnitType.FishingShip    => FishingShip(parent, pos, c, navalAgentTypeId),
            UnitType.TeutonicKnight => TeutonicKnight(parent, pos, c),
            UnitType.WarElephant    => WarElephant(parent, pos, c),
            UnitType.Mangudai       => Mangudai(parent, pos, c),
            UnitType.Samurai        => Samurai(parent, pos, c),
            UnitType.ThrowingAxeman => ThrowingAxeman(parent, pos, c),
            UnitType.Cataphract     => Cataphract(parent, pos, c),
            UnitType.Berserk        => Berserk(parent, pos, c),
            UnitType.Mameluke       => Mameluke(parent, pos, c),
            UnitType.WoadRaider     => WoadRaider(parent, pos, c),
            UnitType.ChuKoNu        => ChuKoNu(parent, pos, c),
            UnitType.Huskarl        => Huskarl(parent, pos, c),
            UnitType.Janissary      => Janissary(parent, pos, c),
            UnitType.Eagle          => Eagle(parent, pos, c),
            UnitType.EliteEagle     => Eagle(parent, pos, c),   // Elite Eagle = Eagle visual + EliteEagle tech bonuses
            UnitType.King           => King(parent, pos, c, teamId),
            _                       => Villager(parent, pos, c, teamId),
        };
        if (e != null) e.teamId = teamId;   // covers the no-teamId factory methods
        return e;
    }

    static GameObject NewUnit(string name, Transform parent, Vector3 worldPos)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        return g;
    }

    static UnitEntity Finish(GameObject g, UnitType type, Color teamColor, GameObject visualPrefab = null, int teamId = 0)
    {
        if (visualPrefab != null)
        {
            var vis = Object.Instantiate(visualPrefab, g.transform);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;

            // Team color tint via MaterialPropertyBlock — no material instance created.
            var tintColor = Color.Lerp(Color.white, teamColor, 0.28f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", tintColor);
            foreach (var smr in vis.GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.SetPropertyBlock(block);
        }
        else
        {
            g.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);
            Prims.EnableShadows(g);
        }

        Prims.BlobShadow(g.transform, 0.65f);

        var ringGo = new GameObject("SelectionRing");
        ringGo.transform.SetParent(g.transform, false);
        ringGo.AddComponent<LineRenderer>();
        ringGo.AddComponent<SelectionRing>();

        var col = g.AddComponent<CapsuleCollider>();
        if (visualPrefab != null)
        {
            col.center = new Vector3(0, 0.75f, 0);
            col.radius = 0.35f;
            col.height = 1.5f;
        }
        else
        {
            col.center = new Vector3(0, 0.6f, 0);
            col.radius = 0.28f;
            col.height = 1.12f;
        }

        var e = g.AddComponent<UnitEntity>();
        e.unitId = _nextId++;
        e.teamId = teamId;
        e.type = type;
        e.state = UnitState.Idle;
        e.targetPos = g.transform.position;
        return e;
    }
}
