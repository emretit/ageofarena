using UnityEngine;

/// <summary>
/// Loads Kenney CC0 model packs from <c>Resources/Kenney/</c> and instantiates
/// them as scene objects.
///
/// Castle Kit paints every mesh from a single colour-atlas texture
/// (<c>Textures/colormap.png</c>) which Unity binds automatically on import
/// because the FBX's embedded material is also named "colormap".
/// Nature Kit uses per-part coloured materials (no atlas) and renders correctly
/// straight from import. Either way one material per pack = low draw-call for RTS.
/// </summary>
public static class KenneyModels
{
    /// <summary>
    /// Instantiate a Kenney model by Resources sub-path (e.g. "Nature/tree_default").
    /// Returns <c>null</c> if the path doesn't resolve so callers can fall back to
    /// the procedural <see cref="BuildingFactory"/> / <see cref="ResourceFactory"/> meshes.
    /// </summary>
    public static GameObject Spawn(string path, Transform parent, Vector3 worldPos,
        float scale = 1f, float yaw = 0f, float yOffset = 0f)
    {
        var prefab = Resources.Load<GameObject>("Kenney/" + path);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab);
        go.name = prefab.name;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos + new Vector3(0f, yOffset, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }
}
