using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DungeonDash.EditorTools
{
    /// <summary>
    /// Authoring surface for the Level Library: edit room templates, themes and props, then
    /// preview the chamber ChamberBuilder produces for a given depth and seed. The validate
    /// pass sweeps many seeds and reports layouts that would ship broken.
    /// </summary>
    public sealed class LevelBuilderWindow : EditorWindow
    {
        const string LibraryPath = "Assets/Resources/LevelLibrary.asset";
        const int MaxPreviewPixels = 620;
        const int ValidateSeedCount = 200;

        static readonly string[] Tabs = { "Room Templates", "Themes", "Props" };
        static readonly Vector2Int[] Cardinals =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        static readonly Color FloorColor = new(0.30f, 0.28f, 0.32f);
        static readonly Color CorridorColor = new(0.22f, 0.21f, 0.25f);
        static readonly Color GrassColor = new(0.24f, 0.40f, 0.24f);
        static readonly Color WallColor = new(0.09f, 0.09f, 0.12f);
        static readonly Color VoidColor = new(0.04f, 0.04f, 0.05f);
        static readonly Color DoorwayColor = new(0.90f, 0.72f, 0.30f);
        static readonly Color BlockingPropColor = new(0.86f, 0.42f, 0.30f);
        static readonly Color FlatPropColor = new(0.36f, 0.68f, 0.82f);
        static readonly Color EntryColor = new(0.40f, 0.86f, 0.44f);
        static readonly Color SpawnColor = new(0.62f, 0.24f, 0.28f);

        LevelLibrary _library;
        readonly Dictionary<Object, Editor> _editors = new();
        int _tab;
        int _selected;
        int _seed = 1;
        int _depth = 1;
        Vector2 _listScroll;
        Vector2 _inspectorScroll;
        Texture2D _preview;
        ChamberPlan _plan;
        string _status = string.Empty;

        [MenuItem("Tools/Dungeon Dash/Level Builder", false, 1)]
        public static void Open()
        {
            var window = GetWindow<LevelBuilderWindow>("Level Builder");
            window.minSize = new Vector2(940f, 580f);
        }

        void OnEnable() => Reload();

        void OnDisable()
        {
            foreach (var editor in _editors.Values) if (editor != null) DestroyImmediate(editor);
            _editors.Clear();
            if (_preview != null) DestroyImmediate(_preview);
        }

        void Reload()
        {
            _library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(LibraryPath);
            _plan = null;
        }

        void OnGUI()
        {
            if (_library == null)
            {
                EditorGUILayout.HelpBox($"No level library at {LibraryPath}.", MessageType.Warning);
                if (GUILayout.Button("Create Default Level Library"))
                {
                    LevelLibraryTools.RebuildDefaultLevelLibrary();
                    Reload();
                }
                return;
            }

            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawInspector();
            DrawPreview();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int tab = GUILayout.Toolbar(_tab, Tabs, EditorStyles.toolbarButton, GUILayout.Width(360f));
            if (tab != _tab)
            {
                _tab = tab;
                _selected = 0;
            }
            GUILayout.FlexibleSpace();
            EditorGUIUtility.labelWidth = 46f;
            _library.minRooms = EditorGUILayout.IntSlider("Rooms", _library.minRooms, 1, 6, GUILayout.Width(150f));
            _library.maxRooms = EditorGUILayout.IntSlider("to", _library.maxRooms, _library.minRooms, 8, GUILayout.Width(140f));
            EditorGUIUtility.labelWidth = 0f;
            if (GUILayout.Button("Save Library", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                EditorUtility.SetDirty(_library);
                AssetDatabase.SaveAssets();
                _status = "Library saved.";
            }
            EditorGUILayout.EndHorizontal();
        }

        Object[] CurrentList() => _tab switch
        {
            0 => _library.templates?.Cast<Object>().ToArray() ?? System.Array.Empty<Object>(),
            1 => _library.themes?.Cast<Object>().ToArray() ?? System.Array.Empty<Object>(),
            _ => _library.props?.Cast<Object>().ToArray() ?? System.Array.Empty<Object>()
        };

        void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210f));
            var entries = CurrentList();
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, entries.Length - 1));

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null) continue;
                bool selected = i == _selected;
                if (GUILayout.Toggle(selected, entries[i].name, EditorStyles.miniButton) && !selected)
                    _selected = i;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add")) AddEntry();
            using (new EditorGUI.DisabledScope(entries.Length == 0))
            {
                if (GUILayout.Button("Copy")) DuplicateEntry(entries);
                if (GUILayout.Button("Delete")) DeleteEntry(entries);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300f));
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            var entries = CurrentList();
            if (entries.Length > 0 && entries[_selected] != null)
            {
                var target = entries[_selected];
                if (!_editors.TryGetValue(target, out var editor) || editor == null)
                {
                    editor = Editor.CreateEditor(target);
                    _editors[target] = editor;
                }
                EditorGUI.BeginChangeCheck();
                editor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    target.name = EntryName(target);
                    _plan = null;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        static string EntryName(Object target) => target switch
        {
            RoomTemplate template => string.IsNullOrEmpty(template.id) ? "room" : template.id,
            ChamberTheme theme => string.IsNullOrEmpty(theme.id) ? "theme" : theme.id,
            PropDefinition prop => string.IsNullOrEmpty(prop.id) ? "prop" : prop.id,
            _ => target.name
        };

        void DrawPreview()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 44f;
            int depth = EditorGUILayout.IntSlider("Depth", _depth, 1, 12, GUILayout.Width(200f));
            int seed = EditorGUILayout.IntField("Seed", _seed, GUILayout.Width(140f));
            EditorGUIUtility.labelWidth = 0f;
            if (depth != _depth || seed != _seed)
            {
                _depth = depth;
                _seed = seed;
                _plan = null;
            }
            if (GUILayout.Button("Reroll", GUILayout.Width(70f)))
            {
                _seed++;
                _plan = null;
            }
            if (GUILayout.Button($"Validate {ValidateSeedCount} seeds", GUILayout.Width(150f))) Validate();
            EditorGUILayout.EndHorizontal();

            if (!_library.IsUsable)
            {
                EditorGUILayout.HelpBox("The library needs at least one theme and one room template.",
                    MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_plan == null) Rebuild();
            if (_preview != null)
            {
                var rect = GUILayoutUtility.GetRect(_preview.width, _preview.height, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(rect, _preview, ScaleMode.ScaleToFit);
            }

            if (_plan != null)
            {
                var layout = _plan.Layout;
                EditorGUILayout.LabelField(
                    $"{layout.Rooms.Count} rooms   {layout.Walkable.Count} floor   {layout.Walls.Count} wall   " +
                    $"{_plan.Props.Count} props   {_plan.SpawnAnchors.Count} spawns   theme: {_plan.Theme.displayName}",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(_status) ? "Ready." : _status, MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        void Rebuild()
        {
            _plan = ChamberBuilder.Build(new System.Random(_seed), _depth, _library);
            Rasterize(_plan);
        }

        void Rasterize(ChamberPlan plan)
        {
            var bounds = plan.Layout.GridBounds;
            int scale = Mathf.Max(1, MaxPreviewPixels / Mathf.Max(bounds.width, bounds.height));
            int width = bounds.width * scale;
            int height = bounds.height * scale;

            if (_preview == null || _preview.width != width || _preview.height != height)
            {
                if (_preview != null) DestroyImmediate(_preview);
                _preview = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            var pixels = new Color32[width * height];
            for (int y = 0; y < bounds.height; y++)
            for (int x = 0; x < bounds.width; x++)
                Fill(pixels, width, x, y, scale, CellColor(plan, new Vector2Int(bounds.xMin + x, bounds.yMin + y)));

            foreach (var spawn in plan.SpawnAnchors)
                Mark(pixels, width, height, bounds, scale, Vector2Int.RoundToInt(spawn), SpawnColor, 1);
            foreach (var prop in plan.Props)
                Mark(pixels, width, height, bounds, scale, Vector2Int.RoundToInt(prop.Position),
                    prop.Definition.blocksMovement ? BlockingPropColor : FlatPropColor, 2);
            Mark(pixels, width, height, bounds, scale, Vector2Int.RoundToInt(plan.Layout.EntryPoint), EntryColor, 3);
            Mark(pixels, width, height, bounds, scale,
                Vector2Int.RoundToInt(plan.Layout.ExitDoorPosition), DoorwayColor, 3);

            _preview.SetPixels32(pixels);
            _preview.Apply();
        }

        static Color CellColor(ChamberPlan plan, Vector2Int cell)
        {
            if (plan.Layout.Doorway.Contains(cell)) return DoorwayColor * 0.7f;
            if (plan.GrassCells.Contains(cell)) return GrassColor;
            if (plan.Layout.Corridors.Contains(cell)) return CorridorColor;
            if (plan.Layout.Walkable.Contains(cell))
            {
                float age = plan.Layout.FloorAge.TryGetValue(cell, out float value) ? value : 0.3f;
                return Color.Lerp(FloorColor, FloorColor * 1.5f, age);
            }
            return plan.Layout.Walls.Contains(cell) ? WallColor : VoidColor;
        }

        // The texture's y axis matches the grid's, so a preview row maps straight to a world row.
        static void Fill(Color32[] pixels, int width, int cellX, int cellY, int scale, Color color)
        {
            var packed = (Color32)color;
            for (int y = 0; y < scale; y++)
            for (int x = 0; x < scale; x++)
                pixels[(cellY * scale + y) * width + cellX * scale + x] = packed;
        }

        static void Mark(Color32[] pixels, int width, int height, RectInt bounds, int scale,
            Vector2Int cell, Color color, int radius)
        {
            int centerX = (cell.x - bounds.xMin) * scale + scale / 2;
            int centerY = (cell.y - bounds.yMin) * scale + scale / 2;
            var packed = (Color32)color;
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int px = centerX + x;
                int py = centerY + y;
                if (px < 0 || py < 0 || px >= width || py >= height) continue;
                pixels[py * width + px] = packed;
            }
        }

        void Validate()
        {
            var failures = new List<string>();
            for (int seed = 0; seed < ValidateSeedCount; seed++)
            {
                var plan = ChamberBuilder.Build(new System.Random(seed), 1 + seed % 12, _library);
                var layout = plan.Layout;
                var start = Vector2Int.RoundToInt(layout.EntryPoint);

                if (!layout.Walkable.Contains(start)) failures.Add($"seed {seed}: entry is not floor");
                else if (Reachable(layout, start) != layout.Walkable.Count)
                    failures.Add($"seed {seed}: disconnected floor");
                if (!layout.Doorway.All(layout.Walkable.Contains))
                    failures.Add($"seed {seed}: doorway is not floor");
                if (layout.Walls.Any(layout.Walkable.Contains))
                    failures.Add($"seed {seed}: wall/floor overlap");
                if (plan.SpawnAnchors.Count == 0) failures.Add($"seed {seed}: no enemy spawn anchors");
            }

            _status = failures.Count == 0
                ? $"{ValidateSeedCount} seeds validated: all chambers connected, doorways open, spawns present."
                : $"{failures.Count} problems:\n{string.Join("\n", failures.Take(12))}";
        }

        static int Reachable(DungeonLayout layout, Vector2Int start)
        {
            var seen = new HashSet<Vector2Int> { start };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var offset in Cardinals)
                {
                    var next = cell + offset;
                    if (layout.Walkable.Contains(next) && seen.Add(next)) pending.Enqueue(next);
                }
            }
            return seen.Count;
        }

        void AddEntry()
        {
            Object created = _tab switch
            {
                0 => Make<RoomTemplate>("room", template => template.id = "room"),
                1 => Make<ChamberTheme>("theme", theme => theme.id = "theme"),
                _ => Make<PropDefinition>("prop", prop => prop.id = "prop")
            };
            AssetDatabase.AddObjectToAsset(created, _library);
            Append(created);
            Commit($"Added {created.name}.");
        }

        void DuplicateEntry(Object[] entries)
        {
            var source = entries[_selected];
            var copy = Instantiate(source);
            copy.name = source.name + " copy";
            AssetDatabase.AddObjectToAsset(copy, _library);
            Append(copy);
            Commit($"Duplicated {source.name}.");
        }

        void DeleteEntry(Object[] entries)
        {
            var target = entries[_selected];
            if (!EditorUtility.DisplayDialog("Delete", $"Delete '{target.name}' from the library?", "Delete", "Cancel"))
                return;
            switch (_tab)
            {
                case 0: _library.templates = Without(_library.templates, target); break;
                case 1: _library.themes = Without(_library.themes, target); break;
                default: _library.props = Without(_library.props, target); break;
            }
            if (_editors.Remove(target, out var editor) && editor != null) DestroyImmediate(editor);
            AssetDatabase.RemoveObjectFromAsset(target);
            DestroyImmediate(target, true);
            Commit("Deleted entry.");
        }

        void Append(Object created)
        {
            switch (_tab)
            {
                case 0: _library.templates = With(_library.templates, (RoomTemplate)created); break;
                case 1: _library.themes = With(_library.themes, (ChamberTheme)created); break;
                default: _library.props = With(_library.props, (PropDefinition)created); break;
            }
            _selected = CurrentList().Length - 1;
        }

        static T[] With<T>(T[] entries, T added) where T : Object =>
            (entries ?? System.Array.Empty<T>()).Append(added).ToArray();

        static T[] Without<T>(T[] entries, Object removed) where T : Object =>
            (entries ?? System.Array.Empty<T>()).Where(entry => entry != removed).ToArray();

        void Commit(string status)
        {
            EditorUtility.SetDirty(_library);
            AssetDatabase.SaveAssets();
            _plan = null;
            _status = status;
        }

        static T Make<T>(string name, System.Action<T> configure) where T : ScriptableObject
        {
            var created = CreateInstance<T>();
            created.name = name;
            configure(created);
            return created;
        }
    }
}
