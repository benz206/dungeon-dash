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

    public sealed class DungeonLayout
    {
        public List<DungeonRoom> Rooms { get; } = new();
        public HashSet<Vector2Int> Walkable { get; } = new();
        public HashSet<Vector2Int> Corridors { get; } = new();
        public HashSet<Vector2Int> Walls { get; } = new();
        public HashSet<Vector2Int> Doorway { get; } = new();
        public Dictionary<Vector2Int, float> FloorAge { get; } = new();
        public RectInt GridBounds { get; set; }
        public Vector2 EntryPoint { get; set; }
        public Vector2 ExitDoorPosition { get; set; }
    }

    public static class DungeonGenerator
    {
        const int GridWidth = 56;
        const int GridHeight = 36;
        const int Margin = 4;
        const int MinRoomSize = 5;
        const int MaxRoomSize = 11;
        const int MinRooms = 4;
        const int BaseAttempts = 30;
        const int HardAttemptCap = 120;
        const int ChunkWallDepth = 3;

        public static DungeonLayout GenerateChunk(System.Random random, int depth)
        {
            var layout = new DungeonLayout();
            int width = random.Next(8, 11) * 2 + 1;
            int height = random.Next(5, 7) * 2 + 1;
            var mainRoom = new RectInt(-width / 2, -height / 2, width, height);
            AddRoom(layout, mainRoom, random);

            // A shallow side wing keeps each chamber readable at one-screen scale while still
            // producing real inner/outer corners for the wall topology to resolve.
            int wingWidth = random.Next(4, 6);
            int wingHeight = random.Next(5, 8);
            int wingY = random.Next(mainRoom.yMin + 1, mainRoom.yMax - wingHeight);
            bool wingOnLeft = (depth + random.Next(2)) % 2 == 0;
            var wing = wingOnLeft
                ? new RectInt(mainRoom.xMin - wingWidth + 1, wingY, wingWidth, wingHeight)
                : new RectInt(mainRoom.xMax - 1, wingY, wingWidth, wingHeight);
            AddRoom(layout, wing, random);

            // The north doorway is two tiles wide and two tiles deep. It is authored as floor so
            // the surrounding wall classifier sees a genuine opening instead of a sprite overlay.
            int doorY = mainRoom.yMax;
            for (int y = doorY; y <= doorY + 1; y++)
            for (int x = -1; x <= 0; x++)
            {
                var cell = new Vector2Int(x, y);
                layout.Doorway.Add(cell);
                layout.Corridors.Add(cell);
                layout.Walkable.Add(cell);
                layout.FloorAge[cell] = 0.1f;
            }

            layout.EntryPoint = new Vector2(0f, mainRoom.yMin + 2f);
            layout.ExitDoorPosition = new Vector2(-0.5f, doorY + 0.5f);
            layout.GridBounds = BoundsAround(layout.Walkable, ChunkWallDepth + 1);
            BuildBoundaryWalls(layout, ChunkWallDepth);
            return layout;
        }

        public static DungeonLayout Generate(System.Random random)
        {
            var layout = new DungeonLayout();

            int attempts = 0;
            while (attempts < BaseAttempts || (layout.Rooms.Count < MinRooms && attempts < HardAttemptCap))
            {
                attempts++;
                int width = random.Next(MinRoomSize, MaxRoomSize + 1);
                int height = random.Next(MinRoomSize, MaxRoomSize + 1);
                int x = random.Next(Margin, GridWidth - Margin - width);
                int y = random.Next(Margin, GridHeight - Margin - height);
                var bounds = new RectInt(x, y, width, height);
                if (OverlapsExistingRoom(layout, bounds)) continue;
                AddRoom(layout, bounds, random);
            }

            ConnectChain(layout, random);

            foreach (var cell in layout.Corridors)
            {
                layout.Walkable.Add(cell);
                layout.FloorAge[cell] = 0.15f;
            }

            var offset = Vector2Int.zero - layout.Rooms[0].Center;
            Translate(layout, offset);
            layout.GridBounds = new RectInt(offset.x, offset.y, GridWidth, GridHeight);

            foreach (var cell in Cells(layout.GridBounds))
                if (!layout.Walkable.Contains(cell)) layout.Walls.Add(cell);

            return layout;
        }

        static bool OverlapsExistingRoom(DungeonLayout layout, RectInt candidate)
        {
            var inflated = Inflate(candidate, 1);
            return layout.Rooms.Any(room => inflated.Overlaps(room.Bounds));
        }

        static RectInt Inflate(RectInt rect, int amount) =>
            new(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2, rect.height + amount * 2);

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

        static void ConnectChain(DungeonLayout layout, System.Random random)
        {
            for (int i = 1; i < layout.Rooms.Count; i++)
            {
                var from = layout.Rooms[i - 1].Center;
                var to = layout.Rooms[i].Center;
                Connect(layout, from, to, random.Next(2) == 0);
            }
        }

        static void Connect(DungeonLayout layout, Vector2Int from, Vector2Int to, bool horizontalFirst)
        {
            Vector2Int corner;
            if (horizontalFirst)
            {
                corner = new Vector2Int(to.x, from.y);
                CarveHorizontalLeg(layout, from.x, to.x, from.y);
                CarveVerticalLeg(layout, from.y, to.y, to.x);
            }
            else
            {
                corner = new Vector2Int(from.x, to.y);
                CarveVerticalLeg(layout, from.y, to.y, from.x);
                CarveHorizontalLeg(layout, from.x, to.x, to.y);
            }

            // Guarantee a full 2x2 at the elbow regardless of leg travel direction.
            CarveCorridorCell(layout, corner);
            CarveCorridorCell(layout, corner + Vector2Int.right);
            CarveCorridorCell(layout, corner + Vector2Int.up);
            CarveCorridorCell(layout, corner + Vector2Int.right + Vector2Int.up);
        }

        static void CarveHorizontalLeg(DungeonLayout layout, int xFrom, int xTo, int y)
        {
            int xMin = Math.Min(xFrom, xTo);
            int xMax = Math.Max(xFrom, xTo);
            for (int x = xMin; x <= xMax; x++)
            {
                CarveCorridorCell(layout, new Vector2Int(x, y));
                CarveCorridorCell(layout, new Vector2Int(x, y + 1));
            }
        }

        static void CarveVerticalLeg(DungeonLayout layout, int yFrom, int yTo, int x)
        {
            int yMin = Math.Min(yFrom, yTo);
            int yMax = Math.Max(yFrom, yTo);
            for (int y = yMin; y <= yMax; y++)
            {
                CarveCorridorCell(layout, new Vector2Int(x, y));
                CarveCorridorCell(layout, new Vector2Int(x + 1, y));
            }
        }

        static void CarveCorridorCell(DungeonLayout layout, Vector2Int cell)
        {
            if (IsRoomFloor(layout, cell)) return;
            layout.Corridors.Add(cell);
        }

        static void Translate(DungeonLayout layout, Vector2Int offset)
        {
            var rooms = layout.Rooms.Select(room => new DungeonRoom(
                new RectInt(room.Bounds.xMin + offset.x, room.Bounds.yMin + offset.y, room.Bounds.width, room.Bounds.height),
                room.Age)).ToList();
            layout.Rooms.Clear();
            layout.Rooms.AddRange(rooms);

            var walkable = layout.Walkable.Select(cell => cell + offset).ToList();
            layout.Walkable.Clear();
            layout.Walkable.UnionWith(walkable);

            var corridors = layout.Corridors.Select(cell => cell + offset).ToList();
            layout.Corridors.Clear();
            layout.Corridors.UnionWith(corridors);

            var floorAge = layout.FloorAge.Select(pair => (cell: pair.Key + offset, age: pair.Value)).ToList();
            layout.FloorAge.Clear();
            foreach (var entry in floorAge) layout.FloorAge[entry.cell] = entry.age;
        }

        static bool IsRoomFloor(DungeonLayout layout, Vector2Int cell) =>
            layout.Rooms.Any(room => room.Bounds.Contains(cell));

        internal static RectInt BoundsAround(IEnumerable<Vector2Int> cells, int padding)
        {
            int xMin = cells.Min(cell => cell.x) - padding;
            int xMax = cells.Max(cell => cell.x) + padding;
            int yMin = cells.Min(cell => cell.y) - padding;
            int yMax = cells.Max(cell => cell.y) + padding;
            return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        internal static void BuildBoundaryWalls(DungeonLayout layout, int depth)
        {
            foreach (var floor in layout.Walkable)
            for (int y = -depth; y <= depth; y++)
            for (int x = -depth; x <= depth; x++)
            {
                var cell = floor + new Vector2Int(x, y);
                if (!layout.Walkable.Contains(cell)) layout.Walls.Add(cell);
            }
        }

        static IEnumerable<Vector2Int> Cells(RectInt bounds)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                yield return new Vector2Int(x, y);
        }
    }

    public static class DungeonTileSelector
    {
        static readonly string[] CleanFloorNames = { "floor_1", "floor_2", "floor_3", "floor_5" };
        static readonly string[] DamagedFloorNames = { "floor_4", "floor_6", "floor_7", "floor_8" };

        public static float DamagedFloorChance(float roomAge) =>
            Mathf.Lerp(0.03f, 0.45f, Mathf.Clamp01(roomAge) * Mathf.Clamp01(roomAge));

        public static Sprite[] CleanFloorPool(Sprite[] floors) => Pool(floors, CleanFloorNames);

        public static Sprite[] DamagedFloorPool(Sprite[] floors) => Pool(floors, DamagedFloorNames);

        static Sprite[] Pool(Sprite[] floors, string[] names) =>
            floors.Where(sprite => names.Contains(sprite.name)).ToArray();

        public static Sprite SelectFloor(Sprite[] cleanFloors, Sprite[] damagedFloors, float wear, Vector2Int cell)
        {
            int threshold = Mathf.RoundToInt(DamagedFloorChance(wear) * 1000f);
            var pool = Hash(cell, 211) % 1000 < threshold ? damagedFloors : cleanFloors;
            return pool[Hash(cell, 223) % pool.Length];
        }

        // Path and grass tiles are chosen by a stable per-cell hash so a given layout renders
        // identically every frame without consuming the generator's random stream.
        public static Sprite SelectPath(Sprite[] paths, Vector2Int cell) =>
            paths[StableHash(cell) % paths.Length];

        public static Sprite SelectGrass(Sprite[] grass, Vector2Int cell) =>
            grass[StableHash(cell) % grass.Length];

        static int StableHash(Vector2Int cell) => Hash(cell, 17);

        static int Hash(Vector2Int cell, int salt) => Mix(WallClassifier.StableHash(cell, salt));

        static int Mix(int hash)
        {
            unchecked
            {
                uint value = (uint)hash;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (int)(value & 0x7FFFFFFF);
            }
        }

        public static Sprite FindByName(IEnumerable<Sprite> sprites, string name) =>
            sprites.First(sprite => sprite.name == name);

        public static IReadOnlyCollection<string> StructuralWallSpriteNames => WallClassifier.StructuralSpriteNames;

        public static string WallSpriteName(DungeonLayout layout, Vector2Int cell) =>
            WallClassifier.SpriteName(layout.Walkable.Contains, cell, layout.GridBounds.min);
    }
}
