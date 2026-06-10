using UnityEngine;

/// <summary>
/// Harvestable resource node meshes (trees, gold/stone mines) — C# port of the
/// Three.js ResourceNode factory, kept stylized and asset-free. Each builder
/// attaches a <see cref="ResourceNode"/> component plus a bounds collider to the
/// root and returns it, so the gather/selection systems can find and raycast it.
/// </summary>
public static class ResourceFactory
{
    // Starting stockpiles per node (Unity-port values).
    const int TreeWood = 100;
    const int GoldAmount = 800;
    const int StoneAmount = 600;

    // Two model pools: dense conifers for the coastal forest wall (deep-forest read),
    // broadleaf for interior clumps and the per-base starting wood line.
    public enum TreeKind { Broadleaf, Conifer }
    static readonly string[] BroadleafModels =
        { "Nature/tree_default", "Nature/tree_oak", "Nature/tree_fat", "Nature/tree_detailed", "Nature/tree_tall", "Nature/tree_small" };
    static readonly string[] ConiferModels =
        { "Nature/tree_pineDefaultA", "Nature/tree_pineDefaultB", "Nature/tree_pineRoundA", "Nature/tree_pineRoundB",
          "Nature/tree_pineRoundC", "Nature/tree_pineTallA", "Nature/tree_pineTallB", "Nature/tree_pineSmallA", "Nature/tree_pineGroundA" };
    static readonly string[] RockModels = { "Nature/rock_largeA", "Nature/rock_largeB", "Nature/rock_largeC", "Nature/rock_largeD" };

    public static ResourceNode Tree(Transform parent, Vector3 worldPos, TreeKind kind = TreeKind.Broadleaf)
    {
        var g = new GameObject("Tree");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;

        float s   = Random.Range(2.6f, 3.6f);  // Kenney trees are unit-scale, ~3 world-units looks right
        float yaw = Random.Range(0f, 360f);
        string[] pool = kind == TreeKind.Conifer ? ConiferModels : BroadleafModels;
        string model = pool[Random.Range(0, pool.Length)];

        var mesh = KenneyModels.Spawn(model, g.transform, Vector3.zero, s, yaw);
        if (mesh != null)
        {
            // The Kenney Nature kit paints pine foliage a cool teal; recolour just the
            // foliage parts to a warm forest green (trunks stay brown). Slight per-tree
            // jitter keeps the canopy from looking flat.
            Color foliage = kind == TreeKind.Conifer
                ? Prims.Hex(0x2f6b30) : Prims.Hex(0x4a8c3a);
            foliage = Color.Lerp(foliage, foliage * 1.15f, Random.value);
            RecolorFoliage(mesh, foliage);
        }
        else
        {
            // Fallback: procedural cone-stack tree.
            var t = g.transform;
            var trunkMat = Prims.Mat(Prims.Hex(0x5c3418));
            var leafMat  = Prims.Mat(Prims.Hex(0x2a6020));
            Prims.Cylinder(t, new Vector3(0, 0.7f, 0), 0.25f, 1.4f, trunkMat);
            Prims.Cone(t, new Vector3(0, 1.5f, 0), 1.3f, 1.4f, 7, leafMat);
            Prims.Cone(t, new Vector3(0, 2.3f, 0), 1.0f, 1.2f, 7, leafMat);
            Prims.Cone(t, new Vector3(0, 3.0f, 0), 0.7f, 1.0f, 7, leafMat);
            g.transform.localScale = Vector3.one * s;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        Prims.BlobShadow(g.transform, 1.2f);
        Prims.EnableShadows(g);
        return Finish(g, ResourceKind.Wood, TreeWood);
    }

    static readonly MaterialPropertyBlock _foliageMpb = new();

    /// <summary>Recolour the foliage submeshes of a Kenney Nature model to a forest green.
    /// The kit paints pine/leaf foliage a cool teal and packs trunk + foliage as separate
    /// submeshes on one renderer, so we tint per-submesh (green-dominant material → green)
    /// and leave the brown trunk and any coloured flower petals alone. A property block
    /// keeps the shared asset materials untouched.</summary>
    public static void RecolorFoliage(GameObject root, Color foliage)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || !m.HasProperty("_Color")) continue;
                var c = m.color;                   // foliage: green dominant; trunk: red dominant
                if (c.g >= c.r && c.g > 0.35f)
                {
                    _foliageMpb.Clear();
                    _foliageMpb.SetColor("_Color", foliage);
                    r.SetPropertyBlock(_foliageMpb, i);
                }
            }
        }
    }

    public static ResourceNode GoldMine(Transform parent, Vector3 worldPos)
    {
        var g = KenneyRockPile(parent, worldPos, "GoldMine");
        if (g == null) g = OrePile(parent, worldPos, "GoldMine", Prims.Hex(0xf2c14e), Prims.Hex(0xb8860b), 0.6f);
        return Finish(g, ResourceKind.Gold, GoldAmount);
    }

    public static ResourceNode StoneMine(Transform parent, Vector3 worldPos)
    {
        var g = KenneyRockPile(parent, worldPos, "StoneMine");
        if (g == null) g = OrePile(parent, worldPos, "StoneMine", Prims.Hex(0xb9b9b9), Prims.Hex(0x7d7d7d), 0.1f);
        return Finish(g, ResourceKind.Stone, StoneAmount);
    }

    static GameObject KenneyRockPile(Transform parent, Vector3 worldPos, string name)
    {
        var first = KenneyModels.Spawn(RockModels[0], null, Vector3.zero);
        if (first == null) return null;
        if (Application.isPlaying) Object.Destroy(first);
        else Object.DestroyImmediate(first);

        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;

        int count = Random.Range(2, 4);
        for (int i = 0; i < count; i++)
        {
            float a = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            float d = Random.Range(0.3f, 0.8f);
            float s = Random.Range(1.4f, 2.2f);  // Kenney rocks are unit-scale
            string rock = RockModels[Random.Range(0, RockModels.Length)];
            KenneyModels.Spawn(rock, g.transform,
                new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), s,
                Random.Range(0f, 360f));
        }

        Prims.BlobShadow(g.transform, 0.8f);
        Prims.EnableShadows(g);
        return g;
    }

    /// <summary>
    /// Turn a finished Farm building into a gatherable food field. The farm root
    /// already carries a BoxCollider (selection/raycast), so no extra collider is
    /// added. The node opts out of destroy-on-deplete so the building persists when
    /// emptied; food is the only renewable-by-rebuild resource in the slice.
    /// </summary>
    public static ResourceNode FarmField(GameObject farmRoot, int amount = 300)
    {
        var node = farmRoot.GetComponent<ResourceNode>() ?? farmRoot.AddComponent<ResourceNode>();
        node.Init(ResourceKind.Food, amount);
        node.destroyOnDeplete = false;
        node.gathererCap = 4;
        // Renewable: when emptied, spend the owning team's wood to re-seed.
        node.renewable = true;
        node.reseedWoodCost = 60;
        node.decayPerSecond = 2f; // idle farms slowly lose food, incentivising villager assignment
        var be = farmRoot.GetComponent<BuildingEntity>();
        node.ownerTeamId = be != null ? be.teamId : 0;
        return node;
    }

    // ── Alternative food sources ──────────────────────────────────────────────

    /// <summary>Berry bush: medium food, faster to deplete than a farm.</summary>
    public static ResourceNode BerryBush(Transform parent, Vector3 worldPos)
    {
        var g = new GameObject("BerryBush");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;
        var bushMat  = Prims.Mat(Prims.Hex(0x2d7a2d), 0f, 0.5f);
        var berryMat = Prims.Mat(Prims.Hex(0xd04050), 0f, 0.6f);
        Prims.Sphere(t, new Vector3(0, 0.4f, 0), 0.45f, bushMat);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI * 2f / 6f;
            Prims.Sphere(t, new Vector3(Mathf.Cos(a)*0.35f, 0.45f, Mathf.Sin(a)*0.35f), 0.1f, berryMat);
        }
        Prims.BlobShadow(t, 0.5f);
        Prims.EnableShadows(g);
        return Finish(g, ResourceKind.Food, 200);
    }

    /// <summary>Fish pond / shallow water patch that yields food.</summary>
    public static ResourceNode FishPond(Transform parent, Vector3 worldPos)
    {
        var g = new GameObject("FishPond");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;
        var waterMat = Prims.Mat(Prims.Hex(0x2255aa), 0.1f, 0.85f);
        var ripple   = Prims.Mat(Prims.Hex(0x3399cc), 0.05f, 0.9f);
        Prims.Box(t, new Vector3(0, 0.03f, 0), new Vector3(2.0f, 0.06f, 2.0f), waterMat);
        Prims.Box(t, new Vector3(0, 0.07f, 0), new Vector3(1.4f, 0.02f, 1.4f), ripple);
        Prims.EnableShadows(g);
        var node = Finish(g, ResourceKind.Food, 250);
        node.gathererCap = 3;
        return node;
    }

    /// <summary>Attach node data + a raycast collider to the mesh root.</summary>
    static ResourceNode Finish(GameObject g, ResourceKind kind, int amount)
    {
        Prims.AddBoundsCollider(g);
        var node = g.AddComponent<ResourceNode>();
        node.Init(kind, amount);
        return node;
    }

    static GameObject OrePile(Transform parent, Vector3 worldPos, string name, Color light, Color dark, float metallic)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var lightMat = Prims.Mat(light, metallic, 0.5f);
        var darkMat = Prims.Mat(dark, 0f, 0.3f);

        int count = Random.Range(5, 8);
        for (int i = 0; i < count; i++)
        {
            float r = Random.Range(0.35f, 0.7f);
            float a = Random.Range(0f, Mathf.PI * 2f);
            float d = Random.Range(0f, 0.9f);
            var rock = Prims.Sphere(t, new Vector3(Mathf.Cos(a) * d, r * 0.6f, Mathf.Sin(a) * d), r, Random.value > 0.4f ? lightMat : darkMat);
            rock.transform.localRotation = Random.rotation;
        }
        Prims.EnableShadows(g);
        return g;
    }
}
