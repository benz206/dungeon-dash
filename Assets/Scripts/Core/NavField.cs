using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    public sealed class NavField
    {
        const float RebuildInterval = 0.15f;
        const int NearestCellSearchRadius = 3;

        static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
        };

        readonly Dictionary<Vector2Int, Vector2Int> _step = new();
        readonly Queue<Vector2Int> _frontier = new();
        HashSet<Vector2Int> _walkable = new();
        Vector2Int _source;
        float _nextRebuild = float.NegativeInfinity;
        bool _built;

        public void SetWalkable(HashSet<Vector2Int> walkable)
        {
            _walkable = walkable;
            _step.Clear();
            _built = false;
            _nextRebuild = float.NegativeInfinity;
        }

        public void EnsureFresh(Vector2 target)
        {
            if (Time.time < _nextRebuild) return;
            _nextRebuild = Time.time + RebuildInterval;
            if (!TryNearestCell(target, out var source)) return;
            if (_built && source == _source) return;
            Rebuild(source);
        }

        public void Rebuild(Vector2Int source)
        {
            _source = source;
            _built = true;
            _step.Clear();
            _frontier.Clear();
            if (!_walkable.Contains(source)) return;

            _step[source] = source;
            _frontier.Enqueue(source);
            while (_frontier.Count > 0)
            {
                var cell = _frontier.Dequeue();
                foreach (var direction in Directions)
                {
                    var neighbor = cell + direction;
                    if (!_walkable.Contains(neighbor) || _step.ContainsKey(neighbor)) continue;
                    _step[neighbor] = cell;
                    _frontier.Enqueue(neighbor);
                }
            }
        }

        public bool TryWaypoint(Vector2 from, Vector2 target, out Vector2 waypoint)
        {
            waypoint = from;
            if (!TryNearestCell(from, out var cell)) return false;
            if (cell == _source)
            {
                waypoint = target;
                return true;
            }
            if (!_step.TryGetValue(cell, out var next)) return false;
            waypoint = next;
            return true;
        }

        public bool TryNearestCell(Vector2 position, out Vector2Int cell)
        {
            cell = Vector2Int.RoundToInt(position);
            if (_walkable.Contains(cell)) return true;

            for (int radius = 1; radius <= NearestCellSearchRadius; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                var candidate = cell + new Vector2Int(x, y);
                if (!_walkable.Contains(candidate)) continue;
                cell = candidate;
                return true;
            }

            return false;
        }
    }
}
