using UnityEngine;

namespace DungeonDash
{
    public sealed class EnemyNavigator : MonoBehaviour
    {
        const float AttackDistance = 0.8f;
        const float RepathInterval = 0.15f;

        DungeonGame _game;
        NavField _field;
        float _speed;
        float _nextAttack;
        float _nextRepath;
        float _knockbackUntil;
        Vector2 _waypoint;
        Vector2 _knockbackDirection;
        bool _ranged;
        float _windupUntil;
        Vector2 _attackTarget;
        AttackTelegraph _telegraph;

        public Vector2 Velocity { get; private set; }
        public bool IsRanged => _ranged;
        public bool IsWindingUp => _windupUntil > 0f;

        public void Setup(DungeonGame game, NavField field, float speed, bool ranged = false)
        {
            _game = game;
            _field = field;
            _speed = speed;
            _waypoint = transform.position;
            _ranged = ranged;
            _nextAttack = Time.time + 0.7f;
            _telegraph = new AttackTelegraph(transform, ranged);
        }

        public void KnockbackFrom(Vector2 sourcePosition)
        {
            Vector2 away = (Vector2)transform.position - sourcePosition;
            if (away.sqrMagnitude < 0.01f) return;
            _knockbackDirection = away.normalized;
            _knockbackUntil = Time.time + 0.14f;
            _windupUntil = 0f;
            _nextAttack = Mathf.Max(_nextAttack, Time.time + 0.35f);
            _telegraph?.Hide();
        }

        void Update()
        {
            Velocity = Vector2.zero;
            if (_game == null || !_game.PlayerAlive || !_game.CombatActive) return;

            if (Time.time < _knockbackUntil)
            {
                Velocity = _knockbackDirection * 3.5f;
                Vector2 next = (Vector2)transform.position + Velocity * Time.deltaTime;
                if (_field.HasLineOfSight(transform.position, next)) transform.position = next;
                return;
            }

            Vector2 position = transform.position;
            Vector2 playerPosition = _game.PlayerPosition;
            Vector2 toPlayer = playerPosition - position;
            if (IsWindingUp)
            {
                float duration = _ranged ? 0.8f : 0.45f;
                _telegraph.Show(position, _attackTarget, 1f - (_windupUntil - Time.time) / duration);
                if (Time.time >= _windupUntil)
                {
                    _windupUntil = 0f;
                    _nextAttack = Time.time + (_ranged ? 1.4f : 0.6f);
                    _telegraph.Hide();
                    if (_ranged) EnemyBolt.Fire(_game, position, _attackTarget, transform.parent);
                    else if (toPlayer.sqrMagnitude <= AttackDistance * AttackDistance &&
                        _field.HasLineOfSight(position, playerPosition)) _game.HurtPlayer(1, position);
                }
                return;
            }

            float range = _ranged ? 6f : AttackDistance;
            if (toPlayer.sqrMagnitude <= range * range && _field.HasLineOfSight(position, playerPosition))
            {
                if (Time.time >= _nextAttack)
                {
                    _attackTarget = playerPosition;
                    _windupUntil = Time.time + (_ranged ? 0.8f : 0.45f);
                    _telegraph.Show(position, _attackTarget, 0f);
                }
                return;
            }

            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + RepathInterval;
                _field.EnsureFresh(playerPosition);
                if (!_field.TryWaypoint(position, playerPosition, out _waypoint)) _waypoint = position;
            }

            Vector2 direction = _waypoint - position;
            if (direction.sqrMagnitude < 0.01f) return;
            Velocity = direction.normalized * _speed;
            transform.position = Vector2.MoveTowards(position, _waypoint, _speed * Time.deltaTime);
        }

        void OnDisable() => _telegraph?.Hide();
        void OnDestroy() => _telegraph?.Destroy();
    }
}
