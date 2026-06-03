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

    public static UnitEntity Villager(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Villager", parent, worldPos);
        var t = g.transform;

        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var cloth = Prims.Mat(Prims.Hex(0x8a6b3f));
        var accent = Prims.Mat(teamColor, 0f, 0.3f);

        Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.4f, 0.7f, 0.3f), cloth);     // torso
        Prims.Box(t, new Vector3(0, 0.18f, 0.02f), new Vector3(0.42f, 0.25f, 0.34f), accent); // belt/apron
        Prims.Sphere(t, new Vector3(0, 0.95f, 0), 0.18f, skin);                            // head
        Prims.Box(t, new Vector3(0, 1.02f, 0), new Vector3(0.34f, 0.16f, 0.34f), cloth);   // cap

        var e = Finish(g, UnitType.Villager, teamColor);
        e.hp = e.maxHp = 25f;
        return e;
    }

    public static UnitEntity Militia(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Militia", parent, worldPos);
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

        var e = Finish(g, UnitType.Militia, teamColor);
        e.hp = e.maxHp = 40f;
        e.pierceArmor = 1f;
        return e;
    }

    public static UnitEntity Archer(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Archer", parent, worldPos);
        var t = g.transform;

        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var tunic = Prims.Mat(teamColor, 0f, 0.3f);
        var wood = Prims.Mat(Prims.Hex(0x6b4a2a));

        Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.4f, 0.7f, 0.3f), tunic);      // torso
        Prims.Sphere(t, new Vector3(0, 0.95f, 0), 0.18f, skin);                            // head
        Prims.Box(t, new Vector3(0, 1.04f, 0), new Vector3(0.3f, 0.18f, 0.3f), Prims.Mat(Prims.Hex(0x3a5a2a))); // hood
        // bow: tall thin curved-ish stave on the left, string implied
        Prims.Box(t, new Vector3(-0.32f, 0.7f, 0.05f), new Vector3(0.05f, 1.0f, 0.08f), wood);
        // quiver on the back
        Prims.Box(t, new Vector3(0.12f, 0.7f, -0.18f), new Vector3(0.12f, 0.5f, 0.12f), wood);

        var e = Finish(g, UnitType.Archer, teamColor);
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

        var wood   = Prims.Mat(Prims.Hex(0x7a5c3a));
        var metal  = Prims.Mat(Prims.Hex(0x888890), 0.4f, 0.6f);
        var rope   = Prims.Mat(Prims.Hex(0xc8a878));
        var accent = Prims.Mat(teamColor, 0.2f, 0.4f);

        // Base frame
        Prims.Box(t, new Vector3(0, 0.20f, 0),    new Vector3(1.2f, 0.14f, 0.7f), wood);
        // Side uprights
        Prims.Box(t, new Vector3(-0.45f, 0.65f, 0), new Vector3(0.12f, 0.82f, 0.45f), wood);
        Prims.Box(t, new Vector3( 0.45f, 0.65f, 0), new Vector3(0.12f, 0.82f, 0.45f), wood);
        // Cross beam at top
        Prims.Box(t, new Vector3(0, 1.05f, 0),    new Vector3(1.1f, 0.12f, 0.18f), wood);
        // Throwing arm (horizontal beam, tilted slightly forward)
        Prims.Box(t, new Vector3(0, 1.18f, 0.28f), new Vector3(0.1f, 0.11f, 1.6f), wood);
        // Counterweight at the rear end of arm
        Prims.Box(t, new Vector3(0, 1.32f, -0.42f), new Vector3(0.3f, 0.28f, 0.24f), metal);
        // Sling cup at front end
        Prims.Box(t, new Vector3(0, 1.48f, 0.96f), new Vector3(0.16f, 0.16f, 0.16f), rope);
        // Wheels (left / right)
        foreach (float wx in new[] { -0.62f, 0.62f })
            Prims.Box(t, new Vector3(wx, 0.16f, 0), new Vector3(0.14f, 0.32f, 0.34f), metal);
        // Team-color accent strip on frame
        Prims.Box(t, new Vector3(0, 0.52f, 0), new Vector3(1.2f, 0.07f, 0.11f), accent);

        var e = Finish(g, UnitType.Trebuchet, teamColor);
        e.hp = e.maxHp = 150f;
        e.moveSpeed = 1.8f;
        return e;
    }

    public static UnitEntity Scout(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Scout", parent, worldPos);
        var t = g.transform;

        var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
        var cloak = Prims.Mat(teamColor, 0f, 0.25f);
        var leather = Prims.Mat(Prims.Hex(0x6b4a2a));

        // Lean runner silhouette: slim torso, head, a trailing cloak and a feathered cap.
        Prims.Box(t, new Vector3(0, 0.48f, 0), new Vector3(0.32f, 0.66f, 0.24f), leather);   // torso
        Prims.Sphere(t, new Vector3(0, 0.92f, 0), 0.16f, skin);                              // head
        Prims.Box(t, new Vector3(0, 1.0f, 0), new Vector3(0.28f, 0.12f, 0.28f), cloak);      // cap
        Prims.Box(t, new Vector3(0, 1.12f, -0.02f), new Vector3(0.05f, 0.2f, 0.05f), cloak); // feather
        Prims.Box(t, new Vector3(0, 0.55f, -0.16f), new Vector3(0.34f, 0.62f, 0.06f), cloak);// cloak

        var e = Finish(g, UnitType.Scout, teamColor);
        e.hp = e.maxHp = 40f;
        e.moveSpeed = 6.5f; // fastest unit — pure recon
        e.pierceArmor = 2f;
        return e;
    }

    public static UnitEntity Medic(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Medic", parent, worldPos);
        var t = g.transform;

        var skin = Prims.Mat(Prims.Hex(0xe0ac69));
        var robe = Prims.Mat(Prims.Hex(0xe8e4d8), 0f, 0.2f);   // pale monk robe
        var trim = Prims.Mat(teamColor, 0f, 0.3f);
        var staff = Prims.Mat(Prims.Hex(0x6b4a2a));

        // Robed monk: tall robe body, hood, head, team-color sash, and a healing staff.
        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.46f, 0.92f, 0.4f), robe);        // robe body
        Prims.Box(t, new Vector3(0, 0.55f, 0), new Vector3(0.12f, 0.84f, 0.42f), trim);      // sash
        Prims.Sphere(t, new Vector3(0, 1.04f, 0), 0.17f, skin);                              // head
        Prims.Box(t, new Vector3(0, 1.12f, 0), new Vector3(0.34f, 0.2f, 0.34f), robe);       // hood
        // Staff with a small cross-piece (heal symbol).
        Prims.Box(t, new Vector3(0.3f, 0.7f, 0.08f), new Vector3(0.05f, 1.1f, 0.05f), staff);
        Prims.Box(t, new Vector3(0.3f, 1.18f, 0.08f), new Vector3(0.2f, 0.06f, 0.06f), trim);

        var e = Finish(g, UnitType.Medic, teamColor);
        e.hp = e.maxHp = 35f;
        e.moveSpeed = 3.2f;
        return e;
    }

    public static UnitEntity Spearman(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Spearman", parent, worldPos);
        var t = g.transform;

        var cloth  = Prims.Mat(teamColor, 0.1f, 0.35f);
        var metal  = Prims.Mat(Prims.Hex(0xb8b8c0), 0.6f, 0.6f);
        var skin   = Prims.Mat(Prims.Hex(0xe0ac69));
        var wood   = Prims.Mat(Prims.Hex(0x6b4a2a));

        Prims.Box(t, new Vector3(0, 0.5f, 0),     new Vector3(0.42f, 0.78f, 0.32f), cloth); // body
        Prims.Sphere(t, new Vector3(0, 1.02f, 0), 0.17f, skin);                             // head
        Prims.Box(t, new Vector3(0, 1.08f, 0),    new Vector3(0.30f, 0.16f, 0.30f), metal); // cap
        // spear: long wooden shaft + metal tip held vertically at the side
        Prims.Box(t, new Vector3(0.34f, 1.0f, 0.08f),  new Vector3(0.05f, 1.9f, 0.05f), wood);
        Prims.Box(t, new Vector3(0.34f, 1.95f, 0.08f), new Vector3(0.08f, 0.25f, 0.08f), metal);

        var e = Finish(g, UnitType.Spearman, teamColor);
        e.hp = e.maxHp = 25f;
        e.moveSpeed = 3.3f;
        e.pierceArmor = 3f;
        return e;
    }

    public static UnitEntity Skirmisher(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Skirmisher", parent, worldPos);
        var t = g.transform;

        var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
        var tunic = Prims.Mat(teamColor, 0f, 0.3f);
        var hide  = Prims.Mat(Prims.Hex(0x9a7b4f));
        var wood  = Prims.Mat(Prims.Hex(0x6b4a2a));
        var metal = Prims.Mat(Prims.Hex(0xc0c0c8), 0.6f, 0.6f);

        Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.7f, 0.32f), tunic);     // torso
        Prims.Box(t, new Vector3(0, 0.55f, 0.18f), new Vector3(0.3f, 0.42f, 0.08f), hide);  // leather jerkin
        Prims.Sphere(t, new Vector3(0, 0.95f, 0), 0.18f, skin);                              // head
        Prims.Box(t, new Vector3(0, 1.02f, 0), new Vector3(0.3f, 0.16f, 0.3f), hide);        // leather cap
        // javelin bundle on the back + one readied in hand
        Prims.Box(t, new Vector3(-0.18f, 0.8f, -0.16f), new Vector3(0.05f, 0.9f, 0.05f), wood);
        Prims.Box(t, new Vector3(0.32f, 0.9f, 0.1f),  new Vector3(0.04f, 1.0f, 0.04f), wood);
        Prims.Box(t, new Vector3(0.32f, 1.42f, 0.1f), new Vector3(0.07f, 0.18f, 0.07f), metal); // javelin tip

        var e = Finish(g, UnitType.Skirmisher, teamColor);
        e.hp = e.maxHp = 30f;
        e.moveSpeed = 3.2f;
        e.pierceArmor = 3f;   // skirmishers shrug off arrows
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

    public static UnitEntity Monk(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Monk", parent, worldPos);
        var t = g.transform;

        var robe  = Prims.Mat(Prims.Hex(0x8b6914), 0f, 0.15f); // brown robe
        var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
        var staff = Prims.Mat(Prims.Hex(0x6b4a2a));

        Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.44f, 0.85f, 0.36f), robe);    // robe body
        Prims.Sphere(t, new Vector3(0, 1.04f, 0), 0.17f, skin);                           // head (tonsure)
        Prims.Box(t, new Vector3(0, 1.12f, 0), new Vector3(0.36f, 0.22f, 0.36f), robe);  // hood
        // holy staff
        Prims.Box(t, new Vector3(0.32f, 0.8f, 0.06f), new Vector3(0.05f, 1.2f, 0.05f), staff);
        Prims.Cone(t, new Vector3(0.32f, 1.42f, 0.06f), 0.12f, 0.25f, 6, Prims.Mat(Prims.Hex(0xf2c14e), 0.2f, 0.6f), 0f);

        var e = Finish(g, UnitType.Monk, teamColor);
        e.hp = e.maxHp = 30f;
        e.moveSpeed = 2.8f;
        return e;
    }

    /// <summary>Britons unique unit: longer range than Archer, higher HP, slower move.</summary>
    public static UnitEntity Longbowman(Transform parent, Vector3 worldPos, Color teamColor)
    {
        var g = NewUnit("Longbowman", parent, worldPos);
        var t = g.transform;

        var skin  = Prims.Mat(Prims.Hex(0xe0ac69));
        var tunic = Prims.Mat(teamColor, 0f, 0.3f);
        var wood  = Prims.Mat(Prims.Hex(0x5a3a18)); // darker wood — yew longbow

        Prims.Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.72f, 0.32f), tunic);      // torso
        Prims.Sphere(t, new Vector3(0, 0.97f, 0), 0.19f, skin);                               // head
        Prims.Box(t, new Vector3(0, 1.06f, 0), new Vector3(0.28f, 0.14f, 0.28f), Prims.Mat(Prims.Hex(0x2a4822))); // hood
        // Longbow: taller and narrower than Archer's bow (yew stave)
        Prims.Box(t, new Vector3(-0.34f, 0.7f, 0.05f), new Vector3(0.04f, 1.3f, 0.07f), wood);
        // Quiver on back — wider to hold more arrows
        Prims.Box(t, new Vector3(0.14f, 0.72f, -0.20f), new Vector3(0.14f, 0.55f, 0.14f), wood);

        var e = Finish(g, UnitType.Longbowman, teamColor);
        e.hp = e.maxHp = 35f;
        e.moveSpeed = 3.0f; // slower than Archer (3.2) but more range
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

    static GameObject NewUnit(string name, Transform parent, Vector3 worldPos)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        return g;
    }

    static UnitEntity Finish(GameObject g, UnitType type, Color teamColor)
    {
        // Scale up visuals 1.25× for readability at ortho distances.
        // Collider is on root and resized to match, so gameplay is unaffected.
        g.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);

        Prims.EnableShadows(g);
        Prims.BlobShadow(g.transform, 0.65f);

        var ringGo = new GameObject("SelectionRing");
        ringGo.transform.SetParent(g.transform, false);
        ringGo.AddComponent<LineRenderer>();
        ringGo.AddComponent<SelectionRing>();

        // Collider scaled to match 1.25× visual (divide by scale).
        var col = g.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.6f, 0);
        col.radius = 0.28f;   // 0.35 / 1.25
        col.height = 1.12f;   // 1.4  / 1.25

        var e = g.AddComponent<UnitEntity>();
        e.unitId = _nextId++;
        e.teamId = 0;
        e.type = type;
        e.state = UnitState.Idle;
        e.targetPos = g.transform.position;
        return e;
    }
}
