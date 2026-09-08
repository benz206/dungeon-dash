using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonDash
{
    public enum GameMode { StartScreen, CharacterSelect, HomeHub, InDungeon, Market, Inventory, Paused, GameOver, Cosmetics }

    public sealed class DungeonGame : MonoBehaviour, IMarketHost
    {
        const float TransitionHalfDuration = 0.32f;
        const float BombRadius = 4f;
        const float PotionDropChance = 0.14f;

        readonly System.Random _random = new();

        CatalogIndex _catalog;
        LevelLibrary _library;
        WorldBuilder _worldBuilder;
        CombatDirector _combat;
        MarketController _market;
        GameUi _ui;
        SaveData _save;
        BuiltWorld _world;
        PlayerController _player;
        Artifact _equipped;
        SaveData.CharacterSlot _activeSlot;

        GameMode _mode = GameMode.StartScreen;
        GameMode _returnMode = GameMode.HomeHub;
        GameMode _cosmeticsReturnMode = GameMode.StartScreen;
        bool _creatingSlot;
        bool _overlayOwnsPause;
        float _timeScaleBeforeOverlay = 1f;
        bool _transitioning;
        bool _pauseAfterTransition;
        float _transitionAmount;
        string _transitionLabel = string.Empty;
        int _weaponCursor;
        int _volumeStep;
        int _chamberSeed;
        bool _runReady;
        float _nextCheckpoint;
        Vector2 _tutorialOrigin;

        public bool WorldRunning => (_mode == GameMode.HomeHub || _mode == GameMode.InDungeon) && !_transitioning;
        public bool AcceptsGameplayInput => WorldRunning;
        public bool CombatActive => _mode == GameMode.InDungeon && !_transitioning;
        public bool TransitionActive => _transitioning;
        public bool RoomExitUnlocked => _world?.ExitDoor?.Unlocked ?? false;
        public int CurrentRoom => _combat.Wave;
        public int Kills => _combat.Kills;
        public int BestChamberCleared => _activeSlot?.bestChamberCleared ?? 0;
        public int LifetimeKills => _activeSlot?.lifetimeKills ?? 0;
        public string GuildRank => _activeSlot?.GuildRank ?? "DELVER";
        public string ProgressGoal => _activeSlot?.NextGoal ?? string.Empty;
        public GameMode Mode => _mode;
        public CatalogIndex Catalog => _catalog;
        public MarketController Market => _market;
        public SaveData Save => _save;
        public PlayerController Player => _player;
        public ChamberTheme Theme => _world?.Theme;
        public Artifact EquippedArtifact => _equipped;
        public int VolumeStep => _volumeStep;
        public MobileControls TouchControls => _ui?.TouchControls;
        public CosmeticStore Cosmetics { get; private set; }
        public bool CosmeticsFromTitle => _cosmeticsReturnMode == GameMode.StartScreen;
        public string TutorialHint => _activeSlot == null ? null : FirstRunGuide.Hint(
            _activeSlot.tutorialActions, _combat.Wave == 0, MobileControls.Enabled);

        static readonly Artifact[] NoArtifacts = Array.Empty<Artifact>();

        public IReadOnlyList<Artifact> Inventory =>
            _activeSlot != null ? (IReadOnlyList<Artifact>)_activeSlot.inventory : NoArtifacts;

        public GameCatalog.CharacterSkin ActiveSkin =>
            _activeSlot == null ? _catalog.Catalog.characters[0] : _catalog.Character(_activeSlot.characterId);

        public float ChamberClearProgress => _mode != GameMode.InDungeon || _combat.WaveSize == 0
            ? 1f
            : 1f - _combat.EnemyCount / (float)_combat.WaveSize;

        public int Coins
        {
            get => _activeSlot?.coins ?? 0;
            set { if (_activeSlot != null) _activeSlot.coins = value; }
        }

        string EquippedId
        {
            get => _activeSlot?.equippedId;
            set { if (_activeSlot != null) _activeSlot.equippedId = value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DungeonGame>() == null)
                new GameObject("Dungeon Dash").AddComponent<DungeonGame>();
        }

        void Awake()
        {
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            if (catalog == null)
            {
                Debug.LogError("GameCatalog is missing. Run Tools/Dungeon Dash/Generate Game Catalog.");
                enabled = false;
                return;
            }

            _catalog = new CatalogIndex(catalog);
            _library = Resources.Load<LevelLibrary>(LevelLibrary.ResourcePath);
            if (_library == null || !_library.IsUsable)
                Debug.LogWarning("LevelLibrary is missing or empty. Run Tools/Dungeon Dash/Generate Level Library.");

            _worldBuilder = new WorldBuilder(_catalog);
            _combat = new CombatDirector(this, _catalog);
            _combat.EnemyDefeated += OnEnemyDefeated;
            _combat.ChamberCleared += OnChamberCleared;

            _save = SaveData.Load();
            _market = new MarketController(this, _save, new LocalMarketService(_save.marketJson), new UgsMarketService());
            _market.Seed(_catalog, _random);
            Cosmetics = gameObject.AddComponent<CosmeticStore>();
            Cosmetics.Initialize(this);

            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = 60;
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }

            ConfigureCamera();
            _volumeStep = GameAudio.MutedForAutomation ? 0 : GameAudio.SavedVolumeStep;
            GameAudio.ApplySavedVolume();

            _ui = gameObject.AddComponent<GameUi>();
            _ui.Initialize(this);
            ApplyMode();
        }

        void Start()
        {
            Cosmetics.Connect();
            RunQaArguments();
        }

        void Update()
        {
            if (AcceptsGameplayInput && _player != null &&
                (PlayerPosition - _tutorialOrigin).sqrMagnitude >= 4f)
                RecordTutorialAction(TutorialAction.Move);
            if (_runReady && CombatActive && Time.unscaledTime >= _nextCheckpoint) PersistSave();
            if (_pauseAfterTransition && !_transitioning)
            {
                _pauseAfterTransition = false;
                SetPauseOpen(true);
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_mode == GameMode.Cosmetics)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) CloseCosmetics();
                return;
            }
            if (_mode is GameMode.StartScreen or GameMode.CharacterSelect or GameMode.GameOver) return;
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

        void ApplyMode() =>
            _ui.SetMode(_mode, _creatingSlot && _mode == GameMode.CharacterSelect);

        void SetMode(GameMode mode)
        {
            _mode = mode;
            ApplyMode();
        }


        void ToggleInventory()
        {
            if (_mode == GameMode.Inventory) { SetInventoryOpen(false); return; }
            if (_mode == GameMode.Market) CloseMarket();
            SetInventoryOpen(true);
        }

        void ToggleMarket()
        {
            if (_mode == GameMode.Market) { CloseMarket(); return; }
            if (_mode == GameMode.Inventory) SetInventoryOpen(false);
            OpenMarketOverlay();
        }

        public void OpenMarketOverlay()
        {
            if (_mode != GameMode.HomeHub && _mode != GameMode.InDungeon) return;
            if (_transitioning) return;
            _returnMode = _mode;
            GameFeel.EndHitStop();
            _timeScaleBeforeOverlay = Time.timeScale;
            Time.timeScale = 0f;
            _overlayOwnsPause = true;
            SetMode(GameMode.Market);
            GameAudio.Play("ui_click", 0.5f);
            _market.Open();
        }

        public void CloseMarket()
        {
            if (_mode != GameMode.Market) return;
            SetMode(_returnMode);
            ReleaseOverlayPause();
            GameAudio.Play("ui_click", 0.5f);
        }

        void CloseOverlay()
        {
            if (_mode == GameMode.Inventory) SetInventoryOpen(false);
            else if (_mode == GameMode.Market) CloseMarket();
        }

        public void SetInventoryOpen(bool open)
        {
            if ((_mode == GameMode.Inventory) == open || (open && _transitioning)) return;
            if (open)
            {
                if (_mode != GameMode.HomeHub && _mode != GameMode.InDungeon) return;
                _returnMode = _mode;
                GameFeel.EndHitStop();
                _timeScaleBeforeOverlay = Time.timeScale;
                Time.timeScale = 0f;
                _overlayOwnsPause = true;
                SetMode(GameMode.Inventory);
                GameAudio.Play("ui_click", 0.5f);
            }
            else
            {
                SetMode(_returnMode);
                GameAudio.Play("ui_click", 0.5f);
                ReleaseOverlayPause();
            }
        }

        public void SetPauseOpen(bool open)
        {
            if ((_mode == GameMode.Paused) == open || (open && _transitioning)) return;
            if (open)
            {
                if (_mode != GameMode.HomeHub && _mode != GameMode.InDungeon) return;
                _returnMode = _mode;
                GameFeel.EndHitStop();
                _timeScaleBeforeOverlay = Time.timeScale;
                Time.timeScale = 0f;
                _overlayOwnsPause = true;
                SetMode(GameMode.Paused);
                GameAudio.Play("ui_click", 0.5f);
            }
            else
            {
                SetMode(_returnMode);
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


        public void ShowTitle()
        {
            PersistSave();
            ReleaseOverlayPause();
            ClearWorld();
            _creatingSlot = false;
            SetMode(GameMode.StartScreen);
        }

        public void OpenSlotSelect()
        {
            _creatingSlot = false;
            SetMode(GameMode.CharacterSelect);
        }

        public void OpenHeroPicker()
        {
            _creatingSlot = true;
            SetMode(GameMode.CharacterSelect);
        }

        public void CancelHeroPicker()
        {
            _creatingSlot = false;
            SetMode(GameMode.CharacterSelect);
        }

        public void ConfirmNewSlot(GameCatalog.CharacterSkin skin)
        {
            if (skin == null) return;
            _save.CreateSlot(skin.id);
            _creatingSlot = false;
            ActivateSlot(_save.slots.Count - 1);
            BuildHub();
        }

        public void ContinueSlot(int index)
        {
            _creatingSlot = false;
            ActivateSlot(index);
            var checkpoint = _activeSlot.run;
            if (checkpoint != null && checkpoint.CanResume &&
                checkpoint.usesLevelLibrary == (_library != null && _library.IsUsable) &&
                checkpoint.enemies.All(enemy => enemy.health > 0 && _catalog.Enemy(enemy.skinId) != null))
                RestoreRun(checkpoint);
            else BuildHub();
        }

        public void DeleteSlotAt(int index)
        {
            _save.DeleteSlot(index);
            _save.Save();
            _ui.RefreshActive();
        }

        public void QuitGame() => Application.Quit();

        public void OpenCosmetics()
        {
            if (_mode != GameMode.StartScreen && _mode != GameMode.Paused) return;
            _cosmeticsReturnMode = _mode;
            SetMode(GameMode.Cosmetics);
            Cosmetics.Connect();
        }

        public void CloseCosmetics()
        {
            if (_mode == GameMode.Cosmetics) SetMode(_cosmeticsReturnMode);
        }

        void EnsureSlotForCharacter(string characterId)
        {
            int index = _save.slots.FindIndex(slot => slot.characterId == characterId);
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
            PersistSave();
            ReleaseOverlayPause();
            ClearWorld();
            _save.activeSlot = index;
            _activeSlot = _save.slots[index];
            EnsureStartingInventory();
            PersistSave();
        }

        void EnsureStartingInventory()
        {
            foreach (var artifact in _activeSlot.inventory.Where(x => !WeaponRules.IsArtifactWeapon(x.weaponId)))
            {
                artifact.weaponId = "weapon_bow";
                artifact.displayName = artifact.displayName.Replace("Arrow", "Bow");
            }
            if (_activeSlot.inventory.Count == 0)
            {
                var starter = ArtifactGenerator.Roll(_catalog.Catalog.weapons[0].id, new System.Random(1));
                starter.displayName = "Starter " + starter.displayName;
                _activeSlot.inventory.Add(starter);
                EquippedId = starter.id;
            }
            _equipped = _activeSlot.inventory.FirstOrDefault(x => x.id == EquippedId) ?? _activeSlot.inventory[0];
            EquippedId = _equipped.id;
            PersistSave();
        }


        void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f);
        }

        public void BuildHub()
        {
            ClearWorld();
            _activeSlot.run = null;
            _world = _worldBuilder.BuildHub(this, OpenMarketFromHub, BeginDungeonTransition);
            _combat.BeginRun();
            _combat.EnterChamber(_world);
            SpawnPlayer(_world.EntryPoint);
            SetMode(GameMode.HomeHub);
            PersistSave();
            Toast(MobileControls.Enabled
                ? "Move with the left pad. Approach a destination to enter."
                : "Home — approach a destination and press E");
        }

        void EnterDungeon()
        {
            ClearWorld();
            _combat.BeginRun();
            BuildChamber();
            SpawnPlayer(_world.EntryPoint);
            SetMode(GameMode.InDungeon);
            _combat.SpawnWave();
            _runReady = true;
            PersistSave();
            _ui.RefreshActive();
            Toast("Clear the chamber and unlock the north door");
        }

        void BuildChamber(int? seed = null, int? depth = null)
        {
            _chamberSeed = seed ?? _random.Next();
            var random = new System.Random(_chamberSeed);
            int chamberDepth = depth ?? _combat.Wave + 1;
            var plan = _library != null && _library.IsUsable
                ? ChamberBuilder.Build(random, chamberDepth, _library)
                : LegacyPlan(random, chamberDepth);
            _world = _worldBuilder.BuildChamber(this, plan, BeginNextRoomTransition);
            _combat.EnterChamber(_world);
        }

        ChamberPlan LegacyPlan(System.Random random, int depth)
        {
            var layout = DungeonGenerator.GenerateChunk(random, depth);
            var plan = new ChamberPlan
            {
                Layout = layout,
                WallDecorations = WallClassifier.PlanDecorations(layout.Walkable.Contains,
                    layout.GridBounds, layout.Rooms.Select(room => room.Bounds).ToList())
            };
            foreach (var cell in layout.Walkable)
                if (!layout.Corridors.Contains(cell) && (cell - Vector2Int.RoundToInt(layout.EntryPoint)).sqrMagnitude > 16)
                    plan.SpawnAnchors.Add(cell);
            if (plan.SpawnAnchors.Count == 0) plan.SpawnAnchors.Add(layout.EntryPoint);
            return plan;
        }

        void ClearWorld()
        {
            _runReady = false;
            _combat.ClearActors();
            if (_player != null) Destroy(_player.gameObject);
            _player = null;
            DungeonViewportSystem.Track(null);
            if (_world?.Root != null) Destroy(_world.Root.gameObject);
            _world = null;
        }

        void SpawnPlayer(Vector2 position)
        {
            _tutorialOrigin = position;
            if (_player != null) Destroy(_player.gameObject);
            var skin = ActiveSkin;
            var playerObject = WorldBuilder.CreateSprite("Player", skin.idle[0], position, 10);
            var body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = playerObject.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.62f, 0.9f);
            collider.offset = new Vector2(0f, -0.25f);
            _player = playerObject.AddComponent<PlayerController>();
            _player.Setup(this, skin);
            playerObject.AddComponent<CosmeticAura>().Initialize(_save);
            DungeonViewportSystem.Track(_player);
        }

        void OpenMarketFromHub() => OpenMarketOverlay();

        public void ReturnToHub()
        {
            ReleaseOverlayPause();
            BuildHub();
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
            yield return AnimateTransition(0f, 1f);
            EnterDungeon();
            yield return null;
            Camera.main?.GetComponent<PlayerCenteredCamera>()?.CenterNow();
            yield return AnimateTransition(1f, 0f);
            EndTransition();
        }

        void BeginNextRoomTransition()
        {
            if (!RoomExitUnlocked || _transitioning || _mode != GameMode.InDungeon) return;
            StartCoroutine(LoadNextRoom());
        }

        IEnumerator LoadNextRoom()
        {
            _transitioning = true;
            _transitionLabel = $"CHAMBER {_combat.Wave + 1:00}";
            yield return AnimateTransition(0f, 1f);

            _runReady = false;
            _combat.ClearActors();
            if (_world?.Root != null) Destroy(_world.Root.gameObject);
            _world = null;
            yield return null;

            BuildChamber();
            if (_player != null)
            {
                _player.transform.position = _world.EntryPoint;
                var body = _player.GetComponent<Rigidbody2D>();
                if (body != null) body.linearVelocity = Vector2.zero;
            }
            Camera.main?.GetComponent<PlayerCenteredCamera>()?.CenterNow();
            _combat.SpawnWave();
            _runReady = true;
            RecordTutorialAction(TutorialAction.Exit);
            PersistSave();

            yield return AnimateTransition(1f, 0f);
            EndTransition();
            _ui.RefreshActive();
            Toast(_combat.Wave == 2 ? "CASTERS · Move away from their aim lines before they fire."
                : $"CHAMBER {_combat.Wave:00}  ·  FIND THE EXIT");
        }

        void EndTransition()
        {
            _transitionAmount = 0f;
            _transitioning = false;
            _ui.SetTransition(0f, string.Empty);
        }

        IEnumerator AnimateTransition(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < TransitionHalfDuration)
            {
                elapsed += Mathf.Clamp(Time.unscaledDeltaTime, 1f / 120f, 1f / 30f);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / TransitionHalfDuration));
                _transitionAmount = Mathf.Lerp(from, to, t);
                _ui.SetTransition(_transitionAmount, _transitionLabel);
                yield return null;
            }
            _transitionAmount = to;
            _ui.SetTransition(_transitionAmount, _transitionLabel);
        }


        public void EnemyDied(EnemyActor enemy)
        {
            _combat.Defeat(enemy);
            PersistSave();
        }

        void OnEnemyDefeated(EnemyActor enemy)
        {
            _activeSlot.lifetimeKills++;
            AddCoins(1 + _combat.Wave / 3);
            var position = enemy.transform.position;
            if (_combat.Kills % 3 == 0) DropArtifact(position);
            else if (_random.NextDouble() < PotionDropChance) DropPickup(position, PickupKind.Potion);
            else if (_random.NextDouble() < 0.12d) DropPickup(position, PickupKind.Bomb);
            else if (_random.NextDouble() < 0.35d) DropPickup(position, PickupKind.Coin);
        }

        void OnChamberCleared()
        {
            string previousRank = GuildRank;
            _activeSlot.bestChamberCleared = Mathf.Max(_activeSlot.bestChamberCleared, _combat.Wave);
            int sold = _market.SimulateSales(_random);
            DropPickup(_world != null ? _world.EntryPoint : Vector2.zero, PickupKind.Chest);
            _world?.ExitDoor?.Unlock();
            RecordTutorialAction(TutorialAction.Clear);
            GameAudio.Play("chest_open", 0.75f);
            Toast(previousRank != GuildRank ? $"GUILD RANK: {GuildRank} · NORTH DOOR UNLOCKED" : sold > 0
                ? $"Chamber clear — {sold} market listing sold!"
                : "CHAMBER CLEAR  ·  NORTH DOOR UNLOCKED");
        }

        public EnemyActor ProjectileTarget(Vector2 position) => _combat.ProjectileTarget(position);
        public bool ProjectilePathClear(Vector2 from, Vector2 to) => _combat.ProjectilePathClear(from, to);

        public Vector2 PlayerPosition => _player == null ? Vector2.zero : (Vector2)_player.transform.position;
        public bool PlayerAlive => _player != null && _player.Health > 0;
        public void HurtPlayer(int amount) => _player?.TakeDamage(amount);
        public void HurtPlayer(int amount, Vector2 sourcePosition) => _player?.TakeDamage(amount, sourcePosition);

        public Sprite WeaponSprite(string id) => _catalog.Weapon(id);

        public void UseWeapon(Vector2 position, Vector2 direction, int damage, string weaponId,
            Sprite equippedSprite, bool critical)
        {
            RecordTutorialAction(TutorialAction.Attack);
            if (WeaponRules.IsRanged(weaponId))
            {
                Fire(position, direction, damage, _catalog.Weapon(WeaponRules.ProjectileSpriteId(weaponId)), critical);
                return;
            }
            MeleeStrike(position, direction, damage, equippedSprite, critical);
        }

        public void Fire(Vector2 position, Vector2 direction, int damage, Sprite sprite, bool critical)
        {
            var shot = WorldBuilder.CreateSprite(critical ? "Critical shot" : "Shot", sprite, position, 12,
                _combat.ActorRoot);
            shot.transform.localScale = Vector3.one * (critical ? 0.7f : 0.55f);
            shot.AddComponent<ProjectileActor>().Setup(this, direction, damage, critical);
            GameAudio.Play("bow_shot", 0.6f);
        }

        void MeleeStrike(Vector2 position, Vector2 direction, int damage, Sprite sprite, bool critical)
        {
            var visual = WorldBuilder.CreateSprite(critical ? "Critical melee swing" : "Melee swing", sprite,
                position, 13, _combat.ActorRoot);
            visual.AddComponent<MeleeSwingActor>().Setup(direction, critical);
            GameAudio.Play("swing_whoosh", 0.6f);
            _combat.MeleeTarget(position, direction)?.TakeDamage(damage, position - direction * 0.75f, critical);
        }

        void DropArtifact(Vector2 position)
        {
            string weaponId = _catalog.ArtifactWeaponIds[_weaponCursor++ % _catalog.ArtifactWeaponIds.Length];
            var artifact = ArtifactGenerator.Roll(weaponId, _random);
            DropPickup(position, PickupKind.Artifact, artifact);
        }

        PickupActor DropPickup(Vector2 position, PickupKind kind, Artifact artifact = null)
        {
            if (kind == PickupKind.Artifact)
            {
                var item = WorldBuilder.CreateSprite(artifact.displayName, _catalog.Weapon(artifact.weaponId),
                    position, 7, _combat.ActorRoot).AddComponent<PickupActor>();
                item.Setup(this, kind, artifact);
                return item;
            }
            var catalog = _catalog.Catalog;
            Sprite[] sprites = kind switch
            {
                PickupKind.Coin => catalog.coins,
                PickupKind.Potion => catalog.potions,
                PickupKind.Bomb => catalog.bombs,
                _ => catalog.chests
            };
            Sprite sprite = kind switch
            {
                PickupKind.Potion => sprites.FirstOrDefault(x => x.name == "flask_big_red") ?? sprites[0],
                PickupKind.Chest => sprites.FirstOrDefault(x => x.name == "chest_full_open_anim_f0") ?? sprites[0],
                _ => sprites[0]
            };
            Sprite[] animation = kind is PickupKind.Coin or PickupKind.Bomb ? sprites : null;
            var drop = WorldBuilder.CreateSprite(kind.ToString(), sprite, position, 7, _combat.ActorRoot);
            var pickup = drop.AddComponent<PickupActor>();
            pickup.Setup(this, kind, null, animation);
            return pickup;
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
                    AddCoins(8 + _combat.Wave);
                    GameAudio.Play("chest_open", 0.6f);
                    Toast($"Chest: +{8 + _combat.Wave} coins");
                    break;
                case PickupKind.Bomb:
                    int hit = _combat.DamageWithin(PlayerPosition, BombRadius, 12 + _combat.Wave * 2);
                    PixelBurst.BombBurst(PlayerPosition);
                    GameAudio.Play("bomb_explode", 1f);
                    GameFeel.Shake(0.6f);
                    GameFeel.HitStop(0.09f);
                    Toast($"Bomb blast hit {hit} enemies");
                    break;
                case PickupKind.Artifact:
                    AddArtifact(artifact);
                    GameAudio.Play("artifact_drop", 0.6f);
                    Toast($"Found {artifact.rarity} {artifact.displayName} ({artifact.quality})");
                    break;
            }
            PersistSave();
        }

        public void GameOver()
        {
            _runReady = false;
            _activeSlot.run = null;
            SetMode(GameMode.GameOver);
            GameAudio.Play("game_over", 0.8f);
            PersistSave();
        }


        public void Equip(Artifact artifact)
        {
            if (artifact == null) return;
            _equipped = artifact;
            EquippedId = artifact.id;
            _player?.RefreshWeapon();
            Toast($"Equipped {artifact.displayName}");
            PersistSave();
            _ui.RefreshActive();
        }

        public void ListArtifact(Artifact artifact)
        {
            if (artifact == null) return;
            _market.List(artifact);
        }

        public void BuyListing(MarketListing listing) => _market.Buy(listing);

        public void CancelListing(MarketListing listing) => _market.Cancel(listing);

        public void RefreshMarket() => _market.Refresh();

        public void ClaimMarket() => _market.Claim();

        public void RetryOnlineMarket() => _market.Open();

        public void ChangeVolume(int direction)
        {
            _volumeStep = Mathf.Clamp(_volumeStep + direction, 0, GameAudio.MaxVolumeStep);
            GameAudio.SetVolumeStep(_volumeStep);
        }

        void AddCoins(int amount)
        {
            Coins += amount;
            _market.TrackCoinDelta(amount);
        }

        public void AddArtifact(Artifact artifact)
        {
            if (artifact == null || _activeSlot == null) return;
            _activeSlot.inventory.Add(artifact);
            _ui.RefreshActive();
        }

        public bool RemoveArtifact(Artifact artifact)
        {
            if (artifact == null || _activeSlot == null) return false;
            bool removed = _activeSlot.inventory.Remove(artifact);
            if (removed) _ui.RefreshActive();
            return removed;
        }

        public void Notify(string message) => Toast(message);

        public void RecordTutorialAction(TutorialAction action)
        {
            if (_activeSlot == null || (_activeSlot.tutorialActions & (int)action) == (int)action) return;
            _activeSlot.tutorialActions |= (int)action;
            PersistSave();
        }

        public void HideTutorial() => RecordTutorialAction(TutorialAction.All);

        public void PersistSave()
        {
            if (_runReady && PlayerAlive)
            {
                var checkpoint = new RunCheckpoint
                {
                    seed = _chamberSeed, weaponCursor = _weaponCursor,
                    usesLevelLibrary = _library != null && _library.IsUsable,
                    playerPosition = PlayerPosition, health = _player.Health
                };
                _combat.Capture(checkpoint);
                _activeSlot.run = checkpoint;
            }
            _save.marketJson = _market.Serialize();
            _save.Save();
            _nextCheckpoint = Time.unscaledTime + 5f;
        }

        void RestoreRun(RunCheckpoint checkpoint)
        {
            BuildChamber(checkpoint.seed, checkpoint.wave);
            SpawnPlayer(checkpoint.playerPosition);
            _player.RestoreHealth(checkpoint.health);
            _combat.Restore(checkpoint);
            _weaponCursor = checkpoint.weaponCursor;
            foreach (var pickup in checkpoint.pickups)
                if (pickup.remainingLifetime > 0f)
                    DropPickup(pickup.position, pickup.kind, pickup.artifact)
                        .RestoreLifetime(pickup.remainingLifetime);
            if (_combat.EnemyCount == 0) _world.ExitDoor?.Unlock();
            _runReady = true;
            SetMode(GameMode.InDungeon);
            SetPauseOpen(true);
            Camera.main?.GetComponent<PlayerCenteredCamera>()?.CenterNow();
        }

        void Toast(string message) => _ui.Toast(message);

        void OnDisable() => ReleaseOverlayPause();

        void OnDestroy() => _worldBuilder?.ReleaseTiles();

        void OnApplicationPause(bool paused)
        {
            if (paused) SuspendSession();
            else Cosmetics?.RefreshPurchases();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && Application.isMobilePlatform) SuspendSession();
        }

        void SuspendSession()
        {
            if (_save == null || _market == null) return;
            TouchControls?.ResetInput();
            PersistSave();
            if (_transitioning) _pauseAfterTransition = true;
            else SetPauseOpen(true);
        }

        void OnApplicationQuit()
        {
            if (_save != null && _market != null) PersistSave();
        }


        void StartRun(GameCatalog.CharacterSkin skin)
        {
            EnsureSlotForCharacter(skin.id);
            EnterDungeon();
        }

        void Restart() => ReturnToHub();

        void RunQaArguments()
        {
            const string captureArgument = "--qa-screenshot=";
            const string characterArgument = "--qa-character=";
            const string viewArgument = "--qa-view=";
            string[] arguments = Environment.GetCommandLineArgs();

            string character = arguments
                .FirstOrDefault(x => x.StartsWith(characterArgument, StringComparison.Ordinal));
            if (character != null)
            {
                var skin = _catalog.Catalog.characters
                    .FirstOrDefault(x => x.id == character.Substring(characterArgument.Length));
                if (skin != null) StartRun(skin);
            }
            else if (arguments.Contains("--qa-resume") && _save.ActiveSlotOrNull != null)
                ContinueSlot(_save.activeSlot);

            string view = arguments
                .FirstOrDefault(x => x.StartsWith(viewArgument, StringComparison.Ordinal))?
                .Substring(viewArgument.Length);
            switch (view)
            {
                case "slots":
                    OpenSlotSelect();
                    break;
                case "picker":
                    OpenHeroPicker();
                    break;
                case "cosmetics":
                    if (WorldRunning) SetPauseOpen(true);
                    OpenCosmetics();
                    break;
                case "hub" when _activeSlot != null:
                    BuildHub();
                    break;
                case "game-over" when WorldRunning:
                    GameOver();
                    break;
                case "door" or "transition" or "next-room" or "encounter" when CombatActive:
                    PrepareDoorQa();
                    if (view != "door") BeginNextRoomTransition();
                    break;
            }

            if (arguments.Contains("--qa-inventory") && WorldRunning) SetInventoryOpen(true);
            if (arguments.Contains("--qa-tutorial") && _activeSlot != null)
                _activeSlot.tutorialActions = 0;
            if (arguments.Contains("--qa-market") && WorldRunning)
            {
                OpenMarketOverlay();
                _market.ClaimLocalProceeds();
            }
            if (arguments.Contains("--qa-pause") && WorldRunning) SetPauseOpen(true);

            string capture = arguments
                .FirstOrDefault(x => x.StartsWith(captureArgument, StringComparison.Ordinal));
            if (capture == null) return;
            Application.runInBackground = true;
            float delay = view == "door" ? 1.1f : 0.25f;
            StartCoroutine(CaptureQaFrame(capture.Substring(captureArgument.Length), view, delay));
        }

        void PrepareDoorQa()
        {
            _combat.KillAll();
            if (_player == null || _world?.ExitDoor == null) return;
            _player.transform.position = (Vector3)_world.ExitDoor.Position + Vector3.down * 0.75f;
            Camera.main?.GetComponent<PlayerCenteredCamera>()?.CenterNow();
        }

        IEnumerator CaptureQaFrame(string path, string view, float delay)
        {
            if (view == "transition")
            {
                while (_transitioning && _transitionAmount < 0.74f) yield return null;
            }
            else if (view == "next-room" || view == "encounter")
            {
                while (_transitioning) yield return null;
                if (view == "encounter")
                {
                    var enemies = _combat.ActorRoot.GetComponentsInChildren<EnemyNavigator>();
                    var caster = enemies.FirstOrDefault(enemy => enemy.IsRanged);
                    var melee = enemies.FirstOrDefault(enemy => !enemy.IsRanged);
                    if (caster != null && ProjectilePathClear(PlayerPosition, PlayerPosition + Vector2.right * 3f))
                        caster.transform.position = PlayerPosition + Vector2.right * 3f;
                    if (melee != null && ProjectilePathClear(PlayerPosition, PlayerPosition + Vector2.up * 0.65f))
                        melee.transform.position = PlayerPosition + Vector2.up * 0.65f;
                }
                yield return new WaitForSecondsRealtime(view == "encounter" ? 0.5f : delay);
            }
            else
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            yield return new WaitForEndOfFrame();
            if (_activeSlot != null)
            {
                PersistSave();
                Debug.Log($"[DungeonDash] QA checkpoint: {JsonUtility.ToJson(_activeSlot.run)}");
            }
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[DungeonDash] QA screenshot: {path}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
