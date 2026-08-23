using UnityEngine;

namespace DungeonDash
{
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
            DamageNumberLayer.Spawn(transform.position, damage,
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
}
