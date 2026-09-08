using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DungeonDash.EditorTools
{
    public static class BrandingTools
    {
        [MenuItem("Tools/Dungeon Dash/Apply App Branding")]
        public static void Apply()
        {
            const string path = "Assets/Art/Branding/AppIcon.png";
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (icon == null) throw new InvalidOperationException($"Missing app icon: {path}");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            foreach (var target in new[] { NamedBuildTarget.Unknown, NamedBuildTarget.Standalone })
                PlayerSettings.SetIcons(target, PlayerSettings.GetIconSizes(target, IconKind.Any)
                    .Select(_ => icon).ToArray(), IconKind.Any);
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var slot in icons) slot.SetTextures(icon);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
