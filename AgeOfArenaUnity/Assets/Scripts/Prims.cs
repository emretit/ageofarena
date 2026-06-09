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

    // N1.mat: shared material cache — same (color, metallic, smoothness) → same Material instance.
    // This allows GPU instancing to batch identical primitives into single draw calls.
    static readonly System.Collections.Generic.Dictionary<(Color, float, float), Material> _matCache = new();

    public static Material Mat(Color color, float metallic = 0f, float smoothness = 0.15f)
    {
        var key = (color, metallic, smoothness);
        if (_matCache.TryGetValue(key, out var cached)) return cached;
        var m = new Material(Standard);
        m.color       = color;
        m.SetFloat("_Metallic",    metallic);
        m.SetFloat("_Glossiness",  smoothness);
        m.enableInstancing = true;   // allow GPU instancing when many identical meshes share this mat
        _matCache[key] = m;
        return m;
    }

    /// <summary>Clear the material cache (call on scene restart to avoid stale references).</summary>
    public static void ClearMatCache() => _matCache.Clear();

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
        DestroyGeneratedCollider(go);
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
            // Blob decals are flat ground-huggers — they must never cast a real shadow.
            if (r.gameObject.name == "BlobShadow") continue;
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

    // ── Contact (blob) shadows ───────────────────────────────────────────────
    static Texture2D _blobTex;
    static Material _blobMat;

    static Texture2D BlobTex()
    {
        if (_blobTex != null) return _blobTex;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 center .. 1 edge
                float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d)) * 0.4f;     // soft falloff, max 40%
                px[y * N + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _blobTex = tex;
        return tex;
    }

    static Material BlobMat()
    {
        if (_blobMat != null) return _blobMat;
        var sh = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Standard;
        _blobMat = new Material(sh) { mainTexture = BlobTex() };
        return _blobMat;
    }

    /// <summary>
    /// Fake contact/AO shadow: a flat, ground-hugging radial-gradient quad that makes
    /// units and buildings read as sitting on the ground. Cheap (one shared material,
    /// no real-time shadow cost) — used alongside the directional shadow map.
    /// </summary>
    public static GameObject BlobShadow(Transform parent, float radius, float yOffset = 0.02f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "BlobShadow";
        DestroyGeneratedCollider(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, textured face up
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = BlobMat();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static void DestroyGeneratedCollider(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider == null) return;

        if (Application.isPlaying)
            Object.Destroy(collider);
        else
            Object.DestroyImmediate(collider);
    }
}
