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
        LocalMarketService _market;
        readonly System.Random _random = new();
        readonly List<EnemyActor> _enemies = new();
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
        Vector2 _inventoryScroll;
        Vector2 _marketScroll;
        string _toast = "Choose a hero";
        float _toastUntil;
        GUIStyle _titleStyle;
        GUIStyle _labelStyle;
        GUIStyle _smallStyle;
        GUIStyle _buttonStyle;
        GUIStyle _boxStyle;

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
            _market = new LocalMarketService(_save.marketJson);
            EnsureStartingInventory();
            SeedMarket();
            ConfigureCamera();
            BuildArena();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _choosingCharacter || _gameOver) return;
            if (keyboard.iKey.wasPressedThisFrame)
            {
                _inventoryOpen = !_inventoryOpen;
                _marketOpen = false;
            }
            if (keyboard.mKey.wasPressedThisFrame)
            {
                _marketOpen = !_marketOpen;
                _inventoryOpen = false;
                if (_marketOpen) ClaimMarketProceeds();
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
            if (_market.Listings.Count > 0) return;
            for (int i = 0; i < Mathf.Min(8, _catalog.weapons.Length); i++)
            {
                var artifact = ArtifactGenerator.Roll(_catalog.weapons[i].id, _random);
                _market.AddNpcListing(artifact, artifact.Price);
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
            for (int y = -6; y <= 6; y++)
            for (int x = -11; x <= 11; x++)
            {
                var sprite = _catalog.floors[Math.Abs(x * 7 + y * 13) % _catalog.floors.Length];
                CreateSprite($"Floor {x},{y}", sprite, new Vector2(x, y), -20, root);
            }

            int wallIndex = 0;
            for (int x = -12; x <= 12; x++)
            {
                CreateWall(new Vector2(x, -7), wallIndex++, root);
                CreateWall(new Vector2(x, 7), wallIndex++, root);
            }
            for (int y = -6; y <= 6; y++)
            {
                CreateWall(new Vector2(-12, y), wallIndex++, root);
                CreateWall(new Vector2(12, y), wallIndex++, root);
            }
        }

        void CreateWall(Vector2 position, int index, Transform parent)
        {
            var go = CreateSprite("Wall", _catalog.walls[index % _catalog.walls.Length], position, -5, parent);
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
                float angle = (i / (float)count) * Mathf.PI * 2f;
                var position = new Vector2(Mathf.Cos(angle) * 9.5f, Mathf.Sin(angle) * 5.2f);
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
            _save.coins += 1 + _wave / 3;
            if (_kills % 3 == 0) DropArtifact(enemy.transform.position);
            else if (_random.NextDouble() < 0.18d) DropPickup(enemy.transform.position, PickupKind.Potion);
            else if (_random.NextDouble() < 0.35d) DropPickup(enemy.transform.position, PickupKind.Coin);

            if (_enemies.Count == 0 && !_wavePending)
            {
                _wavePending = true;
                int sold = _market.SimulateSales(_random);
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
                    _save.coins += 3;
                    Toast("+3 coins");
                    break;
                case PickupKind.Potion:
                    _player.Heal(3);
                    Toast("Restored 3 hearts");
                    break;
                case PickupKind.Chest:
                    _save.coins += 8 + _wave;
                    Toast($"Chest: +{8 + _wave} coins");
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
            go.transform.localScale = critical ? Vector3.one * 1.25f : Vector3.one;
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
            _save.marketJson = _market.Serialize();
            _save.Save();
        }

        void OnApplicationQuit() => Save();

        void Toast(string message)
        {
            _toast = message;
            _toastUntil = Time.time + 3.5f;
        }

        void ClaimMarketProceeds()
        {
            int coins = _market.ClaimProceeds();
            if (coins <= 0) return;
            _save.coins += coins;
            Toast($"Collected {coins} market coins");
            Save();
        }

        void InitStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _titleStyle.normal.textColor = new Color(1f, 0.84f, 0.35f);
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _labelStyle.normal.textColor = Color.white;
            _smallStyle = new GUIStyle(_labelStyle) { fontSize = 14 };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 38 };
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            InitStyles();
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.055f, 0.09f, 0.94f);
            GUI.Box(new Rect(14, 14, 355, 114), GUIContent.none, _boxStyle);
            GUI.color = previous;
            if (_player != null) DrawHearts(new Rect(28, 28, 30, 30));
            GUI.Label(new Rect(28, 66, 330, 28), $"Coins  {_save.coins}     Wave  {_wave}     Kills  {_kills}", _labelStyle);
            GUI.Label(new Rect(28, 94, 330, 24), _equipped == null ? "No artifact" : $"{_equipped.rarity} {_equipped.displayName}  {_equipped.Stats}", _smallStyle);
            GUI.Label(new Rect(Screen.width - 350, 22, 330, 70), "WASD move · Mouse aim/fire · Space fire\nI artifacts · M global market · Esc close", _smallStyle);

            if (Time.time < _toastUntil)
                GUI.Label(new Rect(Screen.width / 2f - 330, 22, 660, 42), _toast, _titleStyle);
            if (_choosingCharacter) DrawCharacterPicker();
            else if (_gameOver) DrawGameOver();
            else if (_inventoryOpen) DrawInventory();
            else if (_marketOpen) DrawMarket();
        }

        void DrawHearts(Rect start)
        {
            for (int i = 0; i < _player.MaxHealth; i++)
            {
                var sprite = i < _player.Health ? _catalog.heartFull : _catalog.heartEmpty;
                GUI.DrawTexture(new Rect(start.x + i * 28, start.y, 24, 24), sprite.texture, ScaleMode.ScaleToFit, true);
            }
        }

        void DrawCharacterPicker()
        {
            var rect = Centered(920, 430);
            Panel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 28, rect.y + 24, rect.width - 56, rect.height - 48));
            GUILayout.Label("DUNGEON DASH", _titleStyle);
            GUILayout.Label("Choose your hero", _labelStyle);
            GUILayout.Space(18);
            GUILayout.BeginHorizontal();
            foreach (var skin in _catalog.characters)
            {
                GUILayout.BeginVertical(GUILayout.Width(130));
                GUILayout.Label(new GUIContent(skin.idle[0].texture), GUILayout.Width(96), GUILayout.Height(120));
                if (GUILayout.Button(Pretty(skin.id), _buttonStyle)) StartRun(skin);
                GUILayout.Label($"Speed {skin.speed:0.0}", _smallStyle);
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("A compact single-player arena with a persistent, multiplayer-ready artifact market.", _smallStyle);
            GUILayout.EndArea();
        }

        void DrawInventory()
        {
            var rect = Centered(760, 650);
            Panel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 25, rect.y + 20, rect.width - 50, rect.height - 40));
            GUILayout.Label("ARTIFACTS", _titleStyle);
            GUILayout.Label("Equip a find or list it on the market. Better rolls are exponentially rarer.", _smallStyle);
            _inventoryScroll = GUILayout.BeginScrollView(_inventoryScroll);
            foreach (var artifact in _save.inventory.ToArray())
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(new GUIContent(WeaponSprite(artifact.weaponId).texture), GUILayout.Width(48), GUILayout.Height(48));
                GUILayout.BeginVertical();
                GUILayout.Label($"{artifact.rarity} {artifact.displayName}  ·  Quality {artifact.quality}", _labelStyle);
                GUILayout.Label(artifact.Stats + $"  ·  Value {artifact.Price}", _smallStyle);
                GUILayout.EndVertical();
                if (artifact.id == _equipped.id) GUILayout.Label("EQUIPPED", _smallStyle, GUILayout.Width(92));
                else if (GUILayout.Button("Equip", _buttonStyle, GUILayout.Width(82))) Equip(artifact);
                GUI.enabled = artifact.id != _equipped.id;
                if (GUILayout.Button("List", _buttonStyle, GUILayout.Width(82))) ListArtifact(artifact);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close (I)", _buttonStyle)) _inventoryOpen = false;
            GUILayout.EndArea();
        }

        void DrawMarket()
        {
            var rect = Centered(820, 690);
            Panel(rect);
            GUILayout.BeginArea(new Rect(rect.x + 25, rect.y + 18, rect.width - 50, rect.height - 36));
            GUILayout.Label("GLOBAL ARTIFACT MARKET", _titleStyle);
            GUILayout.Label("Local simulation for now · service boundary is ready for an authoritative online backend.", _smallStyle);
            GUILayout.Label($"Balance: {_save.coins} coins", _labelStyle);
            _marketScroll = GUILayout.BeginScrollView(_marketScroll);
            foreach (var listing in _market.Listings.ToArray())
            {
                var artifact = listing.artifact;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(new GUIContent(WeaponSprite(artifact.weaponId).texture), GUILayout.Width(48), GUILayout.Height(48));
                GUILayout.BeginVertical();
                GUILayout.Label($"{artifact.rarity} {artifact.displayName} · Q{artifact.quality}", _labelStyle);
                GUILayout.Label(artifact.Stats + $" · {listing.price} coins", _smallStyle);
                GUILayout.EndVertical();
                if (listing.sellerId == LocalMarketService.PlayerSeller)
                {
                    if (GUILayout.Button("Cancel", _buttonStyle, GUILayout.Width(90))) CancelListing(listing);
                }
                else
                {
                    GUI.enabled = _save.coins >= listing.price;
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
            if (GUILayout.Button("Choose Hero & Run Again", _buttonStyle)) Restart();
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
            _save.inventory.Remove(artifact);
            _market.List(artifact, artifact.Price);
            Toast($"Listed {artifact.displayName} for {artifact.Price} coins");
            Save();
        }

        void BuyListing(MarketListing listing)
        {
            var artifact = _market.Buy(listing.id, ref _save.coins);
            if (artifact == null) return;
            _save.inventory.Add(artifact);
            Toast($"Bought {artifact.displayName}");
            Save();
        }

        void CancelListing(MarketListing listing)
        {
            var artifact = _market.Cancel(listing.id);
            if (artifact == null) return;
            _save.inventory.Add(artifact);
            Toast($"Returned {artifact.displayName}");
            Save();
        }

        void Panel(Rect rect)
        {
            var previous = GUI.color;
            GUI.color = new Color(0.035f, 0.05f, 0.085f, 0.98f);
            GUI.Box(rect, GUIContent.none, _boxStyle);
            GUI.color = previous;
        }

        static Rect Centered(float width, float height) =>
            new((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        static string Pretty(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    public enum PickupKind { Coin, Potion, Chest, Artifact }

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
