using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using static DungeonDash.UiTheme;

namespace DungeonDash
{
    public enum GameMode { StartScreen, CharacterSelect, HomeHub, InDungeon, Market, Inventory, Paused, GameOver }

    public sealed class DungeonGame : MonoBehaviour
    {
        const float ArenaHalfWidth = 11.5f;
        const float ArenaHalfHeight = 6.5f;

        GameCatalog _catalog;
        SaveData _save;
        LocalMarketService _localMarket;
        UgsMarketService _onlineMarket;
        bool _useOnlineMarket;
        readonly System.Random _random = new();
        readonly List<EnemyActor> _enemies = new();
        readonly List<Vector2> _enemySpawnPoints = new();
        readonly HashSet<Vector2Int> _walkable = new();
        PlayerController _player;
        Artifact _equipped;
        int _wave;
        int _enemyCursor;
        int _weaponCursor;
        int _kills;
        GameMode _mode = GameMode.StartScreen;
        GameMode _returnMode = GameMode.HomeHub;
        SaveData.CharacterSlot _activeSlot;
        bool _creatingSlot;
        bool _wavePending;
        bool _overlayOwnsPause;
        float _timeScaleBeforeOverlay = 1f;
        bool _transitioning;
        float _transitionAmount;
        string _transitionLabel = string.Empty;
        Vector2 _roomEntryPoint;
        InteractionZone _exitDoorZone;
        SpriteRenderer _exitDoorLeaf;
        BoxCollider2D _exitDoorBlocker;
        bool _roomExitUnlocked;
        int _volumeStep;
        GameCatalog.CharacterSkin _selectedCharacter;
        Artifact _selectedArtifact;
        Vector2 _inventoryScroll;
        Vector2 _marketScroll;
        string _toast = "Choose a hero";
        float _toastUntil;
        GUIStyle _titleStyle;
        GUIStyle _hudTitleStyle;
        GUIStyle _toastStyle;
        GUIStyle _labelStyle;
        GUIStyle _smallStyle;
        GUIStyle _buttonStyle;
        GUIStyle _dangerButtonStyle;
        GUIStyle _sectionStyle;
        GUIStyle _mutedStyle;
        GUIStyle _microStyle;
        GUIStyle _rightStyle;
        GUIStyle _centerStyle;
        GUIStyle _clickStyle;
        float _uiWidth;
        float _uiHeight;

        // The player can move in the hub and the dungeon; combat only runs in the dungeon.
        public bool WorldRunning => (_mode == GameMode.HomeHub || _mode == GameMode.InDungeon) && !_transitioning;
        public bool AcceptsGameplayInput => WorldRunning;
        public bool CombatActive => _mode == GameMode.InDungeon && !_transitioning;
        public Artifact EquippedArtifact => _equipped;
        public bool RoomExitUnlocked => _roomExitUnlocked;
        public int CurrentRoom => _wave;
        public bool TransitionActive => _transitioning;

        // Per-character save fields, routed through the active slot.
        int Coins { get => _activeSlot.coins; set => _activeSlot.coins = value; }
        List<Artifact> Inventory => _activeSlot.inventory;
        string EquippedId { get => _activeSlot.equippedId; set => _activeSlot.equippedId = value; }
        string ActiveCharacterId => _activeSlot.characterId;
        GameCatalog.CharacterSkin ActiveSkin =>
            _catalog.characters.FirstOrDefault(x => x.id == ActiveCharacterId) ?? _catalog.characters[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DungeonGame>() == null)
                new GameObject("Dungeon Dash").AddComponent<DungeonGame>();
        }

        void Awake()
        {
            _catalog = Resources.Load<GameCatalog>("GameCatalog");
            if (_catalog == null)
            {
                Debug.LogError("GameCatalog is missing. Run Tools/Dungeon Dash/Generate Game Catalog.");
                enabled = false;
                return;
            }

            _save = SaveData.Load();
            _localMarket = new LocalMarketService(_save.marketJson);
            _onlineMarket = new UgsMarketService();
            SeedMarket();
            ConfigureCamera();
            _volumeStep = GameAudio.MutedForAutomation ? 0 : GameAudio.SavedVolumeStep;
            GameAudio.ApplySavedVolume();
            // The world (hub or dungeon) is built once a character slot is chosen — nothing on boot.
        }

        void Start()
        {
            const string captureArgument = "--qa-screenshot=";
            const string characterArgument = "--qa-character=";
            const string viewArgument = "--qa-view=";
            string[] arguments = Environment.GetCommandLineArgs();
            string character = arguments
                .FirstOrDefault(x => x.StartsWith(characterArgument, StringComparison.Ordinal));
            if (character != null)
            {
                string id = character.Substring(characterArgument.Length);
                var skin = _catalog.characters.FirstOrDefault(x => x.id == id);
                if (skin != null) StartRun(skin);
            }
            string view = arguments
                .FirstOrDefault(x => x.StartsWith(viewArgument, StringComparison.Ordinal))?
                .Substring(viewArgument.Length);
            if (view == "slots")
            {
                _creatingSlot = false;
                _mode = GameMode.CharacterSelect;
            }
            else if (view == "picker")
            {
                _creatingSlot = true;
                _selectedCharacter = null;
                _mode = GameMode.CharacterSelect;
            }
            else if (view == "hub" && _activeSlot != null)
            {
                BuildHub();
            }
            else if (view == "game-over" && WorldRunning)
            {
                GameOver();
            }
            else if ((view == "door" || view == "transition" || view == "next-room") && CombatActive)
            {
                PrepareDoorQa();
                if (view != "door") BeginNextRoomTransition();
            }
            if (arguments.Contains("--qa-inventory") && WorldRunning)
            {
                SetInventoryOpen(true);
            }
            if (arguments.Contains("--qa-market") && WorldRunning)
            {
                OpenMarketOverlay();
                ClaimLocalMarketProceeds();
            }
            if (arguments.Contains("--qa-pause") && WorldRunning)
            {
                SetPauseOpen(true);
            }

            string argument = arguments
                .FirstOrDefault(x => x.StartsWith(captureArgument, StringComparison.Ordinal));
            if (argument != null)
            {
                Application.runInBackground = true;
                float delay = view == "door" ? 1.1f : 0.25f;
                StartCoroutine(CaptureQaFrame(argument.Substring(captureArgument.Length), view, delay));
            }
        }

        void PrepareDoorQa()
        {
            foreach (var enemy in _enemies.ToArray())
                if (enemy != null) enemy.TakeDamage(9999);
            if (_player == null || _exitDoorZone == null) return;
            _player.transform.position = _exitDoorZone.transform.position + Vector3.down * 0.75f;
            Camera.main?.GetComponent<PlayerCenteredCamera>()?.CenterNow();
        }

        IEnumerator CaptureQaFrame(string path, string view, float delay)
        {
            if (view == "transition")
            {
                while (_transitioning && _transitionAmount < 0.74f) yield return null;
            }
            else if (view == "next-room")
            {
                while (_transitioning) yield return null;
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[DungeonDash] QA screenshot: {path}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_mode == GameMode.StartScreen || _mode == GameMode.CharacterSelect || _mode == GameMode.GameOver) return;
            if (_mode == GameMode.Paused)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) SetPauseOpen(false);
                return;
            }
            if (_transitioning) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_mode == GameMode.Inventory || _mode == GameMode.Market) CloseOverlay();
                else SetPauseOpen(true);
                return;
            }
            if (keyboard.iKey.wasPressedThisFrame) ToggleInventory();
            if (keyboard.mKey.wasPressedThisFrame) ToggleMarket();
        }

        void ToggleInventory()
        {
            if (_mode == GameMode.Inventory) { SetInventoryOpen(false); return; }
            if (_mode == GameMode.Market) _mode = _returnMode; // close market back to the underlying world
            SetInventoryOpen(true);
        }

        void ToggleMarket()
        {
            if (_mode == GameMode.Market) { CloseMarket(); return; }
            if (_mode == GameMode.Inventory) SetInventoryOpen(false);
            OpenMarketOverlay();
        }

        void OpenMarketOverlay()
        {
            if (_mode != GameMode.HomeHub && _mode != GameMode.InDungeon) return;
            _returnMode = _mode;
            _mode = GameMode.Market;
            GameAudio.Play("ui_click", 0.5f);
            OpenMarket();
        }

        void CloseMarket()
        {
            if (_mode != GameMode.Market) return;
            _mode = _returnMode;
            GameAudio.Play("ui_click", 0.5f);
        }

        void CloseOverlay()
        {
            if (_mode == GameMode.Inventory) SetInventoryOpen(false);
            else if (_mode == GameMode.Market) CloseMarket();
        }

        void SetInventoryOpen(bool open)
        {
            if ((_mode == GameMode.Inventory) == open) return;
            if (open)
            {
                if (_mode != GameMode.HomeHub && _mode != GameMode.InDungeon) return;
                _returnMode = _mode;
                _mode = GameMode.Inventory;
                GameAudio.Play("ui_click", 0.5f);
                SelectDefaultArtifact();
                _timeScaleBeforeOverlay = Time.timeScale;
                Time.timeScale = 0f;
                _overlayOwnsPause = true;
            }
            else
            {
                _mode = _returnMode;
                GameAudio.Play("ui_click", 0.5f);
                ReleaseOverlayPause();
            }
        }

        void SetPauseOpen(bool open)
        {
            if ((_mode == GameMode.Paused) == open) return;
            if (open)
            {
                if (_mode != GameMode.HomeHub && _mode != GameMode.InDungeon) return;
                _returnMode = _mode;
                _mode = GameMode.Paused;
                _timeScaleBeforeOverlay = Time.timeScale;
                Time.timeScale = 0f;
                _overlayOwnsPause = true;
                GameAudio.Play("ui_click", 0.5f);
            }
            else
            {
                _mode = _returnMode;
                ReleaseOverlayPause();
                GameAudio.Play("ui_click", 0.5f);
            }
        }

        void ReleaseOverlayPause()
        {
            if (!_overlayOwnsPause) return;
            Time.timeScale = _timeScaleBeforeOverlay;
            _overlayOwnsPause = false;
        }

        void EnsureStartingInventory()
        {
            foreach (var artifact in Inventory.Where(x => !WeaponRules.IsArtifactWeapon(x.weaponId)))
            {
                artifact.weaponId = "weapon_bow";
                artifact.displayName = artifact.displayName.Replace("Arrow", "Bow");
            }
            if (Inventory.Count == 0)
            {
                var starter = ArtifactGenerator.Roll(_catalog.weapons[0].id, new System.Random(1));
                starter.displayName = "Starter " + starter.displayName;
                Inventory.Add(starter);
                EquippedId = starter.id;
            }
            _equipped = Inventory.FirstOrDefault(x => x.id == EquippedId) ?? Inventory[0];
            EquippedId = _equipped.id;
            Save();
        }

        void SeedMarket()
        {
            if (_localMarket.Listings.Count > 0) return;
            var artifactWeapons = _catalog.weapons.Where(x => WeaponRules.IsArtifactWeapon(x.id)).ToArray();
            for (int i = 0; i < Mathf.Min(8, artifactWeapons.Length); i++)
            {
                var artifact = ArtifactGenerator.Roll(artifactWeapons[i].id, _random);
                _localMarket.AddNpcListing(artifact, artifact.Price);
            }
            Save();
        }

        void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f);
        }

        static readonly Vector2Int[] CardinalOffsets =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        void BuildArena()
        {
            var root = new GameObject("Arena").transform;
            var layout = DungeonGenerator.GenerateChunk(_random, _wave + 1);
            _roomEntryPoint = layout.EntryPoint;
            _walkable.Clear();
            _walkable.UnionWith(layout.Walkable);
            foreach (var cell in layout.Walkable)
            {
                float age = layout.FloorAge[cell];
                Sprite sprite;
                Color tint;
                if (layout.Corridors.Contains(cell) && _catalog.paths.Length > 0)
                {
                    sprite = DungeonTileSelector.SelectPath(_catalog.paths, cell);
                    tint = Color.white; // preserve authored path color
                }
                else if (_catalog.grass.Length > 0 &&
                    layout.Rooms.Any(room => DungeonTileSelector.IsGrassRoom(room) && room.Bounds.Contains(cell)))
                {
                    sprite = DungeonTileSelector.SelectGrass(_catalog.grass, cell);
                    tint = Color.white; // preserve authored grass color
                }
                else
                {
                    sprite = DungeonTileSelector.SelectFloor(_catalog.floors, age, _random);
                    tint = Color.white;
                }
                var floor = CreateSprite($"Floor {cell.x},{cell.y} age {age:0.00}", sprite,
                    new Vector2(cell.x, cell.y), -20, root);
                floor.GetComponent<SpriteRenderer>().color = tint;
            }

            var roomBounds = layout.Rooms.Select(room => room.Bounds).ToList();
            var decorations = WallClassifier.PlanDecorations(layout.Walkable.Contains, layout.GridBounds, roomBounds);
            var wallDistances = ComputeWallDistances(layout);

            foreach (var cell in layout.Walls)
            {
                bool hasDecoration = decorations.TryGetValue(cell, out var decoration);
                string spriteName = hasDecoration ? decoration.SpriteName : DungeonTileSelector.WallSpriteName(layout, cell);
                string prefix = !hasDecoration ? "Wall" : decoration.Kind == WallDecorationKind.Banner ? "Banner" : "Decor";
                int dist = hasDecoration ? 1 : wallDistances.GetValueOrDefault(cell, int.MaxValue);
                float brightness = Mathf.Lerp(1f, 0.46f, Mathf.InverseLerp(1f, 4f, dist));
                var sprite = DungeonTileSelector.FindByName(_catalog.walls, spriteName);
                var position = new Vector2(cell.x, cell.y);
                bool hasCollider = HasWalkableNeighbor(layout, cell);

                GameObject wallGo;
                if (spriteName == "column_wall")
                {
                    wallGo = CreateColumnWall(position, sprite, $"{prefix} {spriteName}", root, brightness, hasCollider);
                }
                else
                {
                    wallGo = CreateWall(position, sprite, $"{prefix} {spriteName}", root, brightness);
                    if (hasCollider) wallGo.AddComponent<BoxCollider2D>();
                }

                if (hasDecoration && decoration.AnimFrameNames != null &&
                    (decoration.Kind == WallDecorationKind.FountainMid || decoration.Kind == WallDecorationKind.FountainBasin))
                {
                    var frames = decoration.AnimFrameNames
                        .Select(name => DungeonTileSelector.FindByName(_catalog.walls, name)).ToArray();
                    var renderer = wallGo.GetComponent<SpriteRenderer>();
                    renderer.sprite = frames[0];
                    var loop = wallGo.AddComponent<SpriteFrameLoop>();
                    loop.Frames = frames;
                    loop.Fps = 5f;
                }
            }

            BuildExitDoor(root, layout);

            _enemySpawnPoints.Clear();
            _enemySpawnPoints.AddRange(layout.Walkable
                .Where(cell => cell.sqrMagnitude > 16 && !layout.Corridors.Contains(cell))
                .OrderByDescending(cell => cell.sqrMagnitude)
                .Select(cell => new Vector2(cell.x, cell.y)));
        }

        static Dictionary<Vector2Int, int> ComputeWallDistances(DungeonLayout layout)
        {
            var distances = new Dictionary<Vector2Int, int>();
            var frontier = new Queue<Vector2Int>();
            foreach (var wall in layout.Walls)
            {
                if (!CardinalOffsets.Any(offset => layout.Walkable.Contains(wall + offset))) continue;
                distances[wall] = 1;
                frontier.Enqueue(wall);
            }

            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                int next = distances[cell] + 1;
                foreach (var offset in CardinalOffsets)
                {
                    var neighbor = cell + offset;
                    if (!layout.Walls.Contains(neighbor) || distances.ContainsKey(neighbor)) continue;
                    distances[neighbor] = next;
                    frontier.Enqueue(neighbor);
                }
            }

            return distances;
        }

        static bool HasWalkableNeighbor(DungeonLayout layout, Vector2Int cell) =>
            CardinalOffsets.Any(offset => layout.Walkable.Contains(cell + offset));

        static Color WallTint(float brightness) => new(brightness, brightness, brightness);

        GameObject CreateWall(Vector2 position, Sprite sprite, string name, Transform parent, float brightness)
        {
            var go = CreateSprite(name, sprite, position, YSort.Order(position.y, 0), parent);
            go.GetComponent<SpriteRenderer>().color = WallTint(brightness);
            return go;
        }

        GameObject CreateColumnWall(Vector2 position, Sprite sprite, string name, Transform parent, float brightness, bool hasCollider)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.SetParent(parent);
            if (hasCollider)
            {
                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one;
            }
            var child = CreateSprite(name, sprite, position + new Vector2(0f, -1f),
                YSort.Order(position.y - 2f, 0), go.transform);
            child.GetComponent<SpriteRenderer>().color = WallTint(brightness);
            return go;
        }

        void BuildExitDoor(Transform parent, DungeonLayout layout)
        {
            var center = layout.ExitDoorPosition;
            var frameLeft = DungeonTileSelector.FindByName(_catalog.walls, "doors_frame_left");
            var frameTop = DungeonTileSelector.FindByName(_catalog.walls, "doors_frame_top");
            var frameRight = DungeonTileSelector.FindByName(_catalog.walls, "doors_frame_right");
            var closed = DungeonTileSelector.FindByName(_catalog.walls, "doors_leaf_closed");

            int order = YSort.Order(center.y - 1f, 0);
            CreateSprite("Exit Door Frame Left", frameLeft, center + Vector2.left * 1.5f, order, parent);
            CreateSprite("Exit Door Frame Top", frameTop, center + Vector2.up * 1.5f, order, parent);
            CreateSprite("Exit Door Frame Right", frameRight, center + Vector2.right * 1.5f, order, parent);
            var leaf = CreateSprite("Exit Door Leaf", closed, center, order + 1, parent);
            _exitDoorLeaf = leaf.GetComponent<SpriteRenderer>();
            _exitDoorBlocker = leaf.AddComponent<BoxCollider2D>();
            _exitDoorBlocker.offset = new Vector2(0f, -0.55f);
            _exitDoorBlocker.size = new Vector2(1.8f, 0.7f);

            var interaction = new GameObject("Exit Door");
            interaction.transform.position = center + Vector2.down;
            interaction.transform.SetParent(parent);
            _exitDoorZone = interaction.AddComponent<InteractionZone>();
            _exitDoorZone.Setup(this, "NEXT CHAMBER", BeginNextRoomTransition);
            _exitDoorZone.enabled = false;
            _roomExitUnlocked = false;
        }

        static GameObject CreateSprite(string name, Sprite sprite, Vector2 position, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (parent != null) go.transform.SetParent(parent);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go;
        }

        // QA / test entry point: ensure a slot exists for the hero, then drop straight into a dungeon.
        void StartRun(GameCatalog.CharacterSkin skin)
        {
            EnsureSlotForCharacter(skin.id);
            EnterDungeon();
        }

        void EnsureSlotForCharacter(string characterId)
        {
            int index = _save.slots.FindIndex(s => s.characterId == characterId);
            if (index < 0)
            {
                if (_save.slots.Count < SaveData.MaxSlots)
                {
                    _save.CreateSlot(characterId);
                    index = _save.slots.Count - 1;
                }
                else
                {
                    index = Mathf.Clamp(_save.activeSlot, 0, _save.slots.Count - 1);
                    _save.slots[index].characterId = characterId;
                }
            }
            ActivateSlot(index);
        }

        void ActivateSlot(int index)
        {
            _save.activeSlot = index;
            _activeSlot = _save.slots[index];
            EnsureStartingInventory();
            Save();
        }

        void SpawnPlayer(GameCatalog.CharacterSkin skin, Vector2 position)
        {
            if (_player != null) Destroy(_player.gameObject);
            var go = CreateSprite("Player", skin.idle[0], position, 10);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.62f, 0.9f);
            collider.offset = new Vector2(0f, -0.25f);
            _player = go.AddComponent<PlayerController>();
            _player.Setup(this, skin);
        }

        void EnterDungeon()
        {
            ClearActors();
            DestroyWorldRoots();
            _wave = 0;
            _kills = 0;
            BuildArena();
            SpawnPlayer(ActiveSkin, _roomEntryPoint);
            _mode = GameMode.InDungeon;
            Save();
            SpawnWave();
            Toast("Clear the chamber and unlock the north door");
        }

        void BeginDungeonTransition()
        {
            if (_transitioning || _mode != GameMode.HomeHub) return;
            StartCoroutine(EnterDungeonThroughDoor());
        }

        IEnumerator EnterDungeonThroughDoor()
        {
            _transitioning = true;
            _transitionLabel = "DUNGEON  ·  CHAMBER 01";
            yield return AnimateTransition(0f, 1f, 0.32f);
            EnterDungeon();
            yield return null;
            Camera.main?.GetComponent<PlayerCenteredCamera>()?.CenterNow();
            yield return AnimateTransition(1f, 0f, 0.32f);
            _transitionAmount = 0f;
            _transitioning = false;
        }

        void ClearActors()
        {
            ClearRoomActors();
            if (_player != null) Destroy(_player.gameObject);
            _player = null;
        }

        void ClearRoomActors()
        {
            foreach (var enemy in _enemies.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
            _enemies.Clear();
            foreach (var pickup in FindObjectsByType<PickupActor>(FindObjectsSortMode.None)) Destroy(pickup.gameObject);
            foreach (var projectile in FindObjectsByType<ProjectileActor>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            foreach (var swing in FindObjectsByType<MeleeSwingActor>(FindObjectsSortMode.None)) Destroy(swing.gameObject);
            foreach (var corpse in FindObjectsByType<CorpseFade>(FindObjectsSortMode.None)) Destroy(corpse.gameObject);
        }

        void DestroyWorldRoots()
        {
            var arena = GameObject.Find("Arena");
            if (arena != null) Destroy(arena);
            var hub = GameObject.Find("Hub");
            if (hub != null) Destroy(hub);
            _exitDoorZone = null;
            _exitDoorLeaf = null;
            _exitDoorBlocker = null;
            _roomExitUnlocked = false;
        }

        // A small hand-built room the player returns to between dungeon runs. Two portals sit inside.
        void BuildHub()
        {
            ClearActors();
            DestroyWorldRoots();

            var root = new GameObject("Hub").transform;
            _walkable.Clear();
            const int halfWidth = 12;
            const int halfHeight = 7;
            var floorSprite = _catalog.floors[0];
            for (int x = -halfWidth; x <= halfWidth; x++)
            for (int y = -halfHeight; y <= halfHeight; y++)
            {
                bool border = x == -halfWidth || x == halfWidth || y == -halfHeight || y == halfHeight;
                var position = new Vector2(x, y);
                if (border)
                {
                    string wallName;
                    if (y == halfHeight)
                        wallName = x == -halfWidth ? "wall_top_left" : x == halfWidth ? "wall_top_right" : "wall_top_mid";
                    else if (y == -halfHeight)
                        wallName = x == -halfWidth ? "wall_edge_bottom_left" : x == halfWidth ? "wall_edge_bottom_right" : "edge_down";
                    else
                        wallName = x == -halfWidth ? "wall_left" : "wall_right";
                    var wallSprite = DungeonTileSelector.FindByName(_catalog.walls, wallName);
                    var wall = CreateWall(position, wallSprite, $"Hub Wall {wallName} {x},{y}", root, 1f);
                    wall.AddComponent<BoxCollider2D>();
                }
                else
                {
                    var floor = CreateSprite($"Hub Floor {x},{y}", floorSprite, position, -20, root);
                    floor.GetComponent<SpriteRenderer>().color = Color.white;
                    _walkable.Add(new Vector2Int(x, y));
                }
            }

            CreateHubZone(root, "MARKET", new Vector2(-5f, 3f), OpenMarketFromHub);
            CreateHubDungeonDoor(root, new Vector2(5f, halfHeight - 2f));

            SpawnPlayer(ActiveSkin, Vector2.zero);
            _wave = 0;
            _kills = 0;
            _mode = GameMode.HomeHub;
            Toast("Home — approach a destination and press E");
        }

        void CreateHubZone(Transform parent, string label, Vector2 position, System.Action onInteract)
        {
            var marker = CreateSprite($"Zone {label}", _catalog.chests.Length > 0 ? _catalog.chests[0] : null, position, 6, parent);
            marker.transform.localScale = Vector3.one * 1.1f;
            marker.AddComponent<InteractionZone>().Setup(this, label, onInteract);
        }

        void CreateHubDungeonDoor(Transform parent, Vector2 center)
        {
            var frameLeft = DungeonTileSelector.FindByName(_catalog.walls, "doors_frame_left");
            var frameTop = DungeonTileSelector.FindByName(_catalog.walls, "doors_frame_top");
            var frameRight = DungeonTileSelector.FindByName(_catalog.walls, "doors_frame_right");
            var closed = DungeonTileSelector.FindByName(_catalog.walls, "doors_leaf_closed");
            int order = YSort.Order(center.y - 1f, 0);
            CreateSprite("Hub Door Frame Left", frameLeft, center + Vector2.left * 1.5f, order, parent);
            CreateSprite("Hub Door Frame Top", frameTop, center + Vector2.up * 1.5f, order, parent);
            CreateSprite("Hub Door Frame Right", frameRight, center + Vector2.right * 1.5f, order, parent);
            CreateSprite("Hub Door Leaf", closed, center, order + 1, parent);

            var interaction = new GameObject("Zone DUNGEON");
            interaction.transform.position = center + Vector2.down * 1.7f;
            interaction.transform.SetParent(parent);
            interaction.AddComponent<InteractionZone>().Setup(this, "DUNGEON", BeginDungeonTransition);
        }

        void OpenMarketFromHub()
        {
            _returnMode = GameMode.HomeHub;
            _mode = GameMode.Market;
            OpenMarket();
        }

        void ReturnToHub() => BuildHub();

        void SpawnWave()
        {
            _wavePending = false;
            _wave++;
            int count = Mathf.Min(4 + _wave * 2, 18);
            for (int i = 0; i < count; i++)
            {
                var skin = _catalog.enemies[_enemyCursor++ % _catalog.enemies.Length];
                var position = _enemySpawnPoints[(i * 7 + _wave * 3) % _enemySpawnPoints.Count];
                var go = CreateSprite(skin.id, skin.idle[0], position, 8);
                go.AddComponent<EnemyNavigator>().Setup(this, _walkable,
                    1.5f + Mathf.Min(_wave * 0.04f, 0.7f));
                var enemy = go.AddComponent<EnemyActor>();
                enemy.Setup(this, skin, 11 + _wave * 4);
                _enemies.Add(enemy);
            }
            Toast($"Wave {_wave}");
            GameAudio.Play("wave_start", 0.7f);
        }

        public void EnemyDied(EnemyActor enemy)
        {
            _enemies.Remove(enemy);
            _kills++;
            AddCoins(1 + _wave / 3);
            if (_kills % 3 == 0) DropArtifact(enemy.transform.position);
            else if (_random.NextDouble() < 0.14d) DropPickup(enemy.transform.position, PickupKind.Potion);
            else if (_random.NextDouble() < 0.12d) DropPickup(enemy.transform.position, PickupKind.Bomb);
            else if (_random.NextDouble() < 0.35d) DropPickup(enemy.transform.position, PickupKind.Coin);

            if (_enemies.Count == 0 && !_wavePending)
            {
                _wavePending = true;
                int sold = _localMarket.SimulateSales(_random);
                DropPickup(Vector2.zero, PickupKind.Chest);
                UnlockExitDoor();
                if (sold > 0) Toast($"Door open — {sold} market listing sold!");
                Save();
            }
        }

        void UnlockExitDoor()
        {
            _roomExitUnlocked = true;
            if (_exitDoorLeaf != null)
                _exitDoorLeaf.sprite = DungeonTileSelector.FindByName(_catalog.walls, "doors_leaf_open");
            if (_exitDoorBlocker != null) _exitDoorBlocker.enabled = false;
            if (_exitDoorZone != null) _exitDoorZone.enabled = true;
            Toast("CHAMBER CLEAR  ·  NORTH DOOR UNLOCKED");
            GameAudio.Play("chest_open", 0.75f);
        }

        void BeginNextRoomTransition()
        {
            if (!_roomExitUnlocked || _transitioning || _mode != GameMode.InDungeon) return;
            StartCoroutine(LoadNextRoom());
        }

        IEnumerator LoadNextRoom()
        {
            _transitioning = true;
            _transitionLabel = $"CHAMBER {_wave + 1:00}";
            yield return AnimateTransition(0f, 1f, 0.32f);

            ClearRoomActors();
            var arena = GameObject.Find("Arena");
            if (arena != null) Destroy(arena);
            yield return null;

            BuildArena();
            if (_player != null)
            {
                _player.transform.position = _roomEntryPoint;
                var body = _player.GetComponent<Rigidbody2D>();
                if (body != null) body.linearVelocity = Vector2.zero;
            }
            Camera.main?.GetComponent<PlayerCenteredCamera>()?.CenterNow();
            SpawnWave();
            Save();

            yield return AnimateTransition(1f, 0f, 0.32f);
            _transitionAmount = 0f;
            _transitioning = false;
            Toast($"CHAMBER {_wave:00}  ·  FIND THE EXIT");
        }

        IEnumerator AnimateTransition(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Clamp(Time.unscaledDeltaTime, 1f / 120f, 1f / 30f);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _transitionAmount = Mathf.Lerp(from, to, t);
                yield return null;
            }
            _transitionAmount = to;
        }

        void DropArtifact(Vector2 position)
        {
            var artifactWeapons = _catalog.weapons.Where(x => WeaponRules.IsArtifactWeapon(x.id)).ToArray();
            string weaponId = artifactWeapons[_weaponCursor++ % artifactWeapons.Length].id;
            var artifact = ArtifactGenerator.Roll(weaponId, _random);
            var go = CreateSprite(artifact.displayName, WeaponSprite(weaponId), position, 7);
            go.AddComponent<PickupActor>().Setup(this, PickupKind.Artifact, artifact);
        }

        void DropPickup(Vector2 position, PickupKind kind)
        {
            Sprite[] sprites = kind switch
            {
                PickupKind.Coin => _catalog.coins,
                PickupKind.Potion => _catalog.potions,
                PickupKind.Bomb => _catalog.bombs,
                _ => _catalog.chests
            };
            Sprite sprite = kind switch
            {
                PickupKind.Potion => sprites.FirstOrDefault(x => x.name == "flask_big_red") ?? sprites[0],
                PickupKind.Chest => sprites.FirstOrDefault(x => x.name == "chest_full_open_anim_f0") ?? sprites[0],
                _ => sprites[0]
            };
            Sprite[] animation = kind is PickupKind.Coin or PickupKind.Bomb ? sprites : null;
            var go = CreateSprite(kind.ToString(), sprite, position, 7);
            go.AddComponent<PickupActor>().Setup(this, kind, null, animation);
        }

        public void Collect(PickupKind kind, Artifact artifact)
        {
            switch (kind)
            {
                case PickupKind.Coin:
                    AddCoins(3);
                    Toast("+3 coins");
                    break;
                case PickupKind.Potion:
                    _player.Heal(3);
                    Toast("Restored 3 hearts");
                    break;
                case PickupKind.Chest:
                    AddCoins(8 + _wave);
                    GameAudio.Play("chest_open", 0.6f);
                    Toast($"Chest: +{8 + _wave} coins");
                    break;
                case PickupKind.Bomb:
                    int hit = 0;
                    foreach (var enemy in _enemies.ToArray())
                    {
                        if (enemy == null || ((Vector2)enemy.transform.position - PlayerPosition).sqrMagnitude > 16f) continue;
                        enemy.TakeDamage(12 + _wave * 2);
                        hit++;
                    }
                    PixelBurst.BombBurst(PlayerPosition);
                    GameAudio.Play("bomb_explode", 1f);
                    GameFeel.Shake(0.6f);
                    GameFeel.HitStop(0.09f);
                    Toast($"Bomb blast hit {hit} enemies");
                    break;
                case PickupKind.Artifact:
                    Inventory.Add(artifact);
                    GameAudio.Play("artifact_drop", 0.6f);
                    Toast($"Found {artifact.rarity} {artifact.displayName} ({artifact.quality})");
                    break;
            }
            Save();
        }

        public void UseWeapon(Vector2 position, Vector2 direction, int damage, string weaponId,
            Sprite equippedSprite, bool critical)
        {
            if (WeaponRules.IsRanged(weaponId))
            {
                Fire(position, direction, damage, WeaponSprite(WeaponRules.ProjectileSpriteId(weaponId)), critical);
                return;
            }

            MeleeStrike(position, direction, damage, equippedSprite, critical);
        }

        public void Fire(Vector2 position, Vector2 direction, int damage, Sprite sprite, bool critical)
        {
            var go = CreateSprite(critical ? "Critical shot" : "Shot", sprite, position, 12);
            go.transform.localScale = Vector3.one * (critical ? 0.7f : 0.55f);
            go.AddComponent<ProjectileActor>().Setup(this, direction, damage, critical);
            GameAudio.Play("bow_shot", 0.6f);
        }

        void MeleeStrike(Vector2 position, Vector2 direction, int damage, Sprite sprite, bool critical)
        {
            var visual = CreateSprite(critical ? "Critical melee swing" : "Melee swing", sprite, position, 13);
            visual.AddComponent<MeleeSwingActor>().Setup(direction, critical);
            GameAudio.Play("swing_whoosh", 0.6f);

            EnemyActor target = null;
            float closest = 1.45f * 1.45f;
            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;
                Vector2 offset = (Vector2)enemy.transform.position - position;
                float distance = offset.sqrMagnitude;
                if (distance >= closest || Vector2.Dot(direction, offset.normalized) < 0.2f) continue;
                closest = distance;
                target = enemy;
            }
            target?.TakeDamage(damage, position - direction * 0.75f, critical);
        }

        public EnemyActor ProjectileTarget(Vector2 position)
        {
            EnemyActor closest = null;
            float best = 0.45f * 0.45f;
            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;
                float distance = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                closest = enemy;
            }
            return closest;
        }

        public Vector2 PlayerPosition => _player == null ? Vector2.zero : (Vector2)_player.transform.position;
        public bool PlayerAlive => _player != null && _player.Health > 0;
        public void HurtPlayer(int amount) => _player?.TakeDamage(amount);
        public void HurtPlayer(int amount, Vector2 sourcePosition) => _player?.TakeDamage(amount, sourcePosition);

        public Sprite WeaponSprite(string id) =>
            _catalog.weapons.FirstOrDefault(x => x.id == id)?.sprite;

        public void GameOver()
        {
            _mode = GameMode.GameOver;
            GameAudio.Play("game_over", 0.8f);
            Save();
        }

        // Ends the current run and returns the player to the home hub.
        void Restart()
        {
            ReleaseOverlayPause();
            ReturnToHub();
        }

        void Save()
        {
            _save.marketJson = _localMarket.Serialize();
            _save.Save();
        }

        void OnDisable() => ReleaseOverlayPause();

        void OnApplicationQuit() => Save();

        void Toast(string message)
        {
            _toast = message;
            _toastUntil = Time.time + 3.5f;
        }

        void AddCoins(int amount)
        {
            Coins += amount;
            if (_save.marketAccountInitialized) _save.marketPendingCoinDelta += amount;
        }

        void ClaimLocalMarketProceeds()
        {
            int coins = _localMarket.ClaimProceeds();
            if (coins <= 0) return;
            AddCoins(coins);
            Toast($"Collected {coins} market coins");
            Save();
        }

        async void OpenMarket()
        {
            if (await EnsureOnlineMarket())
            {
                try
                {
                    var response = await _onlineMarket.ClaimAsync();
                    SyncOnlineBalance();
                    if (response.message.StartsWith("Claimed", StringComparison.Ordinal)) Toast(response.message);
                }
                catch (Exception exception)
                {
                    Toast("Online market: " + exception.GetBaseException().Message);
                }
            }
            else if (!_onlineMarket.IsOnline)
            {
                _useOnlineMarket = false;
                ClaimLocalMarketProceeds();
            }
        }

        async System.Threading.Tasks.Task<bool> EnsureOnlineMarket()
        {
            try
            {
                bool connected;
                if (_onlineMarket.IsOnline)
                {
                    if (_save.marketPendingCoinDelta != 0)
                        await _onlineMarket.SyncCoinsAsync(_save.marketPendingCoinDelta);
                    connected = true;
                }
                else
                {
                    int initialBalance = Mathf.Max(0, Coins - _save.marketPendingCoinDelta);
                    connected = await _onlineMarket.ConnectAsync(initialBalance, _save.marketPendingCoinDelta);
                }

                if (!connected) return false;
                _useOnlineMarket = true;
                _save.marketAccountInitialized = true;
                _save.marketPendingCoinDelta = 0;
                SyncOnlineBalance();
                return true;
            }
            catch (Exception exception)
            {
                Toast("Online sync failed: " + exception.GetBaseException().Message);
                return false;
            }
        }

        void SyncOnlineBalance()
        {
            Coins = _onlineMarket.Balance;
            Save();
        }

        void SelectDefaultArtifact()
        {
            if (_selectedArtifact != null && Inventory.Contains(_selectedArtifact)) return;
            _selectedArtifact = _equipped ?? Inventory.OrderByDescending(x => x.quality).FirstOrDefault();
        }

        void SelectDefaultCharacter()
        {
            if (_selectedCharacter != null && _catalog.characters.Contains(_selectedCharacter)) return;
            _selectedCharacter = _catalog.characters[0];
        }

        void InitStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _titleStyle.font = TitleFont;
            _titleStyle.normal.textColor = Cream;
            _hudTitleStyle = new GUIStyle(_titleStyle) { fontSize = 18 };
            _toastStyle = new GUIStyle(_titleStyle) { fontSize = 21, fontStyle = FontStyle.Normal };
            _toastStyle.font = BodyFont;
            _toastStyle.normal.textColor = Cream;
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _labelStyle.font = BodyFont;
            _labelStyle.normal.textColor = Cream;
            _smallStyle = new GUIStyle(_labelStyle) { fontSize = 16 };
            _smallStyle.normal.textColor = new Color(0.82f, 0.81f, 0.74f);
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 42, alignment = TextAnchor.MiddleCenter };
            _buttonStyle.font = TitleFont;
            _buttonStyle.normal.background = _catalog.buttonUp.texture;
            _buttonStyle.active.background = _catalog.buttonDown.texture;
            _buttonStyle.normal.textColor = Cream;
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.textColor = Color.white;
            _dangerButtonStyle = new GUIStyle(_buttonStyle);
            _dangerButtonStyle.normal.background = _catalog.dangerButtonUp.texture;
            _dangerButtonStyle.active.background = _catalog.dangerButtonDown.texture;
            _sectionStyle = new GUIStyle(_labelStyle) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _sectionStyle.normal.textColor = AccentGold;
            _mutedStyle = new GUIStyle(_smallStyle) { fontSize = 13 };
            _mutedStyle.normal.textColor = new Color(0.56f, 0.58f, 0.59f);
            _microStyle = new GUIStyle(_mutedStyle) { fontSize = 11, fontStyle = FontStyle.Bold };
            _rightStyle = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleRight };
            _centerStyle = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleCenter };
            _clickStyle = new GUIStyle();
        }

        void OnGUI()
        {
            InitStyles();
            float scale = Mathf.Min(1f, Screen.width / 1180f, Screen.height / 700f);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _uiWidth = Screen.width / scale;
            _uiHeight = Screen.height / scale;

            switch (_mode)
            {
                case GameMode.StartScreen:
                    DrawStartScreen();
                    GUI.matrix = previousMatrix;
                    return;
                case GameMode.CharacterSelect:
                    DrawCharacterSelect();
                    GUI.matrix = previousMatrix;
                    return;
                case GameMode.Inventory:
                    DrawInventory();
                    GUI.matrix = previousMatrix;
                    return;
                case GameMode.Market:
                    DrawMarket();
                    GUI.matrix = previousMatrix;
                    return;
                case GameMode.Paused:
                    DrawPauseMenu();
                    GUI.matrix = previousMatrix;
                    return;
            }

            FloatingDamageNumbers.Draw(Camera.main, scale);

            var portraitRect = new Rect(18, 18, 72, 72);
            SubPanel(portraitRect);
            if (_player != null)
                DrawSprite(new Rect(portraitRect.x + 8, portraitRect.y + 5, 56, 61), ActiveSkin.idle[0]);

            var statusRect = new Rect(82, 18, 372, 72);
            SubPanel(statusRect);
            DrawRect(new Rect(statusRect.x, statusRect.y, 5, statusRect.height), AccentRed);
            if (_player != null) DrawHearts(new Rect(statusRect.x + 16, statusRect.y + 8, 30, 30));
            if (_catalog.coins.Length > 0)
                DrawSprite(new Rect(statusRect.x + 16, statusRect.y + 42, 20, 20), _catalog.coins[0]);
            GUI.Label(new Rect(statusRect.x + 42, statusRect.y + 40, 82, 24), Coins.ToString(), _smallStyle);
            if (_equipped != null)
            {
                DrawSprite(new Rect(statusRect.x + 126, statusRect.y + 34, 30, 30), WeaponSprite(_equipped.weaponId));
                GUI.Label(new Rect(statusRect.x + 164, statusRect.y + 38, statusRect.width - 176, 24),
                    $"{_equipped.displayName}  ·  Q{_equipped.quality}", _smallStyle);
            }

            var chamberRect = new Rect(_uiWidth / 2f - 118f, 18f, 236f, 56f);
            DrawRect(chamberRect, AccentRed);
            DrawBorder(chamberRect, new Color(0.25f, 0.03f, 0.045f), 3f);
            GUI.Label(new Rect(chamberRect.x, chamberRect.y + 3, chamberRect.width, 28),
                _mode == GameMode.HomeHub ? "HOME BASE" : $"CHAMBER {_wave:00}", _hudTitleStyle);
            GUI.Label(new Rect(chamberRect.x, chamberRect.y + 31, chamberRect.width, 18),
                _mode == GameMode.HomeHub ? "SAFE ROOM" : $"{_kills} DEFEATED", _microStyle);

            float menuX = _uiWidth - 316f;
            if (Button(new Rect(menuX, 20, 142, 46), "VAULT [I]", _buttonStyle))
            {
                SetInventoryOpen(true);
            }
            if (Button(new Rect(menuX + 152, 20, 142, 46), "MARKET [M]", _buttonStyle))
            {
                OpenMarketOverlay();
            }

            if (Time.time < _toastUntil)
            {
                var toastRect = new Rect(_uiWidth / 2f - 280, 82, 560, 36);
                SubPanel(toastRect);
                GUI.Label(toastRect, _toast, _toastStyle);
            }
            var helpRect = new Rect(_uiWidth / 2f - 355f, _uiHeight - 42f, 710f, 30f);
            SubPanel(helpRect);
            GUI.Label(helpRect, "WASD MOVE  ·  LMB ATTACK  ·  RMB DASH  ·  ESC PAUSE", _centerStyle);
            if (_mode == GameMode.GameOver) DrawGameOver();
            DrawSceneTransition();
            GUI.matrix = previousMatrix;
        }

        void DrawHearts(Rect start)
        {
            for (int i = 0; i < _player.MaxHealth / 2; i++)
            {
                int health = _player.Health - i * 2;
                var sprite = health >= 2 ? _catalog.heartFull : health == 1 ? _catalog.heartHalf : _catalog.heartEmpty;
                DrawSprite(new Rect(start.x + i * 28, start.y, 24, 24), sprite);
            }
        }

        void DrawPauseMenu()
        {
            DrawBackdrop();
            var rect = Centered(660, 410);
            Panel(rect);

            var portrait = new Rect(rect.x + 24, rect.y + 24, 112, 112);
            SubPanel(portrait);
            DrawSprite(new Rect(portrait.x + 14, portrait.y + 10, 84, 92), ActiveSkin.idle[0]);

            var banner = new Rect(rect.x + 150, rect.y + 30, rect.width - 176, 82);
            DrawRect(new Rect(banner.x + 7, banner.y + 8, banner.width, banner.height), new Color(0.07f, 0.025f, 0.03f, 0.8f));
            DrawRect(banner, new Color(0.56f, 0.09f, 0.13f));
            DrawBorder(banner, new Color(0.24f, 0.035f, 0.05f), 3f);
            GUI.Label(banner, "PAUSED", _titleStyle);

            const float slotSize = 78f;
            const float slotGap = 16f;
            float slotsX = rect.x + 38f;
            for (int i = 0; i < 6; i++)
            {
                var slot = new Rect(slotsX + i * (slotSize + slotGap), rect.y + 156, slotSize, slotSize);
                SubPanel(slot);
                DrawBorder(slot, new Color(0.2f, 0.24f, 0.31f), 2f);
                if (i < Inventory.Count)
                    DrawSprite(new Rect(slot.x + 11, slot.y + 11, slot.width - 22, slot.height - 22),
                        WeaponSprite(Inventory[i].weaponId));
                else
                    GUI.Label(slot, "+", _titleStyle);
            }

            float buttonY = rect.yMax - 112f;
            float buttonWidth = 176f;
            if (Button(new Rect(rect.x + 38, buttonY, buttonWidth, 66), "HOME", _dangerButtonStyle))
            {
                SetPauseOpen(false);
                ReturnToHub();
            }
            if (Button(new Rect(rect.center.x - buttonWidth / 2f, buttonY, buttonWidth, 66), "RESUME", _buttonStyle))
                SetPauseOpen(false);

            var volumeRect = new Rect(rect.xMax - 38 - buttonWidth, buttonY, buttonWidth, 66);
            SubPanel(volumeRect);
            if (Button(new Rect(volumeRect.x + 4, volumeRect.y + 8, 42, 50), "-", _buttonStyle))
                ChangeVolume(-1);
            GUI.Label(new Rect(volumeRect.x + 48, volumeRect.y + 6, 80, 22), "VOLUME", _centerStyle);
            GUI.Label(new Rect(volumeRect.x + 48, volumeRect.y + 27, 80, 28),
                $"{_volumeStep * 25}%", _hudTitleStyle);
            if (Button(new Rect(volumeRect.xMax - 46, volumeRect.y + 8, 42, 50), "+", _buttonStyle))
                ChangeVolume(1);
        }

        void ChangeVolume(int direction)
        {
            _volumeStep = Mathf.Clamp(_volumeStep + direction, 0, GameAudio.MaxVolumeStep);
            GameAudio.SetVolumeStep(_volumeStep);
        }

        void DrawSceneTransition()
        {
            if (_transitionAmount <= 0f) return;
            const int bands = 12;
            float bandHeight = Mathf.Ceil(_uiHeight / bands) + 1f;
            for (int i = 0; i < bands; i++)
            {
                float stagger = i * 0.018f;
                float amount = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((_transitionAmount - stagger) / (1f - (bands - 1) * 0.018f)));
                float width = _uiWidth * amount;
                float x = i % 2 == 0 ? 0f : _uiWidth - width;
                DrawRect(new Rect(x, i * bandHeight, width, bandHeight), new Color(0.012f, 0.015f, 0.022f));
            }

            if (_transitionAmount < 0.72f) return;
            var labelRect = new Rect(_uiWidth / 2f - 190f, _uiHeight / 2f - 36f, 380f, 72f);
            DrawRect(labelRect, new Color(0.08f, 0.025f, 0.035f, 0.96f));
            DrawBorder(labelRect, new Color(0.55f, 0.12f, 0.16f), 3f);
            GUI.Label(labelRect, _transitionLabel, _titleStyle);
        }

        void DrawStartScreen()
        {
            DrawTitleBackdrop();
            var rect = Centered(700, 450);
            Panel(rect);

            var portrait = new Rect(rect.x + 26, rect.y + 24, 116, 116);
            SubPanel(portrait);
            DrawSprite(new Rect(portrait.x + 14, portrait.y + 10, 88, 98), _catalog.characters[0].idle[0]);

            var banner = new Rect(rect.x + 158, rect.y + 30, rect.width - 184, 82);
            DrawRect(new Rect(banner.x + 8, banner.y + 9, banner.width, banner.height), new Color(0.04f, 0.01f, 0.018f, 0.8f));
            DrawRect(banner, AccentRed);
            DrawBorder(banner, new Color(0.25f, 0.03f, 0.045f), 3f);
            GUI.Label(new Rect(banner.x, banner.y + 2, banner.width, 48), "DUNGEON DASH", _titleStyle);
            GUI.Label(new Rect(banner.x, banner.y + 47, banner.width, 24), "A POCKET ROGUELITE", _centerStyle);

            const float slotSize = 82f;
            const float gap = 20f;
            float slotsX = rect.x + 54f;
            for (int i = 0; i < 6; i++)
            {
                var slot = new Rect(slotsX + i * (slotSize + gap), rect.y + 168, slotSize, slotSize);
                SubPanel(slot);
                if (i < _catalog.characters.Length)
                    DrawSprite(new Rect(slot.x + 10, slot.y + 7, slot.width - 20, slot.height - 14), _catalog.characters[i].idle[0]);
                else
                    GUI.Label(slot, "+", _titleStyle);
            }

            GUI.Label(new Rect(rect.x + 40, rect.y + 268, rect.width - 80, 28),
                "CHOOSE A DELVER  ·  CLEAR CHAMBERS  ·  HUNT RARE ARTIFACTS", _centerStyle);
            if (Button(new Rect(rect.x + 110, rect.y + 322, 310, 66), "ENTER DUNGEON", _buttonStyle))
            {
                _creatingSlot = false;
                _selectedCharacter = null;
                _mode = GameMode.CharacterSelect;
            }
            if (Button(new Rect(rect.x + 438, rect.y + 322, 152, 66), "QUIT", _dangerButtonStyle))
                Application.Quit();
        }

        void DrawCharacterSelect()
        {
            if (_creatingSlot) { DrawHeroPicker(); return; }

            DrawTitleBackdrop();
            var rect = Centered(1000, 560);
            Panel(rect);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 7), AccentRed);
            GUI.Label(new Rect(rect.x + 34, rect.y + 22, 500, 42), "DELVERS", _titleStyle);
            GUI.Label(new Rect(rect.x + 34, rect.y + 72, rect.width - 68, 24), "CONTINUE A SAVE SLOT OR REGISTER A NEW OPERATIVE", _sectionStyle);
            if (Button(new Rect(rect.xMax - 150, rect.y + 22, 118, 38), "BACK", _buttonStyle))
                _mode = GameMode.StartScreen;

            const float slotGap = 24f;
            float slotWidth = (rect.width - 68f - slotGap * 2f) / 3f;
            for (int i = 0; i < SaveData.MaxSlots; i++)
            {
                var card = new Rect(rect.x + 34 + i * (slotWidth + slotGap), rect.y + 118, slotWidth, 400);
                SubPanel(card);
                if (i < _save.slots.Count) DrawFilledSlot(card, i);
                else DrawEmptySlot(card);
            }
        }

        void DrawEmptySlot(Rect card)
        {
            GUI.Label(new Rect(card.x, card.center.y - 46, card.width, 28), "EMPTY", _centerStyle);
            if (Button(new Rect(card.x + 24, card.center.y, card.width - 48, 46), "+ NEW CHARACTER", _buttonStyle))
            {
                _creatingSlot = true;
                _selectedCharacter = null;
                SelectDefaultCharacter();
            }
        }

        void DrawFilledSlot(Rect card, int index)
        {
            var slot = _save.slots[index];
            var skin = _catalog.characters.FirstOrDefault(x => x.id == slot.characterId) ?? _catalog.characters[0];
            DrawRect(new Rect(card.x, card.y, card.width, 5), AccentGold);
            GUI.Label(new Rect(card.x, card.y + 22, card.width, 28), CharacterName(skin.id).ToUpperInvariant(), _titleStyle);
            GUI.Label(new Rect(card.x, card.y + 56, card.width, 20), CharacterRole(skin.id).ToUpperInvariant(), _microStyle);
            var preview = new Rect(card.x + card.width / 2f - 55, card.y + 90, 110, 118);
            DrawRect(preview, new Color(0.018f, 0.027f, 0.04f));
            DrawBorder(preview, new Color(0.22f, 0.34f, 0.43f), 1f);
            DrawSprite(new Rect(preview.x + 12, preview.y + 10, 86, 98), skin.idle[0]);
            GUI.Label(new Rect(card.x, card.y + 222, card.width, 24), $"{slot.coins} COINS  ·  {slot.inventory.Count} ARTIFACTS", _centerStyle);
            if (Button(new Rect(card.x + 24, card.yMax - 110, card.width - 48, 44), "CONTINUE", _buttonStyle))
                ContinueSlot(index);
            if (Button(new Rect(card.x + 24, card.yMax - 56, card.width - 48, 40), "DELETE", _dangerButtonStyle))
                DeleteSlotAt(index);
        }

        void ContinueSlot(int index)
        {
            _creatingSlot = false;
            ActivateSlot(index);
            BuildHub();
        }

        void DeleteSlotAt(int index)
        {
            _save.DeleteSlot(index);
            _save.Save();
        }

        void ConfirmNewSlot(GameCatalog.CharacterSkin skin)
        {
            _save.CreateSlot(skin.id);
            _creatingSlot = false;
            ActivateSlot(_save.slots.Count - 1);
            BuildHub();
        }

        void DrawHeroPicker()
        {
            DrawTitleBackdrop();
            SelectDefaultCharacter();
            var rect = Centered(1120, 650);
            Panel(rect);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 7), AccentRed);
            GUI.Label(new Rect(rect.x + 34, rect.y + 22, 480, 42), "NEW DELVER", _titleStyle);
            GUI.Label(new Rect(rect.x + 520, rect.y + 29, rect.width - 690, 28), "REGISTRY  /  CHOOSE YOUR OPERATIVE", _rightStyle);
            if (Button(new Rect(rect.xMax - 150, rect.y + 22, 118, 38), "CANCEL", _buttonStyle))
                _creatingSlot = false;

            var rosterRect = new Rect(rect.x + 34, rect.y + 138, 706, 468);
            var detailRect = new Rect(rect.x + 758, rect.y + 138, 328, 468);
            SubPanel(rosterRect);

            const float gap = 10f;
            const float cardWidth = 219f;
            const float cardHeight = 103f;
            for (int i = 0; i < _catalog.characters.Length; i++)
            {
                var skin = _catalog.characters[i];
                int column = i % 3;
                int row = i / 3;
                var card = new Rect(rosterRect.x + 14f + column * (cardWidth + gap), rosterRect.y + 15f + row * (cardHeight + gap), cardWidth, cardHeight);
                bool hovered = card.Contains(Event.current.mousePosition);
                bool selected = skin == _selectedCharacter;
                DrawRect(card, selected ? new Color(0.085f, 0.13f, 0.18f) : hovered ? new Color(0.06f, 0.09f, 0.125f) : new Color(0.038f, 0.055f, 0.078f));
                DrawRect(new Rect(card.x, card.y, 3f, card.height), selected ? AccentGold : new Color(0.18f, 0.19f, 0.2f));
                if (selected) DrawBorder(card, AccentGold, 1f);
                var preview = new Rect(card.x + 10f, card.y + 8f, 76f, 86f);
                DrawSprite(preview, skin.idle[0]);
                GUI.Label(new Rect(card.x + 92, card.y + 15, card.width - 102, 23), CharacterName(skin.id).ToUpperInvariant(), _smallStyle);
                GUI.Label(new Rect(card.x + 92, card.y + 40, card.width - 102, 20),
                    $"{CharacterRole(skin.id).ToUpperInvariant()}  /  {CharacterVariant(skin.id)}", _microStyle);
                GUI.Label(new Rect(card.x + 92, card.y + 66, card.width - 102, 20), $"MOVE  {skin.speed:0.0}", _mutedStyle);
                if (GUI.Button(card, GUIContent.none, _clickStyle)) _selectedCharacter = skin;
            }

            DrawCharacterDetails(detailRect, _selectedCharacter);
        }

        void DrawCharacterDetails(Rect rect, GameCatalog.CharacterSkin skin)
        {
            SubPanel(rect);
            GUI.Label(new Rect(rect.x + 24, rect.y + 18, rect.width - 48, 22),
                $"{CharacterRole(skin.id).ToUpperInvariant()}  /  APPEARANCE {CharacterVariant(skin.id)}", _sectionStyle);

            var preview = new Rect(rect.x + 74, rect.y + 46, 180, 150);
            DrawRect(preview, new Color(0.018f, 0.027f, 0.04f));
            DrawBorder(preview, new Color(0.22f, 0.34f, 0.43f), 1f);
            DrawSprite(new Rect(preview.x + 15, preview.y + 11, 150, 128), skin.idle[0]);

            GUI.Label(new Rect(rect.x + 22, rect.y + 204, rect.width - 44, 30), CharacterName(skin.id).ToUpperInvariant(), _titleStyle);

            float statsY = rect.y + 252f;
            DrawStat(new Rect(rect.x + 24, statsY, rect.width - 48, 40), "HEALTH",
                SkinMaxHealth(skin).ToString("0"), SkinMaxHealth(skin) / 16f, new Color(0.82f, 0.35f, 0.4f));
            DrawStat(new Rect(rect.x + 24, statsY + 52, rect.width - 48, 40), "MOVEMENT",
                skin.speed.ToString("0.0"), Mathf.InverseLerp(4f, 6f, skin.speed), new Color(0.42f, 0.68f, 0.79f));
            DrawStat(new Rect(rect.x + 24, statsY + 104, rect.width - 48, 40), "DAMAGE",
                $"x{SkinDamageMod(skin):0.0}", Mathf.InverseLerp(0.8f, 1.6f, SkinDamageMod(skin)), new Color(0.85f, 0.66f, 0.32f));

            if (Button(new Rect(rect.x + 26, rect.yMax - 58, rect.width - 52, 42), "CONFIRM DELVER", _buttonStyle))
                ConfirmNewSlot(skin);
        }

        static float SkinMaxHealth(GameCatalog.CharacterSkin skin) => skin.maxHealth > 0f ? skin.maxHealth : 10f;
        static float SkinDamageMod(GameCatalog.CharacterSkin skin) => skin.damageMod > 0f ? skin.damageMod : 1f;

        static string CharacterName(string id) => CharacterBase(id) switch
        {
            "wizzard" => "Wizard",
            "doc" => "Plague Doctor",
            string value => Pretty(value)
        };

        static string CharacterRole(string id) => CharacterBase(id) switch
        {
            "knight" => "Vanguard",
            "elf" => "Pathfinder",
            "dwarf" => "Sentinel",
            "lizard" => "Skirmisher",
            "wizzard" => "Arcanist",
            _ => "Apothecary"
        };

        static string CharacterBase(string id) => id.EndsWith("_m", StringComparison.Ordinal)
            ? id.Substring(0, id.Length - 2)
            : id;

        static string CharacterVariant(string id) => id.EndsWith("_m", StringComparison.Ordinal) ? "II" : "I";

        void DrawInventory()
        {
            DrawBackdrop();
            SelectDefaultArtifact();
            var rect = Centered(1080, 650);
            Panel(rect);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 7), AccentRed);
            GUI.Label(new Rect(rect.x + 30, rect.y + 18, 500, 38), "THE VAULT", _titleStyle);
            GUI.Label(new Rect(rect.x + 32, rect.y + 58, 570, 22), "LOADOUT & ARTIFACTS  ·  STRONGEST FINDS FIRST", _sectionStyle);
            if (_catalog.coins.Length > 0)
                DrawSprite(new Rect(rect.x + 656, rect.y + 29, 22, 22), _catalog.coins[0]);
            GUI.Label(new Rect(rect.x + 684, rect.y + 25, 196, 32), $"{Coins}  ·  {Inventory.Count} ARTIFACTS", _rightStyle);
            if (Button(new Rect(rect.xMax - 164, rect.y + 20, 132, 38), "RESUME  [I]", _buttonStyle)) SetInventoryOpen(false);

            var listRect = new Rect(rect.x + 30, rect.y + 96, 590, 500);
            var detailRect = new Rect(rect.x + 640, rect.y + 96, 410, 500);
            SubPanel(listRect);
            DrawRect(new Rect(listRect.x, listRect.y, listRect.width, 64), new Color(0.045f, 0.07f, 0.105f));
            var hero = _catalog.characters.FirstOrDefault(x => x.id == ActiveCharacterId);
            if (hero != null)
            {
                DrawSprite(new Rect(listRect.x + 10, listRect.y + 4, 54, 56), hero.idle[0]);
                GUI.Label(new Rect(listRect.x + 73, listRect.y + 8, 250, 23), CharacterName(hero.id).ToUpperInvariant(), _smallStyle);
                GUI.Label(new Rect(listRect.x + 73, listRect.y + 32, 250, 20), $"{CharacterRole(hero.id).ToUpperInvariant()}  ·  ACTIVE DELVER", _microStyle);
            }
            GUI.Label(new Rect(listRect.x + 330, listRect.y + 11, 240, 42), "SELECT ARTIFACT TO INSPECT", _rightStyle);

            var artifacts = Inventory
                .OrderByDescending(x => x.id == _equipped.id)
                .ThenByDescending(x => x.quality)
                .ToArray();
            var viewport = new Rect(listRect.x + 8, listRect.y + 72, listRect.width - 16, listRect.height - 80);
            float contentHeight = Mathf.Max(viewport.height - 1f, artifacts.Length * 82f);
            _inventoryScroll = GUI.BeginScrollView(viewport, _inventoryScroll,
                new Rect(0, 0, viewport.width - 18f, contentHeight));
            for (int i = 0; i < artifacts.Length; i++)
            {
                var artifact = artifacts[i];
                var row = new Rect(0, i * 82f, viewport.width - 22f, 72f);
                bool selected = artifact == _selectedArtifact;
                bool hovered = row.Contains(Event.current.mousePosition);
                DrawRect(row, selected ? new Color(0.085f, 0.15f, 0.24f) : hovered ? new Color(0.06f, 0.1f, 0.16f) : new Color(0.04f, 0.065f, 0.105f));
                DrawRect(new Rect(row.x, row.y, 4, row.height), RarityColor(artifact.rarity));
                if (selected) DrawBorder(row, AccentGold, 1f);
                var sprite = WeaponSprite(artifact.weaponId);
                DrawSprite(new Rect(row.x + 15, row.y + 10, 52, 52), sprite);
                GUI.Label(new Rect(row.x + 80, row.y + 8, row.width - 178, 27), $"{artifact.rarity.ToUpperInvariant()}  {artifact.displayName}", _labelStyle);
                GUI.Label(new Rect(row.x + 80, row.y + 38, row.width - 170, 22), artifact.Stats, _mutedStyle);
                GUI.Label(new Rect(row.xMax - 88, row.y + 8, 72, 24), $"Q{artifact.quality}", _rightStyle);
                GUI.Label(new Rect(row.xMax - 118, row.y + 39, 102, 20), artifact.id == _equipped.id ? "EQUIPPED" : $"{artifact.Price} COINS", _microStyle);
                if (GUI.Button(row, GUIContent.none, _clickStyle)) _selectedArtifact = artifact;
            }
            GUI.EndScrollView();

            DrawArtifactDetails(detailRect, _selectedArtifact);
        }

        void DrawArtifactDetails(Rect rect, Artifact artifact)
        {
            SubPanel(rect);
            if (artifact == null)
            {
                GUI.Label(new Rect(rect.x + 30, rect.center.y - 20, rect.width - 60, 40), "SELECT AN ARTIFACT", _centerStyle);
                return;
            }

            Color rarity = RarityColor(artifact.rarity);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 5), rarity);
            GUI.Label(new Rect(rect.x + 22, rect.y + 18, rect.width - 44, 22), artifact.rarity.ToUpperInvariant(), _sectionStyle);
            GUI.Label(new Rect(rect.x + 22, rect.y + 42, rect.width - 44, 34), artifact.displayName, _labelStyle);

            var sprite = WeaponSprite(artifact.weaponId);
            var preview = new Rect(rect.x + 26, rect.y + 86, 112, 112);
            DrawRect(preview, new Color(0.02f, 0.035f, 0.06f));
            DrawBorder(preview, new Color(rarity.r, rarity.g, rarity.b, 0.65f), 2f);
            DrawSprite(new Rect(preview.x + 14, preview.y + 14, 84, 84), sprite);

            GUI.Label(new Rect(rect.x + 160, rect.y + 88, rect.width - 184, 22), "ROLL QUALITY", _microStyle);
            GUI.Label(new Rect(rect.x + 160, rect.y + 111, rect.width - 184, 43), $"{artifact.quality}", _titleStyle);
            DrawRect(new Rect(rect.x + 160, rect.y + 165, rect.width - 186, 8), new Color(0.015f, 0.025f, 0.045f));
            DrawRect(new Rect(rect.x + 160, rect.y + 165, (rect.width - 186) * artifact.quality / 100f, 8), rarity);
            GUI.Label(new Rect(rect.x + 160, rect.y + 179, rect.width - 184, 20), $"VALUE  {artifact.Price} COINS", _mutedStyle);

            float statsY = rect.y + 222f;
            DrawStat(new Rect(rect.x + 24, statsY, rect.width - 48, 48), "DAMAGE",
                artifact.EffectiveDamage.ToString(), artifact.EffectiveDamage / 40f, rarity);
            DrawStat(new Rect(rect.x + 24, statsY + 58, rect.width - 48, 48), "ATTACK SPEED", $"{artifact.attacksPerSecond:0.00} / SEC", artifact.attacksPerSecond / 3.5f, rarity);
            DrawStat(new Rect(rect.x + 24, statsY + 116, rect.width - 48, 48), "CRITICAL CHANCE", $"{artifact.criticalChance * 100f:0}%", artifact.criticalChance / 0.3f, rarity);

            bool equipped = artifact.id == _equipped.id;
            GUI.enabled = !equipped;
            string equipLabel = equipped ? "EQUIPPED" : "EQUIP ARTIFACT";
            if (Button(new Rect(rect.x + 24, rect.yMax - 86, 174, 42), equipLabel, _buttonStyle)) Equip(artifact);
            GUI.enabled = !equipped && !_onlineMarket.Busy;
            if (Button(new Rect(rect.x + 212, rect.yMax - 86, 174, 42), $"LIST  ·  {artifact.Price}", _dangerButtonStyle)) ListArtifact(artifact);
            GUI.enabled = true;
            GUI.Label(new Rect(rect.x + 24, rect.yMax - 36, rect.width - 48, 20),
                equipped ? "Equip another artifact before listing this one." : "Listing moves this artifact to the global market.", _centerStyle);
        }

        void DrawStat(Rect rect, string label, string value, float amount, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.6f, 22), label, _microStyle);
            GUI.Label(new Rect(rect.x + rect.width * 0.55f, rect.y, rect.width * 0.45f, 22), value, _rightStyle);
            var bar = new Rect(rect.x, rect.y + 28, rect.width, 8);
            DrawRect(bar, new Color(0.015f, 0.025f, 0.045f));
            DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(amount), bar.height), color);
        }

        void DrawMarket()
        {
            DrawBackdrop();
            var rect = Centered(860, 660);
            Panel(rect);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 6), AccentGold);
            GUI.Label(new Rect(rect.x + 30, rect.y + 18, 520, 38), "GLOBAL MARKET", _titleStyle);
            GUI.Label(new Rect(rect.x + 32, rect.y + 58, rect.width - 220, 22), _onlineMarket.Status, _sectionStyle);
            if (Button(new Rect(rect.xMax - 164, rect.y + 20, 132, 38), "CLOSE  [M]", _buttonStyle)) CloseMarket();

            string pending = _useOnlineMarket && _onlineMarket.PendingCoins > 0
                ? $"  ·  {_onlineMarket.PendingCoins} PROCEEDS READY" : string.Empty;
            if (_catalog.coins.Length > 0)
                DrawSprite(new Rect(rect.x + 30, rect.y + 84, 22, 22), _catalog.coins[0]);
            GUI.Label(new Rect(rect.x + 58, rect.y + 82, rect.width - 90, 26), $"{Coins} COINS{pending}", _smallStyle);

            float actionY = rect.y + 118;
            GUI.enabled = !_onlineMarket.Busy;
            if (_useOnlineMarket)
            {
                if (Button(new Rect(rect.x + 30, actionY, 150, 34), "REFRESH", _buttonStyle)) RefreshOnlineMarket();
                if (Button(new Rect(rect.x + 190, actionY, 190, 34), "CLAIM PROCEEDS", _buttonStyle)) ClaimOnlineMarket();
            }
            else if (Button(new Rect(rect.x + 30, actionY, 160, 34), "RETRY ONLINE", _buttonStyle)) OpenMarket();
            GUI.enabled = true;

            var listRect = new Rect(rect.x + 30, actionY + 48, rect.width - 60, rect.yMax - (actionY + 48) - 26);
            SubPanel(listRect);
            var listings = (_useOnlineMarket ? _onlineMarket.Listings : _localMarket.Listings).ToArray();
            var viewport = new Rect(listRect.x + 8, listRect.y + 8, listRect.width - 16, listRect.height - 16);
            const float rowHeight = 68f;
            float contentHeight = Mathf.Max(viewport.height - 1f, listings.Length * rowHeight);
            _marketScroll = GUI.BeginScrollView(viewport, _marketScroll,
                new Rect(0, 0, viewport.width - 18f, contentHeight));
            for (int i = 0; i < listings.Length; i++)
            {
                var listing = listings[i];
                var artifact = listing.artifact;
                var row = new Rect(0, i * rowHeight, viewport.width - 22f, rowHeight - 8f);
                bool hovered = row.Contains(Event.current.mousePosition);
                DrawRect(row, hovered ? new Color(0.06f, 0.1f, 0.16f) : new Color(0.04f, 0.065f, 0.105f));
                DrawRect(new Rect(row.x, row.y, 4, row.height), RarityColor(artifact.rarity));
                DrawSprite(new Rect(row.x + 15, row.y + 6, 48, 48), WeaponSprite(artifact.weaponId));
                GUI.Label(new Rect(row.x + 76, row.y + 4, row.width - 260, 25), $"{artifact.rarity.ToUpperInvariant()}  {artifact.displayName}", _labelStyle);
                GUI.Label(new Rect(row.x + 76, row.y + 32, row.width - 260, 22), $"{artifact.Stats}  ·  Q{artifact.quality}", _mutedStyle);
                GUI.Label(new Rect(row.xMax - 190, row.y + 4, 92, 24), $"{listing.price} COINS", _rightStyle);

                bool own = IsOwnListing(listing);
                var buttonRect = new Rect(row.xMax - 92, row.y + 16, 82, 32);
                GUI.enabled = own ? !_onlineMarket.Busy : !_onlineMarket.Busy && Coins >= listing.price;
                if (Button(buttonRect, own ? "CANCEL" : "BUY", own ? _dangerButtonStyle : _buttonStyle))
                {
                    if (own) CancelListing(listing); else BuyListing(listing);
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        void DrawGameOver()
        {
            DrawBackdrop();
            var rect = Centered(480, 260);
            Panel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 30, rect.y + 25, rect.width - 60, rect.height - 50));
            GUILayout.Label("RUN ENDED", _titleStyle);
            GUILayout.Label($"Reached wave {_wave} with {_kills} kills.\nArtifacts and coins are saved.", _labelStyle);
            GUILayout.FlexibleSpace();
            if (LayoutButton("Return to Home Hub", _dangerButtonStyle)) Restart();
            GUILayout.EndArea();
        }

        void Equip(Artifact artifact)
        {
            _equipped = artifact;
            EquippedId = artifact.id;
            _player.RefreshWeapon();
            Toast($"Equipped {artifact.displayName}");
            Save();
        }

        void ListArtifact(Artifact artifact)
        {
            if (_onlineMarket.Busy) return;
            ListArtifactOnlineOrLocal(artifact);
        }

        void BuyListing(MarketListing listing)
        {
            if (_onlineMarket.Busy) return;
            BuyListingOnlineOrLocal(listing);
        }

        void CancelListing(MarketListing listing)
        {
            if (_onlineMarket.Busy) return;
            CancelListingOnlineOrLocal(listing);
        }

        bool IsOwnListing(MarketListing listing) => _useOnlineMarket
            ? listing.sellerId == _onlineMarket.PlayerId
            : listing.sellerId == LocalMarketService.PlayerSeller;

        async void ListArtifactOnlineOrLocal(Artifact artifact)
        {
            bool online = await EnsureOnlineMarket();
            if (!online)
            {
                if (_onlineMarket.IsOnline) return;
                Inventory.Remove(artifact);
                _localMarket.List(artifact, artifact.Price);
                Toast($"Online unavailable — listed {artifact.displayName} locally");
                Save();
                return;
            }

            try
            {
                await _onlineMarket.ListAsync(artifact, artifact.Price);
                Inventory.Remove(artifact);
                SyncOnlineBalance();
                Toast($"Listed {artifact.displayName} globally for {artifact.Price} coins");
            }
            catch (Exception exception)
            {
                Toast("Listing failed: " + exception.GetBaseException().Message);
            }
        }

        async void BuyListingOnlineOrLocal(MarketListing listing)
        {
            if (!_useOnlineMarket)
            {
                int coins = Coins;
                var localArtifact = _localMarket.Buy(listing.id, ref coins);
                if (localArtifact == null) return;
                if (_save.marketAccountInitialized)
                    _save.marketPendingCoinDelta += coins - Coins;
                Coins = coins;
                Inventory.Add(localArtifact);
                Toast($"Bought {localArtifact.displayName} locally");
                Save();
                return;
            }

            try
            {
                var response = await _onlineMarket.BuyAsync(listing.id);
                if (response.artifact != null) Inventory.Add(response.artifact);
                SyncOnlineBalance();
                Toast(response.message);
            }
            catch (Exception exception)
            {
                Toast("Purchase failed: " + exception.GetBaseException().Message);
            }
        }

        async void CancelListingOnlineOrLocal(MarketListing listing)
        {
            if (!_useOnlineMarket)
            {
                var localArtifact = _localMarket.Cancel(listing.id);
                if (localArtifact == null) return;
                Inventory.Add(localArtifact);
                Toast($"Returned {localArtifact.displayName}");
                Save();
                return;
            }

            try
            {
                var response = await _onlineMarket.CancelAsync(listing.id);
                if (response.artifact != null) Inventory.Add(response.artifact);
                SyncOnlineBalance();
                Toast(response.message);
            }
            catch (Exception exception)
            {
                Toast("Cancel failed: " + exception.GetBaseException().Message);
            }
        }

        async void RefreshOnlineMarket()
        {
            try
            {
                await _onlineMarket.RefreshAsync();
                SyncOnlineBalance();
                Toast("Market refreshed");
            }
            catch (Exception exception)
            {
                Toast("Refresh failed: " + exception.GetBaseException().Message);
            }
        }

        async void ClaimOnlineMarket()
        {
            try
            {
                var response = await _onlineMarket.ClaimAsync();
                SyncOnlineBalance();
                Toast(response.message);
            }
            catch (Exception exception)
            {
                Toast("Claim failed: " + exception.GetBaseException().Message);
            }
        }

        void Panel(Rect rect) => DrawPanel(rect);

        void DrawBackdrop()
        {
            DrawRect(new Rect(0, 0, _uiWidth, _uiHeight), new Color(0.008f, 0.01f, 0.016f, 0.7f));
            DrawRect(new Rect(0, 0, _uiWidth, 5), AccentRed);
        }

        void DrawTitleBackdrop()
        {
            DrawRect(new Rect(0, 0, _uiWidth, _uiHeight), new Color(0.025f, 0.022f, 0.025f));
            const float tileSize = 64f;
            var previous = GUI.color;
            GUI.color = new Color(0.42f, 0.39f, 0.4f, 1f);
            for (float y = 0; y < _uiHeight; y += tileSize)
            for (float x = 0; x < _uiWidth; x += tileSize)
            {
                int index = (Mathf.RoundToInt(x / tileSize) + Mathf.RoundToInt(y / tileSize) * 3) % _catalog.floors.Length;
                DrawSprite(new Rect(x, y, tileSize, tileSize), _catalog.floors[index]);
            }

            var topWall = DungeonTileSelector.FindByName(_catalog.walls, "wall_top_mid");
            var bottomWall = DungeonTileSelector.FindByName(_catalog.walls, "edge_down");
            GUI.color = new Color(0.68f, 0.62f, 0.62f, 1f);
            for (float x = 0; x < _uiWidth; x += tileSize)
            {
                DrawSprite(new Rect(x, 24f, tileSize, tileSize), topWall);
                DrawSprite(new Rect(x, _uiHeight - 88f, tileSize, tileSize), bottomWall);
            }

            GUI.color = new Color(0.76f, 0.73f, 0.73f, 1f);
            int castCount = Mathf.Min(6, _catalog.characters.Length);
            for (int i = 0; i < castCount; i++)
            {
                float x = i < 3 ? 44f + i * 104f : _uiWidth - 132f - (i - 3) * 104f;
                float y = _uiHeight - 202f - (i % 2) * 24f;
                DrawSprite(new Rect(x, y, 88f, 108f), _catalog.characters[i].idle[0]);
            }
            GUI.color = previous;
            DrawRect(new Rect(0, 0, _uiWidth, _uiHeight), new Color(0.02f, 0.015f, 0.02f, 0.46f));
        }

        Rect Centered(float width, float height) =>
            new((_uiWidth - width) / 2f, (_uiHeight - height) / 2f, width, height);

        static string Pretty(string value)
        {
            value = value.Replace('_', ' ');
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }

    public enum PickupKind { Coin, Potion, Chest, Bomb, Artifact }

    public sealed class EnemyActor : MonoBehaviour
    {
        const float SpawnTelegraphDuration = 0.18f;

        DungeonGame _game;
        GameCatalog.EnemySkin _skin;
        SpriteRenderer _renderer;
        EnemyNavigator _navigator;
        int _health;
        float _animationTime;
        float _flashUntil;
        float _invulnerableUntil;
        float _spawnTime;

        public void Setup(DungeonGame game, GameCatalog.EnemySkin skin, int health)
        {
            _game = game;
            _skin = skin;
            _health = health;
            _renderer = GetComponent<SpriteRenderer>();
            _navigator = GetComponent<EnemyNavigator>();
            _spawnTime = Time.time;
        }

        void Update()
        {
            _renderer.color = Time.time < _flashUntil
                ? new Color(3.5f, 3.5f, 3.5f, 1f)
                : Color.white;
            float spawnT = Mathf.Clamp01((Time.time - _spawnTime) / SpawnTelegraphDuration);
            transform.localScale = Vector3.one * spawnT;
            _renderer.sortingOrder = YSort.Order(transform.position.y, 1);
            if (!_game.PlayerAlive || !_game.CombatActive) return;
            Vector2 delta = _game.PlayerPosition - (Vector2)transform.position;
            _renderer.flipX = delta.x < 0f;
            _animationTime += Time.deltaTime;
            var frames = _navigator != null && _navigator.Velocity.sqrMagnitude > 0.01f && _skin.run.Length > 0
                ? _skin.run
                : _skin.idle;
            if (frames.Length > 0) _renderer.sprite = frames[Mathf.FloorToInt(_animationTime * 8f) % frames.Length];
        }

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, _game == null ? transform.position : _game.PlayerPosition, false);
        }

        public void TakeDamage(int damage, Vector2 sourcePosition) => TakeDamage(damage, sourcePosition, false);

        public void TakeDamage(int damage, Vector2 sourcePosition, bool critical)
        {
            if (Time.time < _invulnerableUntil) return;
            _invulnerableUntil = Time.time + 0.12f;
            _flashUntil = Time.time + 0.1f;
            _navigator?.KnockbackFrom(sourcePosition);
            _health -= damage;
            FloatingDamageNumbers.Spawn(transform.position, damage,
                critical ? DamageNumberKind.Critical : DamageNumberKind.Normal);
            PixelBurst.HitSpark(transform.position, ((Vector2)transform.position - sourcePosition).normalized, critical);
            GameAudio.Play(critical ? "crit_impact" : "hit_impact", 0.8f);
            if (critical) GameFeel.Shake(0.22f);
            if (_health > 0) return;
            _game.EnemyDied(this);
            PixelBurst.EnemyDeathPuff(transform.position, _skin.id);
            GameAudio.Play("enemy_die", 0.8f);
            GameFeel.Shake(0.14f);
            gameObject.AddComponent<CorpseFade>().Begin(_renderer);
            Destroy(_navigator);
            Destroy(this);
        }
    }

    // Keeps the sprite visible for a short fade after EnemyActor destroys itself, so
    // FindObjectsByType<EnemyActor> reflects the kill immediately while the corpse lingers.
    public sealed class CorpseFade : MonoBehaviour
    {
        const float FadeDuration = 0.35f;

        SpriteRenderer _renderer;
        float _startTime;

        public void Begin(SpriteRenderer renderer)
        {
            _renderer = renderer;
            _renderer.color = Color.white;
            _startTime = Time.time;
        }

        void Update()
        {
            float t = (Time.time - _startTime) / FadeDuration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            _renderer.color = new Color(1f, 1f, 1f, 1f - t);
            transform.localScale = Vector3.one * (1f - t * 0.25f);
        }
    }

    public sealed class ProjectileActor : MonoBehaviour
    {
        DungeonGame _game;
        Vector2 _direction;
        int _damage;
        bool _critical;
        float _expires;

        public void Setup(DungeonGame game, Vector2 direction, int damage, bool critical = false)
        {
            _game = game;
            _direction = direction;
            _damage = damage;
            _critical = critical;
            _expires = Time.time + 1.6f;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 45f);
        }

        void Update()
        {
            if (!_game.CombatActive) return;
            transform.position += (Vector3)(_direction * (11f * Time.deltaTime));
            var target = _game.ProjectileTarget(transform.position);
            if (target != null)
            {
                target.TakeDamage(_damage, transform.position - (Vector3)(_direction * 0.35f), _critical);
                Destroy(gameObject);
            }
            else if (Time.time >= _expires) Destroy(gameObject);
        }
    }

    public sealed class PickupActor : MonoBehaviour
    {
        const float MagnetRadius = 1.5f;
        const float MagnetSpeed = 6f;

        DungeonGame _game;
        PickupKind _kind;
        Artifact _artifact;
        Sprite[] _frames;
        SpriteRenderer _renderer;
        float _spawnTime;

        public void Setup(DungeonGame game, PickupKind kind, Artifact artifact, Sprite[] frames = null)
        {
            _game = game;
            _kind = kind;
            _artifact = artifact;
            _frames = frames;
            _renderer = GetComponent<SpriteRenderer>();
            _spawnTime = Time.time;
        }

        void Update()
        {
            if (!_game.CombatActive) return;
            if (_frames != null && _frames.Length > 0)
                _renderer.sprite = _frames[Mathf.FloorToInt(Time.time * 8f) % _frames.Length];
            _renderer.sortingOrder = YSort.Order(transform.position.y, 0);
            if (_kind == PickupKind.Coin)
            {
                Vector2 toPlayer = _game.PlayerPosition - (Vector2)transform.position;
                if (toPlayer.sqrMagnitude < MagnetRadius * MagnetRadius)
                    transform.position = Vector2.MoveTowards(transform.position, _game.PlayerPosition, MagnetSpeed * Time.deltaTime);
            }
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.07f);
            if (((Vector2)transform.position - _game.PlayerPosition).sqrMagnitude < 0.7f * 0.7f)
            {
                if (_kind == PickupKind.Coin) { PixelBurst.CoinSparkle(transform.position); GameAudio.Play("coin", 0.5f); }
                else if (_kind == PickupKind.Potion) { PixelBurst.PotionGlint(transform.position); GameAudio.Play("potion", 0.5f); }
                _game.Collect(_kind, _artifact);
                Destroy(gameObject);
            }
            else if (Time.time - _spawnTime > 25f) Destroy(gameObject);
        }
    }
}
