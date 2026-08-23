using UnityEngine;

namespace DungeonDash
{
    public sealed class EnemyNavigator : MonoBehaviour
    {
        const float AttackDistance = 0.8f;
        const float AttackCooldown = 0.9f;
        const float RepathInterval = 0.15f;

        DungeonGame _game;
        NavField _field;
        float _speed;
        float _nextAttack;
        float _nextRepath;
        float _knockbackUntil;
        Vector2 _waypoint;
        Vector2 _knockbackDirection;

        public Vector2 Velocity { get; private set; }

        public void Setup(DungeonGame game, NavField field, float speed)
        {
            _game = game;
            _field = field;
            _speed = speed;
            _waypoint = transform.position;
        }

        public void KnockbackFrom(Vector2 sourcePosition)
        {
            Vector2 away = (Vector2)transform.position - sourcePosition;
            if (away.sqrMagnitude < 0.01f) return;
            _knockbackDirection = away.normalized;
            _knockbackUntil = Time.time + 0.14f;
        }

        void Update()
        {
            Velocity = Vector2.zero;
            if (_game == null || !_game.PlayerAlive || !_game.CombatActive) return;

            if (Time.time < _knockbackUntil)
            {
                Velocity = _knockbackDirection * 3.5f;
                transform.position += (Vector3)(Velocity * Time.deltaTime);
                return;
            }

            Vector2 position = transform.position;
            Vector2 playerPosition = _game.PlayerPosition;
            Vector2 toPlayer = playerPosition - position;
            if (toPlayer.sqrMagnitude <= AttackDistance * AttackDistance)
            {
                if (Time.time >= _nextAttack)
                {
                    _nextAttack = Time.time + AttackCooldown;
                    _game.HurtPlayer(1, transform.position);
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
    }
}
