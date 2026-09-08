using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    public sealed class CombatDirector
    {
        const int BaseWaveSize = 4;
        const int WaveSizeStep = 2;
        const int MaxWaveSize = 18;
        const float BaseEnemySpeed = 1.5f;
        const float MaxEnemySpeedBonus = 0.7f;
        const float MeleeRange = 1.45f;
        const float MeleeArcDot = 0.2f;
        const float ProjectileHitRadius = 0.45f;

        readonly DungeonGame _game;
        readonly CatalogIndex _catalog;
        readonly List<EnemyActor> _enemies = new();
        readonly List<Vector2> _spawnAnchors = new();
        readonly NavField _navField = new();
        Transform _actorRoot;
        int _enemyCursor;
        bool _clearPending;

        public CombatDirector(DungeonGame game, CatalogIndex catalog)
        {
            _game = game;
            _catalog = catalog;
        }

        public event Action<EnemyActor> EnemyDefeated;
        public event Action ChamberCleared;

        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public int WaveSize { get; private set; }
        public int EnemyCount => _enemies.Count;

        public Transform ActorRoot
        {
            get
            {
                if (_actorRoot == null) _actorRoot = new GameObject("Actors").transform;
                return _actorRoot;
            }
        }

        public void BeginRun()
        {
            Wave = 0;
            Kills = 0;
            WaveSize = 0;
            _clearPending = false;
        }

        public void EnterChamber(BuiltWorld world)
        {
            _navField.SetWalkable(world.Walkable);
            _spawnAnchors.Clear();
            _spawnAnchors.AddRange(world.SpawnAnchors);
            if (_spawnAnchors.Count == 0) _spawnAnchors.Add(world.EntryPoint);
            _clearPending = false;
        }

        public void SpawnWave()
        {
            _clearPending = false;
            Wave++;
            int count = Mathf.Min(BaseWaveSize + Wave * WaveSizeStep, MaxWaveSize);
            WaveSize = count;
            int health = 11 + Wave * 4;

            for (int i = 0; i < count; i++)
            {
                var skin = _catalog.Catalog.enemies[_enemyCursor++ % _catalog.Catalog.enemies.Length];
                if (Wave >= 2 && i % 4 == 0)
                    skin = _catalog.Enemy(Wave % 2 == 0 ? "necro" : "orc_shaman") ?? skin;
                var position = _spawnAnchors[(i * 7 + Wave * 3) % _spawnAnchors.Count];
                SpawnEnemy(skin, position, health);
            }

            GameAudio.Play("wave_start", 0.7f);
        }

        void SpawnEnemy(GameCatalog.EnemySkin skin, Vector2 position, int health)
        {
            float speed = BaseEnemySpeed + Mathf.Min(Wave * 0.04f, MaxEnemySpeedBonus);
            var actorObject = WorldBuilder.CreateSprite(skin.id, skin.idle[0], position, 8, ActorRoot);
            bool ranged = Wave >= 2 && skin.id is "necro" or "orc_shaman";
            actorObject.AddComponent<EnemyNavigator>().Setup(_game, _navField, speed, ranged);
            var enemy = actorObject.AddComponent<EnemyActor>();
            enemy.Setup(_game, skin, health);
            _enemies.Add(enemy);
        }

        public void Capture(RunCheckpoint checkpoint)
        {
            checkpoint.wave = Wave;
            checkpoint.kills = Kills;
            checkpoint.waveSize = WaveSize;
            checkpoint.enemyCursor = _enemyCursor;
            foreach (var enemy in _enemies) checkpoint.enemies.Add(enemy.Capture());
            if (_actorRoot == null) return;
            foreach (var pickup in _actorRoot.GetComponentsInChildren<PickupActor>())
            {
                var state = pickup.Capture();
                if (state != null) checkpoint.pickups.Add(state);
            }
        }

        public void Restore(RunCheckpoint checkpoint)
        {
            Wave = checkpoint.wave;
            Kills = checkpoint.kills;
            WaveSize = checkpoint.waveSize;
            _enemyCursor = checkpoint.enemyCursor;
            _clearPending = checkpoint.enemies.Count == 0;
            foreach (var enemy in checkpoint.enemies)
                SpawnEnemy(_catalog.Enemy(enemy.skinId), enemy.position, enemy.health);
        }

        public void Defeat(EnemyActor enemy)
        {
            if (!_enemies.Remove(enemy)) return;
            Kills++;
            EnemyDefeated?.Invoke(enemy);
            if (_enemies.Count > 0 || _clearPending) return;
            _clearPending = true;
            ChamberCleared?.Invoke();
        }

        public void ClearActors()
        {
            _enemies.Clear();
            if (_actorRoot != null) UnityEngine.Object.Destroy(_actorRoot.gameObject);
            _actorRoot = null;
        }

        public void KillAll()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
                if (_enemies[i] != null) _enemies[i].TakeDamage(9999);
        }

        public bool ProjectilePathClear(Vector2 from, Vector2 to) => _navField.HasLineOfSight(from, to);

        public EnemyActor ProjectileTarget(Vector2 position)
        {
            EnemyActor closest = null;
            float best = ProjectileHitRadius * ProjectileHitRadius;
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

        public EnemyActor MeleeTarget(Vector2 position, Vector2 direction)
        {
            EnemyActor target = null;
            float closest = MeleeRange * MeleeRange;
            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;
                Vector2 offset = (Vector2)enemy.transform.position - position;
                float distance = offset.sqrMagnitude;
                if (distance >= closest || Vector2.Dot(direction, offset.normalized) < MeleeArcDot) continue;
                closest = distance;
                target = enemy;
            }
            return target;
        }

        public int DamageWithin(Vector2 position, float radius, int damage)
        {
            int hit = 0;
            float radiusSquared = radius * radius;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if (enemy == null) continue;
                if (((Vector2)enemy.transform.position - position).sqrMagnitude > radiusSquared) continue;
                enemy.TakeDamage(damage);
                hit++;
            }
            return hit;
        }
    }
}
