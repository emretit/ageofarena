using UnityEngine;

/// <summary>
/// Non-harvestable ground foliage (bushes, grass tufts, flowers, stumps, mushrooms,
/// water lilies) scattered to make the island read as a lush, dense Arena. Pure
/// decoration: colliders are stripped so it never affects raycasts, selection,
/// pathing or gathering. Every model comes from the existing Kenney Nature kit
/// (Resources/Kenney/Nature) — no new assets required.
/// </summary>
public static class Decor
{
    static readonly string[] Bushes =
        { "Nature/plant_bush", "Nature/plant_bushDetailed", "Nature/plant_bushLarge", "Nature/plant_bushSmall", "Nature/plant_bushTriangle" };
    static readonly string[] Grass =
        { "Nature/grass", "Nature/grass_large", "Nature/grass_leafs", "Nature/grass_leafsLarge" };
    static readonly string[] Flowers =
        { "Nature/flower_redA", "Nature/flower_yellowA", "Nature/flower_purpleA", "Nature/flower_redB", "Nature/flower_yellowB", "Nature/flower_purpleB" };
    static readonly string[] Stumps =
        { "Nature/stump_round", "Nature/stump_old", "Nature/stump_square", "Nature/mushroom_redGroup", "Nature/mushroom_tanGroup" };
    static readonly string[] Lilies = { "Nature/lily_small", "Nature/lily_large" };

    /// <summary>Scatter a mix of ground foliage in an annulus (rInner..rOuter) around a center.</summary>
    public static void Scatter(Transform parent, Vector3 center, float rInner, float rOuter, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Lerp(rInner, rOuter, Mathf.Sqrt(Random.value)); // area-uniform
            var pos = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            string[] pool = PickPool();
            Spawn(pool[Random.Range(0, pool.Length)], parent, pos, Random.Range(0.8f, 1.6f));
        }
    }

    /// <summary>Water lilies floating in the coastal shallows (annulus rInner..rOuter from origin).</summary>
    public static void ScatterLilies(Transform parent, float rInner, float rOuter, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(rInner, rOuter);
            var pos = new Vector3(Mathf.Cos(a) * r, 0.04f, Mathf.Sin(a) * r);
            Spawn(Lilies[Random.Range(0, Lilies.Length)], parent, pos, Random.Range(1.0f, 1.8f));
        }
    }

    static string[] PickPool()
    {
        float roll = Random.value;
        if (roll < 0.40f) return Grass;
        if (roll < 0.70f) return Bushes;
        if (roll < 0.90f) return Flowers;
        return Stumps;
    }

    static readonly Color FoliageGreen = Prims.Hex(0x57a23e);

    static void Spawn(string model, Transform parent, Vector3 worldPos, float scale)
    {
        var go = KenneyModels.Spawn(model, parent, worldPos, scale, Random.Range(0f, 360f));
        if (go == null) return;
        // Decoration must never intercept clicks or block pathing.
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        // The Nature kit tints grass/leaves a cool teal — warm them to green (flower petals
        // and other non-green submeshes are left untouched by the per-submesh check).
        ResourceFactory.RecolorFoliage(go, FoliageGreen);
    }
}
