// Imports any PNG under Assets/Resources/UI/ as a 9-slice UI sprite with the
// correct border so UiSkin can Resources.Load<Sprite> it directly. Without this,
// Unity's default importer would treat the file as a plain Texture2D (NPOT-scaled,
// no border, not loadable as a Sprite), breaking the sliced HUD frames.
//
// Borders are keyed off the Kenney "UI Pack RPG Expansion" (CC0) file names.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

class UiSpriteImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (p.IndexOf("/Resources/UI/", StringComparison.Ordinal) < 0) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.npotScale           = TextureImporterNPOTScale.None; // keep exact pixel size
        ti.mipmapEnabled       = false;
        ti.filterMode          = FilterMode.Bilinear;
        ti.alphaIsTransparency = true;
        ti.wrapMode            = TextureWrapMode.Clamp;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect; // FullRect required for sliced
        s.spriteBorder   = BorderFor(Path.GetFileNameWithoutExtension(p));
        ti.SetTextureSettings(s);
    }

    // Vector4 = (left, bottom, right, top) in pixels.
    static Vector4 BorderFor(string name)
    {
        if (name.StartsWith("panelInset"))   return new Vector4(14, 14, 14, 14);
        if (name.StartsWith("panel"))        return new Vector4(16, 16, 16, 16);
        if (name.StartsWith("buttonLong"))   return new Vector4(16, 12, 16, 12);
        if (name.StartsWith("buttonSquare")) return new Vector4(10, 10, 10, 10);
        return new Vector4(8, 8, 8, 8);
    }
}
