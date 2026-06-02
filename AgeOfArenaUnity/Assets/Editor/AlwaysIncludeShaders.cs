#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The scene is built fully procedurally (no materials live in any asset), so the
/// shaders we resolve at runtime via <c>Shader.Find</c> have nothing anchoring them
/// into a WebGL build and can be stripped — showing up as magenta / a missing skybox.
/// This guarantees they ship by adding them to GraphicsSettings → Always Included
/// Shaders. Idempotent; runs once on editor load and only writes when something is
/// missing. Uses real <c>Shader.Find</c> references (no hand-written built-in fileIDs).
/// </summary>
[InitializeOnLoad]
public static class AlwaysIncludeShaders
{
    static readonly string[] Required =
    {
        "Standard",
        "Skybox/Procedural",
        "Unlit/Transparent",
        "Unlit/Color",
        "Sprites/Default",
    };

    static AlwaysIncludeShaders()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0) return;

        var so = new SerializedObject(assets[0]);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null) return;

        var present = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
            present.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

        bool changed = false;
        foreach (var name in Required)
        {
            var sh = Shader.Find(name);
            if (sh == null || present.Contains(sh)) continue;
            int idx = list.arraySize;
            list.InsertArrayElementAtIndex(idx);
            list.GetArrayElementAtIndex(idx).objectReferenceValue = sh;
            present.Add(sh);
            changed = true;
        }

        if (changed) so.ApplyModifiedProperties();
    }
}
#endif
