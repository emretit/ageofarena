using UnityEngine;

/// <summary>
/// Loads Quaternius "Ultimate Fantasy RTS" CC0 building models from
/// <c>Resources/Quaternius/RTS/</c>. Unlike Kenney Castle Kit (single colour
/// atlas) these FBX embed per-material RGB colours, so they render correctly
/// straight from import with no texture binding required.
///
/// Each model is auto-scaled to a target ground footprint and dropped so its
/// base sits on the building root's local y=0 plane — this hides the packs'
/// arbitrary native Blender scale and lets <see cref="BuildingFactory"/> size
/// every building from one number.
///
/// Bounds are computed from the meshes (<see cref="MeshFilter.sharedMesh"/>),
/// NOT from <c>Renderer.bounds</c>: a freshly instantiated renderer reports a
/// stale/zero world AABB on the frame it is created, which made buildings spawn
/// at wildly wrong scales (e.g. a dock 7 units tall). Mesh bounds are immediate
/// and frame-independent, so the fit is deterministic in both edit and play mode.
/// </summary>
public static class QuaterniusBuildings
{
    /// <summary>True if the model exists in Resources (cheap, cached load).</summary>
    public static bool Has(string model) =>
        Resources.Load<GameObject>("Quaternius/RTS/" + model) != null;

    /// <summary>
    /// Instantiate <paramref name="model"/> under <paramref name="root"/>, scaled so
    /// its widest ground dimension (or its height, when <paramref name="byHeight"/>)
    /// equals <paramref name="targetSize"/>, with its base resting on the root origin.
    /// Returns <c>null</c> when the model is missing so callers can fall back to
    /// procedural / Kenney meshes.
    /// </summary>
    public static GameObject Spawn(string model, Transform root, float targetSize,
        float yaw = 0f, Vector3 offset = default, bool byHeight = false, float pitch = 0f)
    {
        var prefab = Resources.Load<GameObject>("Quaternius/RTS/" + model);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab);
        go.name = model;
        go.transform.SetParent(root, false);
        // pitch corrects models authored Z-up (lying on their back) to stand upright.
        go.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        FitToFootprint(go.transform, root, targetSize, byHeight);
        go.transform.localPosition += offset;
        return go;
    }

    /// <summary>Local-space top height of the fitted model (for flag placement).</summary>
    public static float Height(GameObject model, Transform root) =>
        TryLocalBounds(model.transform, root, out var b) ? b.max.y : 1f;

    // Uniformly scale so the chosen dimension equals targetSize, then shift so the
    // combined mesh bounds are centred on XZ and the base touches y=0.
    static void FitToFootprint(Transform model, Transform root, float targetSize, bool byHeight)
    {
        if (!TryLocalBounds(model, root, out var b)) return;
        float dim = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        if (dim > 1e-4f) model.localScale *= targetSize / dim;

        if (!TryLocalBounds(model, root, out b)) return;
        model.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
    }

    // Combined AABB of all child meshes, expressed in <paramref name="root"/>'s local
    // space. Uses sharedMesh.bounds corners (reliable the frame the object spawns).
    static bool TryLocalBounds(Transform model, Transform root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        var filters = model.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            Bounds mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, Corners[i]);
                Vector3 local = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                else bounds.Encapsulate(local);
            }
        }
        return any;
    }

    static readonly Vector3[] Corners =
    {
        new(-1,-1,-1), new(-1,-1, 1), new(-1, 1,-1), new(-1, 1, 1),
        new( 1,-1,-1), new( 1,-1, 1), new( 1, 1,-1), new( 1, 1, 1),
    };
}
