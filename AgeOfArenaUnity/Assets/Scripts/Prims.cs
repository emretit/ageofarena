using UnityEngine;

/// <summary>
/// Low-level mesh / material helpers. Mirrors the box/cylinder/cone/sphere
/// building blocks used by the Three.js version so building factories read
/// the same way.
/// </summary>
public static class Prims
{
    static Shader _standard;
    static Shader Standard => _standard != null ? _standard : (_standard = Shader.Find("Standard"));

    public static Material Mat(Color color, float metallic = 0f, float smoothness = 0.15f)
    {
        var m = new Material(Standard);
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    public static Color Hex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f
        );
    }

    static GameObject Spawn(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        // Primitive colliders aren't needed for static decoration; keep them only
        // where selection/raycast will use them later.
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>Axis-aligned box. size = full extents (like THREE.BoxGeometry).</summary>
    public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material mat)
        => Spawn(PrimitiveType.Cube, parent, pos, size, mat);

    /// <summary>Cylinder. Unity cylinder is 2 units tall by default, so height scales by /2.</summary>
    public static GameObject Cylinder(Transform parent, Vector3 pos, float radius, float height, Material mat)
        => Spawn(PrimitiveType.Cylinder, parent, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat);

    public static GameObject Sphere(Transform parent, Vector3 pos, float radius, Material mat)
        => Spawn(PrimitiveType.Sphere, parent, pos, new Vector3(radius * 2f, radius * 2f, radius * 2f), mat);

    /// <summary>
    /// Cone / pyramid built from an n-gon base to a single apex. segments=4 gives
    /// the pyramidal roof look used for the Town Center / houses.
    /// </summary>
    public static GameObject Cone(Transform parent, Vector3 pos, float radius, float height, int segments, Material mat, float yaw = 0f)
    {
        var go = new GameObject("Cone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);

        var verts = new Vector3[segments + 2];
        verts[0] = new Vector3(0, height, 0);          // apex
        verts[segments + 1] = Vector3.zero;            // base center
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
        }

        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < segments; i++)
        {
            int cur = 1 + i;
            int next = 1 + (i + 1) % segments;
            // side
            tris.Add(0); tris.Add(next); tris.Add(cur);
            // base
            tris.Add(segments + 1); tris.Add(cur); tris.Add(next);
        }

        var mesh = new Mesh { name = "ConeMesh" };
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    /// <summary>Enable shadow casting/receiving on every renderer under a root.</summary>
    public static void EnableShadows(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }
    }

    /// <summary>
    /// Add a single <see cref="BoxCollider"/> to <paramref name="root"/> sized to
    /// the combined local-space bounds of its child renderers. Used to give
    /// selection/command raycasts something to hit, since <see cref="Spawn"/>
    /// strips the primitive colliders.
    /// </summary>
    public static BoxCollider AddBoundsCollider(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return null;

        // Combine world bounds, then express them in the root's local space.
        var world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

        var col = root.AddComponent<BoxCollider>();
        col.center = root.transform.InverseTransformPoint(world.center);
        // Bounds are axis-aligned in world space; root has no rotation here, so
        // dividing extents by lossyScale gives correct local size.
        var s = root.transform.lossyScale;
        col.size = new Vector3(
            world.size.x / Mathf.Max(Mathf.Abs(s.x), 1e-4f),
            world.size.y / Mathf.Max(Mathf.Abs(s.y), 1e-4f),
            world.size.z / Mathf.Max(Mathf.Abs(s.z), 1e-4f));
        return col;
    }

    static Shader _unlit;
    static Shader Unlit =>
        _unlit != null ? _unlit
        : (_unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Standard);

    /// <summary>
    /// Flat, unlit colored material for LineRenderer rings/markers. LineRenderer
    /// startColor/endColor alone don't render without a material in Built-in RP.
    /// </summary>
    public static Material UnlitColorMat(Color color)
    {
        var m = new Material(Unlit);
        m.color = color;            // "_Color" on Unlit/Color, "_Color"/main on Sprites/Default
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        return m;
    }
}
