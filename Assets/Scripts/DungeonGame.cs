using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonDash
{
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
        PlayerController _player;
        Artifact _equipped;
        int _wave;
        int _enemyCursor;
        int _weaponCursor;
        int _kills;
        bool _choosingCharacter = true;
        bool _gameOver;
        bool _inventoryOpen;
        bool _marketOpen;
        bool _wavePending;
        Artifact _selectedArtifact;
        Vector2 _inventoryScroll;
        Vector2 _marketScroll;
        string _toast = "Choose a hero";
        float _toastUntil;
        GUIStyle _titleStyle;
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

        public bool AcceptsGameplayInput => !_choosingCharacter && !_gameOver && !_inventoryOpen && !_marketOpen;
        public bool WorldRunning => AcceptsGameplayInput;
        public Artifact EquippedArtifact => _equipped;

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
            EnsureStartingInventory();
            SeedMarket();
            ConfigureCamera();
            BuildArena();
        }

        void Start()
        {
            const string captureArgument = "--qa-screenshot=";
            const string characterArgument = "--qa-character=";
            string[] arguments = Environment.GetCommandLineArgs();
            string character = arguments
                .FirstOrDefault(x => x.StartsWith(characterArgument, StringComparison.Ordinal));
            if (character != null)
            {
                string id = character.Substring(characterArgument.Length);
                var skin = _catalog.characters.FirstOrDefault(x => x.id == id);
                if (skin != null) StartRun(skin);
            }
            if (arguments.Contains("--qa-inventory") && !_choosingCharacter)
            {
                _inventoryOpen = true;
                SelectDefaultArtifact();
            }
            if (arguments.Contains("--qa-market") && !_choosingCharacter)
            {
                _marketOpen = true;
                ClaimLocalMarketProceeds();
            }

            string argument = arguments
                .FirstOrDefault(x => x.StartsWith(captureArgument, StringComparison.Ordinal));
            if (argument != null)
                StartCoroutine(CaptureQaFrame(argument.Substring(captureArgument.Length)));
        }

        static IEnumerator CaptureQaFrame(string path)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[DungeonDash] QA screenshot: {path}");
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _choosingCharacter || _gameOver) return;
            if (keyboard.iKey.wasPressedThisFrame)
            {
                _inventoryOpen = !_inventoryOpen;
                _marketOpen = false;
                if (_inventoryOpen) SelectDefaultArtifact();
            }
            if (keyboard.mKey.wasPressedThisFrame)
            {
                _marketOpen = !_marketOpen;
                _inventoryOpen = false;
                if (_marketOpen) OpenMarket();
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _inventoryOpen = false;
                _marketOpen = false;
            }
        }

        void EnsureStartingInventory()
        {
            if (_save.inventory.Count == 0)
            {
                var starter = ArtifactGenerator.Roll(_catalog.weapons[0].id, new System.Random(1));
                starter.displayName = "Starter " + starter.displayName;
                _save.inventory.Add(starter);
                _save.equippedId = starter.id;
            }
            _equipped = _save.inventory.FirstOrDefault(x => x.id == _save.equippedId) ?? _save.inventory[0];
            _save.equippedId = _equipped.id;
            Save();
        }

        void SeedMarket()
        {
            if (_localMarket.Listings.Count > 0) return;
            for (int i = 0; i < Mathf.Min(8, _catalog.weapons.Length); i++)
            {
                var artifact = ArtifactGenerator.Roll(_catalog.weapons[i].id, _random);
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

        void BuildArena()
        {
            var root = new GameObject("Arena").transform;
            var layout = DungeonGenerator.Generate(_random);
            foreach (var cell in layout.Walkable)
            {
                float age = layout.FloorAge[cell];
                var sprite = DungeonTileSelector.SelectFloor(_catalog.floors, age, _random);
                var floor = CreateSprite($"Floor {cell.x},{cell.y} age {age:0.00}", sprite,
                    new Vector2(cell.x, cell.y), -20, root);
                floor.GetComponent<SpriteRenderer>().color = new Color(0.58f, 0.62f, 0.68f);
            }

            foreach (var cell in layout.Walls)
            {
                string spriteName = DungeonTileSelector.WallSpriteName(layout, cell);
                CreateWall(new Vector2(cell.x, cell.y),
                    DungeonTileSelector.FindByName(_catalog.walls, spriteName), spriteName, root);
            }

            foreach (var door in layout.Doors)
            {
                string spriteName = DungeonTileSelector.DoorSpriteName(door.IsOpen);
                var go = CreateSprite(door.IsOpen ? "Door open" : "Door closed",
                    DungeonTileSelector.FindByName(_catalog.walls, spriteName),
                    new Vector2(door.Position.x, door.Position.y), -10, root);
                if (!door.IsOpen) go.AddComponent<BoxCollider2D>();
            }

            string[] banners = { "wall_banner_blue", "wall_banner_green", "wall_banner_red", "wall_banner_yellow" };
            for (int i = 1; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                var position = new Vector2Int(room.Center.x, room.Bounds.yMax);
                if (!layout.Walls.Contains(position)) continue;
                string spriteName = banners[(i - 1) % banners.Length];
                CreateSprite($"Banner {spriteName}", DungeonTileSelector.FindByName(_catalog.walls, spriteName),
                    new Vector2(position.x, position.y), -4, root);
            }

            _enemySpawnPoints.Clear();
            _enemySpawnPoints.AddRange(layout.Walkable
                .Where(cell => cell.sqrMagnitude > 16 && !layout.Corridors.Contains(cell))
                .OrderByDescending(cell => cell.sqrMagnitude)
                .Select(cell => new Vector2(cell.x, cell.y)));
        }

        void CreateWall(Vector2 position, Sprite sprite, string semanticName, Transform parent)
        {
            var go = CreateSprite($"Wall {semanticName}", sprite, position, -5, parent);
            go.GetComponent<SpriteRenderer>().color = new Color(0.68f, 0.72f, 0.78f);
            go.AddComponent<BoxCollider2D>();
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

        void StartRun(GameCatalog.CharacterSkin skin)
        {
            _choosingCharacter = false;
            _save.characterId = skin.id;
            var go = CreateSprite("Player", skin.idle[0], Vector2.zero, 10);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.62f, 0.9f);
            collider.offset = new Vector2(0f, -0.25f);
            _player = go.AddComponent<PlayerController>();
            _player.Setup(this, skin);
            _wave = 0;
            _kills = 0;
            Save();
            SpawnWave();
            Toast("Clear the arena — I: artifacts, M: market");
        }

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
                var enemy = go.AddComponent<EnemyActor>();
                enemy.Setup(this, skin, 11 + _wave * 4, 1.5f + Mathf.Min(_wave * 0.04f, 0.7f));
                _enemies.Add(enemy);
            }
            Toast($"Wave {_wave}");
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
                if (sold > 0) Toast($"Wave clear — {sold} market listing sold!");
                StartCoroutine(NextWave());
                Save();
            }
        }

        IEnumerator NextWave()
        {
            yield return new WaitForSeconds(2.5f);
            if (!_gameOver) SpawnWave();
        }

        void DropArtifact(Vector2 position)
        {
            string weaponId = _catalog.weapons[_weaponCursor++ % _catalog.weapons.Length].id;
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
            var go = CreateSprite(kind.ToString(), sprites[0], position, 7);
            go.AddComponent<PickupActor>().Setup(this, kind, null, sprites);
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
                    Toast($"Bomb blast hit {hit} enemies");
                    break;
                case PickupKind.Artifact:
                    _save.inventory.Add(artifact);
                    Toast($"Found {artifact.rarity} {artifact.displayName} ({artifact.quality})");
                    break;
            }
            Save();
        }

        public void Fire(Vector2 position, Vector2 direction, int damage, Sprite sprite, bool critical)
        {
            var go = CreateSprite(critical ? "Critical shot" : "Shot", sprite, position, 12);
            go.transform.localScale = Vector3.one * (critical ? 0.7f : 0.55f);
            go.AddComponent<ProjectileActor>().Setup(this, direction, damage);
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

        public Sprite WeaponSprite(string id) =>
            _catalog.weapons.FirstOrDefault(x => x.id == id)?.sprite;

        public void GameOver()
        {
            _gameOver = true;
            Save();
        }

        void Restart()
        {
            foreach (var enemy in _enemies.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
            _enemies.Clear();
            if (_player != null) Destroy(_player.gameObject);
            foreach (var pickup in FindObjectsByType<PickupActor>(FindObjectsSortMode.None)) Destroy(pickup.gameObject);
            foreach (var projectile in FindObjectsByType<ProjectileActor>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            _gameOver = false;
            _choosingCharacter = true;
            _inventoryOpen = false;
            _marketOpen = false;
        }

        void Save()
        {
            _save.marketJson = _localMarket.Serialize();
            _save.Save();
        }

        void OnApplicationQuit() => Save();

        void Toast(string message)
        {
            _toast = message;
            _toastUntil = Time.time + 3.5f;
        }

        void AddCoins(int amount)
        {
            _save.coins += amount;
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
                    int initialBalance = Mathf.Max(0, _save.coins - _save.marketPendingCoinDelta);
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
            _save.coins = _onlineMarket.Balance;
            Save();
        }

        void SelectDefaultArtifact()
        {
            if (_selectedArtifact != null && _save.inventory.Contains(_selectedArtifact)) return;
            _selectedArtifact = _equipped ?? _save.inventory.OrderByDescending(x => x.quality).FirstOrDefault();
        }

        void InitStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _titleStyle.normal.textColor = new Color(1f, 0.84f, 0.35f);
            _toastStyle = new GUIStyle(_titleStyle) { fontSize = 23 };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _labelStyle.normal.textColor = Color.white;
            _smallStyle = new GUIStyle(_labelStyle) { fontSize = 14 };
            _smallStyle.normal.textColor = new Color(0.76f, 0.81f, 0.9f);
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 38 };
            _buttonStyle.normal.background = _catalog.buttonUp.texture;
            _buttonStyle.active.background = _catalog.buttonDown.texture;
            _dangerButtonStyle = new GUIStyle(_buttonStyle);
            _dangerButtonStyle.normal.background = _catalog.dangerButtonUp.texture;
            _dangerButtonStyle.active.background = _catalog.dangerButtonDown.texture;
            _sectionStyle = new GUIStyle(_labelStyle) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _sectionStyle.normal.textColor = new Color(0.42f, 0.79f, 1f);
            _mutedStyle = new GUIStyle(_smallStyle) { fontSize = 13 };
            _mutedStyle.normal.textColor = new Color(0.53f, 0.59f, 0.69f);
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

            if (_choosingCharacter)
            {
                DrawCharacterPicker();
                GUI.matrix = previousMatrix;
                return;
            }
            if (_inventoryOpen)
            {
                DrawInventory();
                GUI.matrix = previousMatrix;
                return;
            }
            if (_marketOpen)
            {
                DrawMarket();
                GUI.matrix = previousMatrix;
                return;
            }

            var statusRect = new Rect(18, 18, 410, 112);
            DrawRect(statusRect, new Color(0.025f, 0.04f, 0.075f, 0.96f));
            DrawBorder(statusRect, new Color(0.18f, 0.3f, 0.5f, 0.9f), 2f);
            if (_player != null) DrawHearts(new Rect(28, 28, 30, 30));
            GUI.Label(new Rect(28, 61, 370, 25), $"{_save.coins} COINS     WAVE {_wave}     {_kills} KILLS", _sectionStyle);
            if (_equipped != null)
            {
                DrawRect(new Rect(28, 91, 3, 22), RarityColor(_equipped.rarity));
                GUI.Label(new Rect(39, 88, 375, 28), $"{_equipped.rarity.ToUpperInvariant()}  {_equipped.displayName}  ·  Q{_equipped.quality}", _smallStyle);
            }

            float menuX = _uiWidth - 354f;
            if (GUI.Button(new Rect(menuX, 20, 160, 40), "VAULT  [I]", _buttonStyle))
            {
                _inventoryOpen = true;
                SelectDefaultArtifact();
            }
            if (GUI.Button(new Rect(menuX + 170, 20, 160, 40), "MARKET  [M]", _buttonStyle))
            {
                _marketOpen = true;
                OpenMarket();
            }
            GUI.Label(new Rect(menuX, 67, 330, 46), "WASD move  ·  Mouse aim / fire  ·  Space fire", _centerStyle);

            if (Time.time < _toastUntil)
                GUI.Label(new Rect(_uiWidth / 2f - 300, 72, 600, 36), _toast, _toastStyle);
            if (_gameOver) DrawGameOver();
            GUI.matrix = previousMatrix;
        }

        void DrawHearts(Rect start)
        {
            for (int i = 0; i < _player.MaxHealth / 2; i++)
            {
                int health = _player.Health - i * 2;
                var sprite = health >= 2 ? _catalog.heartFull : health == 1 ? _catalog.heartHalf : _catalog.heartEmpty;
                GUI.DrawTexture(new Rect(start.x + i * 28, start.y, 24, 24), sprite.texture, ScaleMode.ScaleToFit, true);
            }
        }

        void DrawCharacterPicker()
        {
            DrawBackdrop();
            var rect = Centered(1120, 650);
            Panel(rect);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 7), new Color(0.22f, 0.66f, 1f));
            GUI.Label(new Rect(rect.x + 34, rect.y + 22, rect.width - 68, 42), "DUNGEON DASH", _titleStyle);
            GUI.Label(new Rect(rect.x + 34, rect.y + 65, rect.width - 68, 24), "CHOOSE YOUR DELVER  ·  SURVIVE THE ARENA  ·  BUILD A LEGENDARY VAULT", _centerStyle);

            var metaRect = new Rect(rect.x + 34, rect.y + 103, rect.width - 68, 42);
            DrawRect(metaRect, new Color(0.055f, 0.08f, 0.13f, 0.95f));
            GUI.Label(new Rect(metaRect.x + 16, metaRect.y, 310, metaRect.height), $"{_save.coins} COINS BANKED", _sectionStyle);
            GUI.Label(new Rect(metaRect.x + 335, metaRect.y, 310, metaRect.height), $"{_save.inventory.Count} ARTIFACTS SECURED", _sectionStyle);
            GUI.Label(new Rect(metaRect.x + 650, metaRect.y, 390, metaRect.height),
                _equipped == null ? "NO ARTIFACT EQUIPPED" : $"STARTING WITH  {_equipped.displayName.ToUpperInvariant()}  ·  Q{_equipped.quality}", _rightStyle);

            const float gap = 12f;
            float cardWidth = (rect.width - 68f - gap * 5f) / 6f;
            const float cardHeight = 188f;
            float cardsY = rect.y + 162f;
            for (int i = 0; i < _catalog.characters.Length; i++)
            {
                var skin = _catalog.characters[i];
                int column = i % 6;
                int row = i / 6;
                var card = new Rect(rect.x + 34f + column * (cardWidth + gap), cardsY + row * (cardHeight + gap), cardWidth, cardHeight);
                bool hovered = card.Contains(Event.current.mousePosition);
                DrawRect(card, hovered ? new Color(0.09f, 0.16f, 0.25f, 0.98f) : new Color(0.045f, 0.07f, 0.115f, 0.98f));
                DrawBorder(card, hovered ? new Color(0.3f, 0.75f, 1f) : new Color(0.13f, 0.22f, 0.36f), hovered ? 2f : 1f);
                var preview = new Rect(card.x + (card.width - 86f) / 2f, card.y + 12f, 86f, 96f);
                GUI.DrawTexture(preview, skin.idle[0].texture, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(card.x + 8, card.y + 111, card.width - 16, 26), Pretty(skin.id).ToUpperInvariant(), _centerStyle);
                GUI.Label(new Rect(card.x + 8, card.y + 139, card.width - 16, 20), $"MOVE  {skin.speed:0.0}", _microStyle);
                GUI.Label(new Rect(card.x + 8, card.y + 159, card.width - 16, 20), "SELECT HERO", _centerStyle);
                if (GUI.Button(card, GUIContent.none, _clickStyle)) StartRun(skin);
            }

            GUI.Label(new Rect(rect.x + 34, rect.yMax - 40, rect.width - 68, 22),
                "Every run grows your persistent collection. Press I in the arena to compare and equip your finds.", _centerStyle);
        }

        void DrawInventory()
        {
            DrawBackdrop();
            SelectDefaultArtifact();
            var rect = Centered(1080, 650);
            Panel(rect);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 7), new Color(0.22f, 0.66f, 1f));
            GUI.Label(new Rect(rect.x + 30, rect.y + 18, 500, 38), "THE VAULT", _titleStyle);
            GUI.Label(new Rect(rect.x + 32, rect.y + 58, 570, 22), "LOADOUT & ARTIFACTS  ·  STRONGEST FINDS FIRST", _sectionStyle);
            GUI.Label(new Rect(rect.x + 630, rect.y + 25, 250, 32), $"{_save.inventory.Count} ARTIFACTS   ·   {_save.coins} COINS", _rightStyle);
            if (GUI.Button(new Rect(rect.xMax - 164, rect.y + 20, 132, 38), "CLOSE  [I]", _buttonStyle)) _inventoryOpen = false;

            var listRect = new Rect(rect.x + 30, rect.y + 96, 590, 500);
            var detailRect = new Rect(rect.x + 640, rect.y + 96, 410, 500);
            DrawRect(listRect, new Color(0.027f, 0.045f, 0.075f, 0.98f));
            DrawBorder(listRect, new Color(0.12f, 0.21f, 0.34f), 1f);
            DrawRect(new Rect(listRect.x, listRect.y, listRect.width, 42), new Color(0.055f, 0.09f, 0.145f));
            GUI.Label(new Rect(listRect.x + 14, listRect.y, 300, 42), "YOUR COLLECTION", _sectionStyle);
            GUI.Label(new Rect(listRect.x + 330, listRect.y, 240, 42), "SELECT TO INSPECT", _rightStyle);

            var artifacts = _save.inventory
                .OrderByDescending(x => x.id == _equipped.id)
                .ThenByDescending(x => x.quality)
                .ToArray();
            var viewport = new Rect(listRect.x + 8, listRect.y + 50, listRect.width - 16, listRect.height - 58);
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
                if (selected) DrawBorder(row, new Color(0.27f, 0.68f, 1f), 1f);
                var sprite = WeaponSprite(artifact.weaponId);
                if (sprite != null) GUI.DrawTexture(new Rect(row.x + 15, row.y + 10, 52, 52), sprite.texture, ScaleMode.ScaleToFit, true);
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
            DrawRect(rect, new Color(0.035f, 0.055f, 0.09f, 0.99f));
            DrawBorder(rect, new Color(0.13f, 0.24f, 0.39f), 1f);
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
            if (sprite != null) GUI.DrawTexture(new Rect(preview.x + 14, preview.y + 14, 84, 84), sprite.texture, ScaleMode.ScaleToFit, true);

            GUI.Label(new Rect(rect.x + 160, rect.y + 88, rect.width - 184, 22), "ROLL QUALITY", _microStyle);
            GUI.Label(new Rect(rect.x + 160, rect.y + 111, rect.width - 184, 43), $"{artifact.quality}", _titleStyle);
            DrawRect(new Rect(rect.x + 160, rect.y + 165, rect.width - 186, 8), new Color(0.015f, 0.025f, 0.045f));
            DrawRect(new Rect(rect.x + 160, rect.y + 165, (rect.width - 186) * artifact.quality / 100f, 8), rarity);
            GUI.Label(new Rect(rect.x + 160, rect.y + 179, rect.width - 184, 20), $"VALUE  {artifact.Price} COINS", _mutedStyle);

            float statsY = rect.y + 222f;
            DrawStat(new Rect(rect.x + 24, statsY, rect.width - 48, 48), "DAMAGE", artifact.damage.ToString(), artifact.damage / 30f, rarity);
            DrawStat(new Rect(rect.x + 24, statsY + 58, rect.width - 48, 48), "ATTACK SPEED", $"{artifact.attacksPerSecond:0.00} / SEC", artifact.attacksPerSecond / 3.5f, rarity);
            DrawStat(new Rect(rect.x + 24, statsY + 116, rect.width - 48, 48), "CRITICAL CHANCE", $"{artifact.criticalChance * 100f:0}%", artifact.criticalChance / 0.3f, rarity);

            bool equipped = artifact.id == _equipped.id;
            GUI.enabled = !equipped;
            string equipLabel = equipped ? "EQUIPPED" : "EQUIP ARTIFACT";
            if (GUI.Button(new Rect(rect.x + 24, rect.yMax - 86, 174, 42), equipLabel, _buttonStyle)) Equip(artifact);
            GUI.enabled = !equipped && !_onlineMarket.Busy;
            if (GUI.Button(new Rect(rect.x + 212, rect.yMax - 86, 174, 42), $"LIST  ·  {artifact.Price}", _dangerButtonStyle)) ListArtifact(artifact);
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
            var rect = Centered(820, 690);
            Panel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 25, rect.y + 18, rect.width - 50, rect.height - 36));
            GUILayout.Label("GLOBAL ARTIFACT MARKET", _titleStyle);
            GUILayout.Label(_onlineMarket.Status, _smallStyle);
            string pending = _useOnlineMarket && _onlineMarket.PendingCoins > 0
                ? $" · {_onlineMarket.PendingCoins} proceeds ready"
                : string.Empty;
            GUILayout.Label($"Balance: {_save.coins} coins{pending}", _labelStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = !_onlineMarket.Busy;
            if (_useOnlineMarket)
            {
                if (GUILayout.Button("Refresh", _buttonStyle)) RefreshOnlineMarket();
                if (GUILayout.Button("Claim proceeds", _buttonStyle)) ClaimOnlineMarket();
            }
            else if (GUILayout.Button("Retry online", _buttonStyle)) OpenMarket();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            _marketScroll = GUILayout.BeginScrollView(_marketScroll);
            var listings = _useOnlineMarket ? _onlineMarket.Listings : _localMarket.Listings;
            foreach (var listing in listings.ToArray())
            {
                var artifact = listing.artifact;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(new GUIContent(WeaponSprite(artifact.weaponId).texture), GUILayout.Width(48), GUILayout.Height(48));
                GUILayout.BeginVertical();
                GUILayout.Label($"{artifact.rarity} {artifact.displayName} · Q{artifact.quality}", _labelStyle);
                GUILayout.Label(artifact.Stats + $" · {listing.price} coins", _smallStyle);
                GUILayout.EndVertical();
                if (IsOwnListing(listing))
                {
                    GUI.enabled = !_onlineMarket.Busy;
                    if (GUILayout.Button("Cancel", _buttonStyle, GUILayout.Width(90))) CancelListing(listing);
                    GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = !_onlineMarket.Busy && _save.coins >= listing.price;
                    if (GUILayout.Button("Buy", _buttonStyle, GUILayout.Width(90))) BuyListing(listing);
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close (M)", _buttonStyle)) _marketOpen = false;
            GUILayout.EndArea();
        }

        void DrawGameOver()
        {
            var rect = Centered(480, 260);
            Panel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 30, rect.y + 25, rect.width - 60, rect.height - 50));
            GUILayout.Label("RUN ENDED", _titleStyle);
            GUILayout.Label($"Reached wave {_wave} with {_kills} kills.\nArtifacts and coins are saved.", _labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Choose Hero & Run Again", _dangerButtonStyle)) Restart();
            GUILayout.EndArea();
        }

        void Equip(Artifact artifact)
        {
            _equipped = artifact;
            _save.equippedId = artifact.id;
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
                _save.inventory.Remove(artifact);
                _localMarket.List(artifact, artifact.Price);
                Toast($"Online unavailable — listed {artifact.displayName} locally");
                Save();
                return;
            }

            try
            {
                await _onlineMarket.ListAsync(artifact, artifact.Price);
                _save.inventory.Remove(artifact);
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
                int oldBalance = _save.coins;
                var localArtifact = _localMarket.Buy(listing.id, ref _save.coins);
                if (localArtifact == null) return;
                if (_save.marketAccountInitialized)
                    _save.marketPendingCoinDelta += _save.coins - oldBalance;
                _save.inventory.Add(localArtifact);
                Toast($"Bought {localArtifact.displayName} locally");
                Save();
                return;
            }

            try
            {
                var response = await _onlineMarket.BuyAsync(listing.id);
                if (response.artifact != null) _save.inventory.Add(response.artifact);
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
                _save.inventory.Add(localArtifact);
                Toast($"Returned {localArtifact.displayName}");
                Save();
                return;
            }

            try
            {
                var response = await _onlineMarket.CancelAsync(listing.id);
                if (response.artifact != null) _save.inventory.Add(response.artifact);
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

        void Panel(Rect rect)
        {
            DrawRect(rect, new Color(0.025f, 0.04f, 0.07f, 0.99f));
            DrawBorder(rect, new Color(0.15f, 0.27f, 0.44f), 2f);
        }

        void DrawBackdrop()
        {
            DrawRect(new Rect(0, 0, _uiWidth, _uiHeight), new Color(0.008f, 0.015f, 0.03f, 0.82f));
            DrawRect(new Rect(0, 0, _uiWidth, 4), new Color(0.13f, 0.48f, 0.78f, 0.8f));
        }

        static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        static Color RarityColor(string rarity) => rarity switch
        {
            "Mythic" => new Color(1f, 0.58f, 0.18f),
            "Epic" => new Color(0.72f, 0.4f, 1f),
            "Rare" => new Color(0.2f, 0.68f, 1f),
            _ => new Color(0.55f, 0.65f, 0.74f)
        };

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
        DungeonGame _game;
        GameCatalog.EnemySkin _skin;
        SpriteRenderer _renderer;
        int _health;
        float _speed;
        float _animationTime;
        float _nextHit;

        public void Setup(DungeonGame game, GameCatalog.EnemySkin skin, int health, float speed)
        {
            _game = game;
            _skin = skin;
            _health = health;
            _speed = speed;
            _renderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (!_game.PlayerAlive || !_game.WorldRunning) return;
            Vector2 delta = _game.PlayerPosition - (Vector2)transform.position;
            if (delta.magnitude > 0.85f)
                transform.position += (Vector3)(delta.normalized * (_speed * Time.deltaTime));
            else if (Time.time >= _nextHit)
            {
                _nextHit = Time.time + 0.9f;
                _game.HurtPlayer(1);
            }
            _renderer.flipX = delta.x < 0f;
            _animationTime += Time.deltaTime;
            var frames = delta.magnitude > 0.9f && _skin.run.Length > 0 ? _skin.run : _skin.idle;
            if (frames.Length > 0) _renderer.sprite = frames[Mathf.FloorToInt(_animationTime * 8f) % frames.Length];
        }

        public void TakeDamage(int damage)
        {
            _health -= damage;
            if (_health > 0) return;
            _game.EnemyDied(this);
            Destroy(gameObject);
        }
    }

    public sealed class ProjectileActor : MonoBehaviour
    {
        DungeonGame _game;
        Vector2 _direction;
        int _damage;
        float _expires;

        public void Setup(DungeonGame game, Vector2 direction, int damage)
        {
            _game = game;
            _direction = direction;
            _damage = damage;
            _expires = Time.time + 1.6f;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 45f);
        }

        void Update()
        {
            transform.position += (Vector3)(_direction * (11f * Time.deltaTime));
            var target = _game.ProjectileTarget(transform.position);
            if (target != null)
            {
                target.TakeDamage(_damage);
                Destroy(gameObject);
            }
            else if (Time.time >= _expires) Destroy(gameObject);
        }
    }

    public sealed class PickupActor : MonoBehaviour
    {
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
            if (_frames != null && _frames.Length > 0)
                _renderer.sprite = _frames[Mathf.FloorToInt(Time.time * 8f) % _frames.Length];
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.07f);
            if (((Vector2)transform.position - _game.PlayerPosition).sqrMagnitude < 0.7f * 0.7f)
            {
                _game.Collect(_kind, _artifact);
                Destroy(gameObject);
            }
            else if (Time.time - _spawnTime > 25f) Destroy(gameObject);
        }
    }
}
