using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DungeonDash.EditorTools
{
    public static class LevelLibraryTools
    {
        const string LibraryPath = "Assets/Resources/LevelLibrary.asset";

        [MenuItem("Tools/Dungeon Dash/5. Generate Level Library", false, 24)]
        public static void GenerateLevelLibrary()
        {
            if (AssetDatabase.LoadAssetAtPath<LevelLibrary>(LibraryPath) != null)
            {
                Debug.Log($"[DungeonDash] Level library already exists at {LibraryPath}; left untouched.");
                return;
            }
            RebuildDefaultLevelLibrary();
        }

        [MenuItem("Tools/Dungeon Dash/Rebuild Default Level Library", false, 40)]
        public static void RebuildDefaultLevelLibrary()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.DeleteAsset(LibraryPath);

            var library = ScriptableObject.CreateInstance<LevelLibrary>();
            library.name = "LevelLibrary";
            library.minRooms = 2;
            library.maxRooms = 4;
            AssetDatabase.CreateAsset(library, LibraryPath);

            var props = new Dictionary<string, PropDefinition>();
            foreach (var prop in DefaultProps()) props[prop.id] = Attach(library, prop, prop.id);
            library.props = props.Values.ToArray();

            library.themes = DefaultThemes(props)
                .Select(theme => Attach(library, theme, theme.id)).ToArray();
            library.templates = DefaultTemplates()
                .Select(template => Attach(library, template, template.id)).ToArray();

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DungeonDash] Level library: {library.themes.Length} themes, " +
                      $"{library.templates.Length} room templates, {library.props.Length} props.");
        }

        static T Attach<T>(LevelLibrary library, T child, string name) where T : ScriptableObject
        {
            child.name = name;
            AssetDatabase.AddObjectToAsset(child, library);
            return child;
        }

        static IEnumerable<PropDefinition> DefaultProps()
        {
            yield return Prop("crate", new[] { "crate" }, PropPlacement.OpenFloor,
                blocks: true, weight: 1.4f, maxPerRoom: 4, offsetY: 0.25f);
            yield return Prop("column", new[] { "column" }, PropPlacement.RoomCorner,
                blocks: true, weight: 1f, maxPerRoom: 4, offsetY: 1f);
            yield return Prop("skull", new[] { "skull" }, PropPlacement.OpenFloor,
                blocks: false, weight: 0.9f, maxPerRoom: 3);
            yield return Prop("pit", new[] { "hole" }, PropPlacement.OpenFloor,
                blocks: false, weight: 0.6f, maxPerRoom: 2);
            yield return Prop("spikes", new[]
                {
                    "floor_spikes_anim_f0", "floor_spikes_anim_f1",
                    "floor_spikes_anim_f2", "floor_spikes_anim_f3"
                }, PropPlacement.OpenFloor, blocks: false, weight: 0.8f, maxPerRoom: 3);
            yield return Prop("ladder", new[] { "floor_ladder" }, PropPlacement.AgainstNorthWall,
                blocks: false, weight: 0.5f, maxPerRoom: 1);
            yield return Prop("stairs", new[] { "floor_stairs" }, PropPlacement.RoomCenter,
                blocks: false, weight: 0.4f, maxPerRoom: 1);
            yield return Prop("lever", new[] { "lever_left" }, PropPlacement.AgainstNorthWall,
                blocks: false, weight: 0.5f, maxPerRoom: 1);
        }

        static PropDefinition Prop(string id, string[] sprites, PropPlacement placement,
            bool blocks, float weight, int maxPerRoom, float offsetY = 0f)
        {
            var prop = ScriptableObject.CreateInstance<PropDefinition>();
            prop.id = id;
            prop.spriteNames = sprites;
            prop.placement = placement;
            prop.blocksMovement = blocks;
            prop.weight = weight;
            prop.maxPerRoom = maxPerRoom;
            prop.offset = new Vector2(0f, offsetY);
            prop.framesPerSecond = sprites.Length > 1 ? 6f : 0f;
            return prop;
        }

        static IEnumerable<ChamberTheme> DefaultThemes(Dictionary<string, PropDefinition> props)
        {
            yield return Theme("catacombs", "The Catacombs", wear: 0.18f, grass: 0f, density: 0.4f,
                floor: new Color(0.96f, 0.95f, 0.98f), wall: Color.white,
                accent: new Color(0.72f, 0.53f, 0.31f),
                props: Pick(props, "crate", "skull", "column", "pit"));
            yield return Theme("overgrowth", "The Overgrowth", wear: 0.35f, grass: 0.55f, density: 0.5f,
                floor: new Color(0.9f, 1f, 0.9f), wall: new Color(0.86f, 0.95f, 0.86f),
                accent: new Color(0.42f, 0.72f, 0.36f),
                props: Pick(props, "crate", "pit", "column", "stairs", "skull"));
            yield return Theme("foundry", "The Foundry", wear: 0.75f, grass: 0f, density: 0.55f,
                floor: new Color(1f, 0.92f, 0.85f), wall: new Color(1f, 0.88f, 0.8f),
                accent: new Color(0.85f, 0.42f, 0.18f),
                props: Pick(props, "crate", "spikes", "lever", "skull"));
            yield return Theme("sanctum", "The Sanctum", wear: 0.12f, grass: 0.2f, density: 0.35f,
                floor: new Color(0.9f, 0.94f, 1f), wall: new Color(0.88f, 0.92f, 1f),
                accent: new Color(0.36f, 0.62f, 0.92f),
                props: Pick(props, "column", "ladder", "stairs", "skull"));
        }

        static PropDefinition[] Pick(Dictionary<string, PropDefinition> props, params string[] ids) =>
            ids.Where(props.ContainsKey).Select(id => props[id]).ToArray();

        static ChamberTheme Theme(string id, string displayName, float wear, float grass, float density,
            Color floor, Color wall, Color accent, PropDefinition[] props)
        {
            var theme = ScriptableObject.CreateInstance<ChamberTheme>();
            theme.id = id;
            theme.displayName = displayName;
            theme.floorWear = wear;
            theme.grassChance = grass;
            theme.propDensity = density;
            theme.floorTint = floor;
            theme.wallTint = wall;
            theme.accent = accent;
            theme.props = props;
            return theme;
        }

        static IEnumerable<RoomTemplate> DefaultTemplates()
        {
            yield return Template("entry_hall", RoomRole.Entry, RoomShape.Rectangle,
                new Vector2Int(11, 9), new Vector2Int(15, 11), weight: 1f, minDepth: 1,
                propDensity: 0.5f, enemyShare: 0.6f);
            yield return Template("entry_rotunda", RoomRole.Entry, RoomShape.Ellipse,
                new Vector2Int(11, 9), new Vector2Int(13, 11), weight: 0.5f, minDepth: 3,
                propDensity: 0.5f, enemyShare: 0.6f);
            yield return Template("combat_pit", RoomRole.Combat, RoomShape.Rectangle,
                new Vector2Int(11, 9), new Vector2Int(17, 13), weight: 1.2f, minDepth: 1,
                propDensity: 0.8f, enemyShare: 1.4f);
            yield return Template("combat_colonnade", RoomRole.Combat, RoomShape.Pillared,
                new Vector2Int(13, 11), new Vector2Int(17, 13), weight: 0.9f, minDepth: 2,
                propDensity: 0.6f, enemyShare: 1.3f);
            yield return Template("combat_crossing", RoomRole.Combat, RoomShape.Cross,
                new Vector2Int(13, 11), new Vector2Int(17, 13), weight: 0.8f, minDepth: 3,
                propDensity: 0.7f, enemyShare: 1.2f);
            yield return Template("hall_gallery", RoomRole.Hall, RoomShape.Notched,
                new Vector2Int(11, 7), new Vector2Int(15, 9), weight: 1f, minDepth: 1,
                propDensity: 1.2f, enemyShare: 0.8f);
            yield return Template("hall_rotunda", RoomRole.Hall, RoomShape.Ellipse,
                new Vector2Int(11, 9), new Vector2Int(15, 11), weight: 0.7f, minDepth: 2,
                propDensity: 1f, enemyShare: 0.9f);
            yield return Template("treasure_vault", RoomRole.Treasure, RoomShape.Rectangle,
                new Vector2Int(9, 7), new Vector2Int(11, 9), weight: 1f, minDepth: 1,
                propDensity: 1.6f, enemyShare: 0.7f);
            yield return Template("treasure_grotto", RoomRole.Treasure, RoomShape.Ellipse,
                new Vector2Int(9, 7), new Vector2Int(11, 9), weight: 0.6f, minDepth: 4,
                propDensity: 1.5f, enemyShare: 0.7f);
        }

        static RoomTemplate Template(string id, RoomRole role, RoomShape shape, Vector2Int minSize,
            Vector2Int maxSize, float weight, int minDepth, float propDensity, float enemyShare)
        {
            var template = ScriptableObject.CreateInstance<RoomTemplate>();
            template.id = id;
            template.role = role;
            template.shape = shape;
            template.minSize = minSize;
            template.maxSize = maxSize;
            template.weight = weight;
            template.minDepth = minDepth;
            template.propDensity = propDensity;
            template.enemyShare = enemyShare;
            return template;
        }
    }
}
