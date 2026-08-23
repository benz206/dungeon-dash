using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace DungeonDash.EditorTools
{
    public static class SpriteAtlasTools
    {
        const string AtlasDir = "Assets/Generated/Atlases";

        static readonly (string atlas, string[] folders)[] Groups =
        {
            ("Tiles", new[] { "Assets/Art/Tiles" }),
            ("Characters", new[] { "Assets/Art/Characters" }),
            ("Enemies", new[] { "Assets/Art/Enemies" }),
            ("Objects", new[] { "Assets/Art/Items", "Assets/Art/Weapons", "Assets/Art/UI" }),
        };

        [MenuItem("Tools/Dungeon Dash/4. Generate Sprite Atlases", false, 23)]
        public static void GenerateSpriteAtlases()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2 &&
                EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2Build)
            {
                Debug.LogWarning("[DungeonDash] Sprite Packer is not in V2 mode; the generated " +
                                 "atlases will be ignored. Set Project Settings > Editor > Sprite Packer.");
            }

            EnsureFolder(AtlasDir);
            foreach (var (atlas, folders) in Groups) BuildAtlas(atlas, folders);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DungeonDash] Packed {Groups.Length} sprite atlases into {AtlasDir}.");
        }

        static void BuildAtlas(string name, string[] folders)
        {
            string path = $"{AtlasDir}/{name}.spriteatlasv2";
            var asset = new SpriteAtlasAsset();
            foreach (var folder in folders)
            {
                var folderAsset = AssetDatabase.LoadAssetAtPath<Object>(folder);
                if (folderAsset == null)
                {
                    Debug.LogWarning($"[DungeonDash] Atlas '{name}' skipped missing folder {folder}.");
                    continue;
                }
                asset.Add(new[] { folderAsset });
            }

            SpriteAtlasAsset.Save(asset, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (importer == null)
            {
                Debug.LogError($"[DungeonDash] Atlas '{name}' did not import as a Sprite Atlas V2 asset.");
                return;
            }

            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                padding = 4,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                filterMode = FilterMode.Point,
                generateMipMaps = false,
                sRGB = true,
                readable = false
            };
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                maxTextureSize = 4096,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Uncompressed
            });
            importer.includeInBuild = true;
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
