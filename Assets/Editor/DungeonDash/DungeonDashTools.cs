using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using DungeonDash;

namespace DungeonDash.EditorTools
{
    /// <summary>
    /// One-shot generators that turn the imported 0x72 sprites into usable Unity
    /// assets: Tiles + Tile Palette + animated/rule tiles, and per-character
    /// animation clips + animator controllers + a Player prefab.
    /// Run via the "Tools/Dungeon Dash" menu (or -executeMethod in batch mode).
    /// Idempotent: re-running replaces previously generated assets.
    /// </summary>
    public static class DungeonDashTools
    {
        const string ArtTiles = "Assets/Art/Tiles";
        const string ArtChars = "Assets/Art/Characters";
        const string GenTiles = "Assets/Generated/Tiles";
        const string GenPalettes = "Assets/Generated/Palettes";
        const string GenAnims = "Assets/Generated/Animations";
        const string GenCtrls = "Assets/Generated/Animators";
        const string PrefabDir = "Assets/Prefabs";
        const string CatalogPath = "Assets/Resources/GameCatalog.asset";

        struct CharDef
        {
            public string id;
            public string prefix;
            public float speed;   // Godot px/s; divided by 100 for Unity units/s
            public bool hasHit;
            public CharDef(string id, string prefix, float speed, bool hasHit)
            { this.id = id; this.prefix = prefix; this.speed = speed; this.hasHit = hasHit; }
        }

        // Playable roster + defaults from the Godot PlayerData.gd CHARACTER_DEFS.
        static readonly CharDef[] Characters =
        {
            new CharDef("knight",  "knight_f",  500f, true),
            new CharDef("elf",     "elf_f",     540f, true),
            new CharDef("dwarf",   "dwarf_f",   470f, true),
            new CharDef("lizard",  "lizard_f",  560f, true),
            new CharDef("wizzard", "wizzard_f", 495f, true),
            new CharDef("doc",     "doc",       520f, false), // doc has no hit frame
        };

        [MenuItem("Tools/Dungeon Dash/Generate Everything", false, 0)]
        public static void GenerateEverything()
        {
            GenerateTiles();
            GenerateCharacters();
            GenerateGameCatalog();
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Dungeon Dash",
                    "Tiles + characters generated. Check the Console for details.", "OK");
        }

        [MenuItem("Tools/Dungeon Dash/3. Generate Game Catalog", false, 22)]
        public static void GenerateGameCatalog()
        {
            EnsureFolder("Assets/Resources");
            var catalog = ScriptableObject.CreateInstance<GameCatalog>();

            catalog.characters = Characters.Select(c => new GameCatalog.CharacterSkin
            {
                id = c.id,
                idle = LoadCharacterFrames(c, "idle"),
                run = LoadCharacterFrames(c, "run"),
                speed = c.speed / 100f
            }).ToArray();

            catalog.enemies = AssetDatabase.GetSubFolders("Assets/Art/Enemies")
                .OrderBy(x => x).Select(path =>
                {
                    var idle = LoadFrames(path, "_idle_anim_");
                    var run = LoadFrames(path, "_run_anim_");
                    return new GameCatalog.EnemySkin
                    {
                        id = Path.GetFileName(path),
                        idle = idle,
                        run = run.Length == 0 ? idle : run
                    };
                }).Where(x => x.idle.Length > 0).ToArray();

            catalog.weapons = LoadSprites("Assets/Art/Weapons")
                .Select(sprite => new GameCatalog.NamedSprite { id = sprite.name, sprite = sprite }).ToArray();

            var tiles = LoadSprites(ArtTiles);
            catalog.floors = tiles.Where(x => x.name.StartsWith("floor_")).ToArray();
            catalog.walls = tiles.Where(x => x.name.StartsWith("wall_") || x.name.StartsWith("column")).ToArray();
            catalog.coins = LoadSprites("Assets/Art/Items/coin");
            catalog.potions = LoadSprites("Assets/Art/Items/potion");
            catalog.chests = LoadSprites("Assets/Art/Items/chest");
            catalog.bombs = LoadSprites("Assets/Art/Items/bomb");
            catalog.heartFull = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/hearts/ui_heart_full.png");
            catalog.heartHalf = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/hearts/ui_heart_half.png");
            catalog.heartEmpty = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/hearts/ui_heart_empty.png");
            catalog.buttonUp = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/button/button_blue_up.png");
            catalog.buttonDown = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/button/button_blue_down.png");

            CreateOrReplace(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DungeonDash] Game catalog: {catalog.characters.Length} heroes, " +
                      $"{catalog.enemies.Length} enemies, {catalog.weapons.Length} artifacts.");
        }

        static Sprite[] LoadFrames(string folder, string marker) => LoadSprites(folder)
            .Where(x => x.name.Contains(marker)).OrderBy(x => x.name).ToArray();

        static Sprite[] LoadCharacterFrames(CharDef character, string animation) =>
            LoadSprites($"{ArtChars}/{character.id}")
                .Where(x => x.name.StartsWith($"{character.prefix}_{animation}_anim_"))
                .OrderBy(x => x.name).ToArray();

        static Sprite[] LoadSprites(string folder) => AssetDatabase.FindAssets("t:Sprite", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(x => x != null)
            .OrderBy(x => x.name)
            .ToArray();

        // ----------------------------------------------------------------- TILES

        [MenuItem("Tools/Dungeon Dash/1. Generate Tiles", false, 20)]
        public static void GenerateTiles()
        {
            EnsureFolder(GenTiles);

            // Collect every tile sprite by name.
            var spriteByName = new Dictionary<string, Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtTiles }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    spriteByName[Path.GetFileNameWithoutExtension(path)] = sprite;
            }

            // One Tile asset per sprite.
            var tilePaths = new List<string>();
            foreach (var kv in spriteByName.OrderBy(k => k.Key))
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = kv.Value;
                tile.colliderType = ColliderFor(kv.Key);
                var dst = $"{GenTiles}/{kv.Key}.asset";
                CreateOrReplace(tile, dst);
                tilePaths.Add(dst);
            }

            // Animated tiles for "<name>_anim_f#" groups (spikes, fountains, ...).
            int animCount = 0;
            var groups = new Dictionary<string, List<KeyValuePair<int, Sprite>>>();
            foreach (var kv in spriteByName)
            {
                var m = Regex.Match(kv.Key, @"^(.*)_anim_f(\d+)$");
                if (!m.Success) continue;
                var key = m.Groups[1].Value;
                if (!groups.TryGetValue(key, out var list)) { list = new(); groups[key] = list; }
                list.Add(new KeyValuePair<int, Sprite>(int.Parse(m.Groups[2].Value), kv.Value));
            }
            foreach (var grp in groups)
            {
                if (grp.Value.Count < 2) continue;
                var frames = grp.Value.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
                var at = ScriptableObject.CreateInstance<AnimatedTile>();
                at.m_AnimatedSprites = frames;
                at.m_MinSpeed = 4f;
                at.m_MaxSpeed = 4f;
                at.m_TileColliderType = Tile.ColliderType.None;
                CreateOrReplace(at, $"{GenTiles}/{grp.Key}_animated.asset");
                animCount++;
            }

            // Floor rule tile: randomizes among floor_1..8.
            var floor = Enumerable.Range(1, 8)
                .Select(i => spriteByName.TryGetValue($"floor_{i}", out var s) ? s : null)
                .Where(s => s != null).ToArray();
            if (floor.Length > 0)
            {
                var rt = ScriptableObject.CreateInstance<RuleTile>();
                rt.m_DefaultSprite = floor[0];
                rt.m_DefaultColliderType = Tile.ColliderType.None;
                rt.m_TilingRules ??= new List<RuleTile.TilingRule>();
                rt.m_TilingRules.Add(new RuleTile.TilingRule
                {
                    m_Sprites = floor,
                    m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random,
                    m_ColliderType = Tile.ColliderType.None,
                });
                CreateOrReplace(rt, $"{GenTiles}/Floor_Random.asset");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TryBuildPalette(tilePaths);
            Debug.Log($"[DungeonDash] Tiles: {tilePaths.Count} tiles, {animCount} animated tiles, " +
                      $"floor rule tile. Source: {ArtTiles}");
        }

        static void TryBuildPalette(List<string> tilePaths)
        {
            try
            {
                EnsureFolder(GenPalettes);
                var palettePath = $"{GenPalettes}/DungeonPalette.prefab";

                var root = new GameObject("DungeonPalette");
                var grid = root.AddComponent<Grid>();
                grid.cellSize = new Vector3(1f, 1f, 0f);

                var layer = new GameObject("Layer1");
                layer.transform.SetParent(root.transform);
                var tm = layer.AddComponent<Tilemap>();
                layer.AddComponent<TilemapRenderer>();

                const int cols = 16;
                for (int i = 0; i < tilePaths.Count; i++)
                {
                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePaths[i]);
                    if (tile != null)
                        tm.SetTile(new Vector3Int(i % cols, -(i / cols), 0), tile);
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, palettePath);
                UnityEngine.Object.DestroyImmediate(root);

                if (prefab != null)
                {
                    var gp = ScriptableObject.CreateInstance<GridPalette>();
                    gp.cellSizing = GridPalette.CellSizing.Automatic;
                    gp.name = "Palette Settings";
                    AssetDatabase.AddObjectToAsset(gp, palettePath);
                    AssetDatabase.ImportAsset(palettePath);
                }
                Debug.Log($"[DungeonDash] Tile Palette: {palettePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DungeonDash] Tile Palette creation skipped: " + e.Message);
            }
        }

        // ------------------------------------------------------------ CHARACTERS

        [MenuItem("Tools/Dungeon Dash/2. Generate Characters", false, 21)]
        public static void GenerateCharacters()
        {
            EnsureFolder(GenAnims);
            EnsureFolder(GenCtrls);
            EnsureFolder(PrefabDir);

            // 1. Animation clips for every character.
            foreach (var c in Characters)
            {
                MakeClip(c, "idle", 4, 10f, true);
                MakeClip(c, "run", 4, 10f, true);
                if (c.hasHit) MakeClip(c, "hit", 1, 5f, false);
            }

            // 2. Shared base controller (keyed on knight's clips).
            var baseIdle = LoadClip("knight", "idle");
            var baseRun = LoadClip("knight", "run");
            var baseHit = LoadClip("knight", "hit");
            var baseController = BuildBaseController(baseIdle, baseRun, baseHit);

            // 3. One AnimatorOverrideController per character.
            foreach (var c in Characters)
            {
                var idle = LoadClip(c.id, "idle");
                var run = LoadClip(c.id, "run");
                var hit = c.hasHit ? LoadClip(c.id, "hit") : idle; // doc falls back to idle

                var path = $"{GenCtrls}/{c.id}.overrideController";
                var ov = new AnimatorOverrideController(baseController) { name = c.id };
                CreateOrReplace(ov, path);
                ov[baseIdle] = idle;
                ov[baseRun] = run;
                ov[baseHit] = hit;
                EditorUtility.SetDirty(ov);
            }

            // 4. Player prefab, defaulting to the knight override.
            var knightOv = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                $"{GenCtrls}/knight.overrideController");
            BuildPlayerPrefab(knightOv, Characters[0]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DungeonDash] Characters: clips + PlayerBase controller + " +
                      Characters.Length + " override controllers + Player prefab.");
        }

        static AnimationClip MakeClip(CharDef c, string anim, int frameCount, float fps, bool loop)
        {
            EnsureFolder($"{GenAnims}/{c.id}");

            var frames = new List<Sprite>();
            for (int i = 0; i < frameCount; i++)
            {
                var path = $"{ArtChars}/{c.id}/{c.prefix}_{anim}_anim_f{i}.png";
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) frames.Add(sp);
            }
            if (frames.Count == 0)
                Debug.LogWarning($"[DungeonDash] No frames found for {c.id} {anim} (prefix {c.prefix}).");

            var clip = new AnimationClip { frameRate = fps };
            if (frames.Count > 0)
            {
                var binding = new EditorCurveBinding
                { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
                var keys = new ObjectReferenceKeyframe[frames.Count];
                for (int i = 0; i < frames.Count; i++)
                    keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            CreateOrReplace(clip, $"{GenAnims}/{c.id}/{c.id}_{anim}.anim");
            return clip;
        }

        static AnimatorController BuildBaseController(AnimationClip idle, AnimationClip run, AnimationClip hit)
        {
            var path = $"{GenCtrls}/PlayerBase.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sRun = sm.AddState("Run"); sRun.motion = run;
            var sHit = sm.AddState("Hit"); sHit.motion = hit;
            sm.defaultState = sIdle;

            var toRun = sIdle.AddTransition(sRun);
            toRun.hasExitTime = false; toRun.duration = 0f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var toIdle = sRun.AddTransition(sIdle);
            toIdle.hasExitTime = false; toIdle.duration = 0f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var anyHit = sm.AddAnyStateTransition(sHit);
            anyHit.hasExitTime = false; anyHit.duration = 0f; anyHit.canTransitionToSelf = false;
            anyHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            var hitDone = sHit.AddTransition(sIdle);
            hitDone.hasExitTime = true; hitDone.exitTime = 0.9f; hitDone.duration = 0f;

            EditorUtility.SetDirty(ac);
            return ac;
        }

        static void BuildPlayerPrefab(RuntimeAnimatorController controller, CharDef def)
        {
            var prefabPath = $"{PrefabDir}/Player.prefab";
            var go = new GameObject("Player");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{ArtChars}/{def.id}/{def.prefix}_idle_anim_f0.png");
            sr.sortingOrder = 0;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = go.AddComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(0.6f, 1.0f);
            col.offset = new Vector2(0f, -0.25f);

            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            go.AddComponent<PlayerController>();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[DungeonDash] Player prefab: {prefabPath} (default character: {def.id})");
        }

        // --------------------------------------------------------------- HELPERS

        static AnimationClip LoadClip(string id, string anim) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{GenAnims}/{id}/{id}_{anim}.anim");

        static Tile.ColliderType ColliderFor(string n)
        {
            bool solidWall = n.StartsWith("wall_")
                && !n.StartsWith("wall_banner")
                && !n.StartsWith("wall_fountain")
                && !n.StartsWith("wall_goo")
                && !n.StartsWith("wall_hole");
            if (solidWall) return Tile.ColliderType.Sprite;
            if (n == "column" || n == "column_wall" || n == "crate") return Tile.ColliderType.Sprite;
            return Tile.ColliderType.None;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void CreateOrReplace(UnityEngine.Object asset, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
