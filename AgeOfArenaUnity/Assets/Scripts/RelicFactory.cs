using UnityEngine;

/// <summary>
/// Procedural relic / control-point marker: a stone pedestal topped by a glowing
/// orb. The orb material is handed to the <see cref="RelicEntity"/> so capture can
/// re-tint it to the owning team's colour. No collider / NavMeshObstacle — relics
/// are walk-through map features that units capture by standing nearby.
/// </summary>
public static class RelicFactory
{
    public static RelicEntity Relic(Transform parent, Vector3 worldPos)
    {
        var g = new GameObject("Relic");
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var t = g.transform;

        var stoneMat = Prims.Mat(Prims.Hex(0x9a8f7a), 0.1f, 0.25f);   // pale stone pedestal
        // Neutral gold orb; bright + glossy so it reads as a special objective.
        var orbMat   = Prims.Mat(new Color(1f, 0.82f, 0.2f), 0.5f, 0.9f);

        Prims.Cylinder(t, new Vector3(0, 0.15f, 0), 0.95f, 0.3f, stoneMat); // base disc
        Prims.Cylinder(t, new Vector3(0, 0.7f, 0),  0.32f, 1.0f, stoneMat); // pillar
        Prims.Box(t,      new Vector3(0, 1.2f, 0),  new Vector3(0.7f, 0.16f, 0.7f), stoneMat); // capital
        Prims.Sphere(t,   new Vector3(0, 1.55f, 0), 0.42f, orbMat);         // glowing orb
        // Four small accent posts around the base ring.
        foreach (var ax in new[] { -0.7f, 0.7f })
            foreach (var az in new[] { -0.7f, 0.7f })
                Prims.Box(t, new Vector3(ax, 0.35f, az), new Vector3(0.14f, 0.5f, 0.14f), stoneMat);

        Prims.EnableShadows(g);

        var relic = g.AddComponent<RelicEntity>();
        relic.SetOrb(orbMat);
        return relic;
    }
}
