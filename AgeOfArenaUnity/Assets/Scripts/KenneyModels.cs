using UnityEngine;

/// <summary>
/// Loads Kenney CC0 model packs from <c>Resources/Kenney/</c> and instantiates
/// them as scene objects. Each pack's FBX embeds a material named "colormap" that
/// binds to the co-located <c>Textures/colormap.png</c> atlas on import (FantasyTown
/// &amp; Castle); Nature uses per-part coloured materials with no texture. Either way
/// instantiating the imported model GameObject brings mesh + material with no extra
/// wiring — one atlas material per pack = low draw-call for an RTS.
/// </summary>
public static class KenneyModels
{
    /// <summary>
    /// Instantiate a Kenney model by Resources sub-path (e.g. "FantasyTown/windmill").
    /// Returns <c>null</c> if the path doesn't resolve, so callers can fall back to
    /// the procedural <see cref="BuildingFactory"/> meshes.
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
