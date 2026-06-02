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

    public static ResourceNode Tree(Transform parent, Vector3 worldPos)
    {
        var g = new GameObject("Tree");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var trunkMat = Prims.Mat(Prims.Hex(0x5c3418));
        var leafMat  = Prims.Mat(Prims.Hex(0x2a6020)); // deeper forest green

        Prims.Cylinder(t, new Vector3(0, 0.7f, 0), 0.25f, 1.4f, trunkMat);
        Prims.Cone(t, new Vector3(0, 1.5f, 0), 1.3f, 1.4f, 7, leafMat);
        Prims.Cone(t, new Vector3(0, 2.3f, 0), 1.0f, 1.2f, 7, leafMat);
        Prims.Cone(t, new Vector3(0, 3.0f, 0), 0.7f, 1.0f, 7, leafMat);

        // per-tree variation
        float s = Random.Range(0.85f, 1.25f);
        t.localScale = new Vector3(s, s, s);
        t.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        Prims.BlobShadow(t, 0.7f);
        Prims.EnableShadows(g);
        return Finish(g, ResourceKind.Wood, TreeWood);
    }

    public static ResourceNode GoldMine(Transform parent, Vector3 worldPos)
    {
        var g = OrePile(parent, worldPos, "GoldMine", Prims.Hex(0xf2c14e), Prims.Hex(0xb8860b), 0.6f);
        return Finish(g, ResourceKind.Gold, GoldAmount);
    }

    public static ResourceNode StoneMine(Transform parent, Vector3 worldPos)
    {
        var g = OrePile(parent, worldPos, "StoneMine", Prims.Hex(0xb9b9b9), Prims.Hex(0x7d7d7d), 0.1f);
        return Finish(g, ResourceKind.Stone, StoneAmount);
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
        var be = farmRoot.GetComponent<BuildingEntity>();
        node.ownerTeamId = be != null ? be.teamId : 0;
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
