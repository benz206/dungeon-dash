using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    public static class GridPathfinder
    {
        static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
        };

        public static bool TryFindNextStep(
            IReadOnlyCollection<Vector2Int> walkable,
            Vector2Int start,
            Vector2Int target,
            out Vector2Int next)
        {
            next = start;
            if (start == target) return true;

            var cells = walkable as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(walkable);
            if (!cells.Contains(start) || !cells.Contains(target)) return false;

            var pending = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            pending.Enqueue(start);
            previous[start] = start;

            while (pending.Count > 0)
            {
                Vector2Int current = pending.Dequeue();
                foreach (var direction in Directions)
                {
                    Vector2Int candidate = current + direction;
                    if (!cells.Contains(candidate) || previous.ContainsKey(candidate)) continue;
                    previous[candidate] = current;
                    if (candidate == target)
                    {
                        while (previous[candidate] != start) candidate = previous[candidate];
                        next = candidate;
                        return true;
                    }
                    pending.Enqueue(candidate);
                }
            }

            return false;
        }

        public static Vector2Int ClosestCell(IReadOnlyCollection<Vector2Int> walkable, Vector2 position)
        {
            var closest = default(Vector2Int);
            float bestDistance = float.MaxValue;
            foreach (var cell in walkable)
            {
                float distance = ((Vector2)cell - position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                closest = cell;
                bestDistance = distance;
            }
            return closest;
        }
    }

    public sealed class EnemyNavigator : MonoBehaviour
    {
        const float AttackDistance = 0.8f;
        const float AttackCooldown = 0.9f;
        const float RepathInterval = 0.15f;

        DungeonGame _game;
        IReadOnlyCollection<Vector2Int> _walkable;
        float _speed;
        float _nextAttack;
        float _nextRepath;
        float _knockbackUntil;
        Vector2 _waypoint;
        Vector2 _knockbackDirection;

        public Vector2 Velocity { get; private set; }

        public void Setup(DungeonGame game, IReadOnlyCollection<Vector2Int> walkable, float speed)
        {
            _game = game;
            _walkable = walkable;
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
            if (_game == null || !_game.PlayerAlive || !_game.WorldRunning) return;

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
                Vector2Int start = GridPathfinder.ClosestCell(_walkable, position);
                Vector2Int target = GridPathfinder.ClosestCell(_walkable, playerPosition);
                _waypoint = GridPathfinder.TryFindNextStep(_walkable, start, target, out Vector2Int next)
                    ? (Vector2)next
                    : position;
                if (start == target) _waypoint = playerPosition;
            }

            Vector2 direction = _waypoint - position;
            if (direction.sqrMagnitude < 0.01f) return;
            Velocity = direction.normalized * _speed;
            transform.position = Vector2.MoveTowards(position, _waypoint, _speed * Time.deltaTime);
        }
    }
}
