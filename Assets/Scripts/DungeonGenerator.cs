using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonDash
{
    public sealed class DungeonRoom
    {
        public RectInt Bounds { get; }
        public float Age { get; }
        public Vector2Int Center => new(Bounds.xMin + Bounds.width / 2, Bounds.yMin + Bounds.height / 2);

        public DungeonRoom(RectInt bounds, float age)
        {
            Bounds = bounds;
            Age = age;
        }
    }

    public readonly struct DungeonDoor
    {
        public Vector2Int Position { get; }
        public bool IsOpen { get; }

        public DungeonDoor(Vector2Int position, bool isOpen)
        {
            Position = position;
            IsOpen = isOpen;
        }
    }

    public sealed class DungeonLayout
    {
        public List<DungeonRoom> Rooms { get; } = new();
        public HashSet<Vector2Int> Walkable { get; } = new();
        public HashSet<Vector2Int> Corridors { get; } = new();
        public HashSet<Vector2Int> Walls { get; } = new();
        public List<DungeonDoor> Doors { get; } = new();
        public Dictionary<Vector2Int, float> FloorAge { get; } = new();
    }

    public static class DungeonGenerator
    {
        const int SatelliteRoomCount = 6;

        static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
        };

        static readonly Vector2Int[] RoomAnchors =
        {
            new(-14, -10), new(0, -10), new(14, -10),
            new(-14, 0),                    new(14, 0),
            new(-14, 10),  new(0, 10),     new(14, 10)
        };

        public static DungeonLayout Generate(System.Random random)
        {
            var layout = new DungeonLayout();
            AddRoom(layout, new RectInt(-3, -3, 7, 7), random);

            var anchors = RoomAnchors.ToArray();
            for (int i = anchors.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (anchors[i], anchors[swapIndex]) = (anchors[swapIndex], anchors[i]);
            }

            for (int i = 0; i < SatelliteRoomCount; i++)
                AddRoom(layout, SatelliteRoom(anchors[i], random), random);

            ConnectClosestRooms(layout, random);

            foreach (var cell in layout.Corridors)
            {
                layout.Walkable.Add(cell);
                layout.FloorAge[cell] = 0.15f;
                if (CardinalDirections.Any(direction => IsRoomFloor(layout, cell + direction)) &&
                    layout.Doors.All(door => door.Position != cell))
                    layout.Doors.Add(new DungeonDoor(cell, true));
            }

            foreach (var cell in layout.Walkable)
            foreach (var offset in SurroundingOffsets())
                if (!layout.Walkable.Contains(cell + offset)) layout.Walls.Add(cell + offset);

            return layout;
        }

        static RectInt SatelliteRoom(Vector2Int anchor, System.Random random)
        {
            int width = random.Next(5, 9);
            int height = random.Next(4, 7);
            int centerX = anchor.x + random.Next(-2, 3);
            int centerY = anchor.y + random.Next(-1, 2);
            return new RectInt(centerX - width / 2, centerY - height / 2, width, height);
        }

        static void ConnectClosestRooms(DungeonLayout layout, System.Random random)
        {
            var connected = new HashSet<int> { 0 };
            while (connected.Count < layout.Rooms.Count)
            {
                int closestFrom = 0;
                int closestTo = 0;
                int closestDistance = int.MaxValue;
                foreach (int from in connected)
                for (int to = 0; to < layout.Rooms.Count; to++)
                {
                    if (connected.Contains(to)) continue;
                    int distance = ManhattanDistance(layout.Rooms[from].Center, layout.Rooms[to].Center);
                    if (distance >= closestDistance) continue;
                    closestFrom = from;
                    closestTo = to;
                    closestDistance = distance;
                }

                Connect(layout, layout.Rooms[closestFrom].Center, layout.Rooms[closestTo].Center,
                    random.Next(2) == 0);
                connected.Add(closestTo);
            }
        }

        static int ManhattanDistance(Vector2Int a, Vector2Int b) =>
            Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

        static void AddRoom(DungeonLayout layout, RectInt bounds, System.Random random)
        {
            var room = new DungeonRoom(bounds, (float)random.NextDouble());
            layout.Rooms.Add(room);
            foreach (var cell in Cells(bounds))
            {
                layout.Walkable.Add(cell);
                layout.FloorAge[cell] = room.Age;
            }
        }

        static void Connect(DungeonLayout layout, Vector2Int from, Vector2Int to, bool horizontalFirst)
        {
            var corner = horizontalFirst ? new Vector2Int(to.x, from.y) : new Vector2Int(from.x, to.y);
            CarveLine(layout, from, corner);
            CarveLine(layout, corner, to);
        }

        static void CarveLine(DungeonLayout layout, Vector2Int from, Vector2Int to)
        {
            var direction = new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
            var cell = from;
            while (true)
            {
                if (!IsRoomFloor(layout, cell)) layout.Corridors.Add(cell);
                if (cell == to) break;
                cell += direction;
            }
        }

        static bool IsRoomFloor(DungeonLayout layout, Vector2Int cell) =>
            layout.Rooms.Any(room => room.Bounds.Contains(cell));

        static IEnumerable<Vector2Int> Cells(RectInt bounds)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                yield return new Vector2Int(x, y);
        }

        static IEnumerable<Vector2Int> SurroundingOffsets()
        {
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                if (x != 0 || y != 0) yield return new Vector2Int(x, y);
        }
    }

    public static class DungeonTileSelector
    {
        public static float DamagedFloorChance(float roomAge) =>
            Mathf.Lerp(0.03f, 0.45f, Mathf.Clamp01(roomAge) * Mathf.Clamp01(roomAge));

        public static Sprite SelectFloor(Sprite[] floors, float roomAge, System.Random random)
        {
            var clean = FindByName(floors, "floor_1");
            var damaged = floors.Where(sprite => sprite.name != "floor_1").ToArray();
            if (damaged.Length == 0 || random.NextDouble() >= DamagedFloorChance(roomAge)) return clean;
            return damaged[random.Next(damaged.Length)];
        }

        public static Sprite FindByName(IEnumerable<Sprite> sprites, string name) =>
            sprites.First(sprite => sprite.name == name);

        public static string DoorSpriteName(bool isOpen) =>
            isOpen ? "doors_leaf_open" : "doors_leaf_closed";

        public static string WallSpriteName(DungeonLayout layout, Vector2Int wall)
        {
            bool floorLeft = layout.Walkable.Contains(wall + Vector2Int.left);
            bool floorRight = layout.Walkable.Contains(wall + Vector2Int.right);
            bool floorAbove = layout.Walkable.Contains(wall + Vector2Int.up);
            bool floorBelow = layout.Walkable.Contains(wall + Vector2Int.down);

            if (floorBelow && floorRight) return "wall_top_left";
            if (floorBelow && floorLeft) return "wall_top_right";
            if (floorBelow) return "wall_top_mid";
            if (floorAbove) return "edge_down";
            if (floorRight) return "wall_left";
            if (floorLeft) return "wall_right";
            return "wall_mid";
        }
    }
}
