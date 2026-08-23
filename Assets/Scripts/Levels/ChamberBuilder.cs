using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonDash
{
    public static class ChamberBuilder
    {
        const int WallDepth = 3;
        const int RoomGapMin = 3;
        const int RoomGapMax = 6;
        const int PlacementAttempts = 40;
        const int PropAttempts = 12;
        const float PropsPerCell = 0.06f;
        const int EntryClearanceSquared = 25;

        static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        sealed class PlacedRoom
        {
            public RoomTemplate Template;
            public RectInt Bounds;
            public Vector2Int Anchor;
            public List<Vector2Int> Cells;
            public List<Vector2Int> TopRow;
            public List<Vector2Int> Corners;
        }

        public static ChamberPlan Build(System.Random random, int depth, LevelLibrary library)
        {
            var layout = new DungeonLayout();
            var rooms = new List<PlacedRoom>();

            var entryTemplate = library.PickTemplate(random, depth, RoomRole.Entry);
            var entrySize = entryTemplate.RollSize(random);
            var entryBounds = new RectInt(-entrySize.x / 2, -entrySize.y / 2, entrySize.x, entrySize.y);
            var entryRoom = Stamp(layout, entryTemplate, entryBounds, random);
            rooms.Add(entryRoom);

            int roomCount = library.RoomCountForDepth(depth);
            for (int i = 1; i < roomCount; i++)
            {
                var role = i == roomCount - 1 && roomCount > 2 ? RoomRole.Treasure
                    : i % 2 == 0 ? RoomRole.Hall
                    : RoomRole.Combat;
                TryGrowRoom(layout, rooms, library.PickTemplate(random, depth, role), random);
            }

            layout.EntryPoint = NearestCell(entryRoom.Cells,
                new Vector2Int(entryRoom.Bounds.xMin + entryRoom.Bounds.width / 2, entryRoom.Bounds.yMin + 1));

            var exitRoom = rooms
                .OrderByDescending(room => (room.Anchor - entryRoom.Anchor).sqrMagnitude)
                .First();
            CarveExitDoorway(layout, exitRoom);

            layout.GridBounds = DungeonGenerator.BoundsAround(layout.Walkable, WallDepth + 1);
            DungeonGenerator.BuildBoundaryWalls(layout, WallDepth);

            var plan = new ChamberPlan
            {
                Layout = layout,
                Theme = library.ThemeForDepth(depth),
                WallDecorations = WallClassifier.PlanDecorations(layout.Walkable.Contains,
                    layout.GridBounds, layout.Rooms.Select(room => room.Bounds).ToList())
            };
            foreach (var room in rooms)
            foreach (var pillar in PillarCells(room))
                if (layout.Walls.Contains(pillar)) plan.ForcedColumns.Add(pillar);

            EnsureBanner(plan);
            PlaceGrass(plan, rooms, random);
            PlaceProps(plan, rooms, random);
            CollectSpawnAnchors(plan, rooms);
            return plan;
        }

        static void TryGrowRoom(DungeonLayout layout, List<PlacedRoom> rooms, RoomTemplate template,
            System.Random random)
        {
            if (template == null) return;
            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                var neighbor = rooms[random.Next(rooms.Count)];
                var size = template.RollSize(random);
                int gap = random.Next(RoomGapMin, RoomGapMax + 1);
                var direction = Directions[random.Next(Directions.Length)];
                int jitter = random.Next(-2, 3);

                int x, y;
                if (direction.x != 0)
                {
                    x = direction.x > 0 ? neighbor.Bounds.xMax + gap : neighbor.Bounds.xMin - gap - size.x;
                    y = neighbor.Bounds.yMin + jitter;
                }
                else
                {
                    y = direction.y > 0 ? neighbor.Bounds.yMax + gap : neighbor.Bounds.yMin - gap - size.y;
                    x = neighbor.Bounds.xMin + jitter;
                }

                var bounds = new RectInt(x, y, size.x, size.y);
                var padded = new RectInt(x - RoomGapMin, y - RoomGapMin,
                    size.x + RoomGapMin * 2, size.y + RoomGapMin * 2);
                if (rooms.Any(room => padded.Overlaps(room.Bounds))) continue;

                var placed = Stamp(layout, template, bounds, random);
                Carve(layout, neighbor.Anchor, placed.Anchor, random.Next(2) == 0);
                rooms.Add(placed);
                return;
            }
        }

        static PlacedRoom Stamp(DungeonLayout layout, RoomTemplate template, RectInt bounds,
            System.Random random)
        {
            float age = (float)random.NextDouble();
            var cells = ShapeCells(template, bounds, random);
            foreach (var cell in cells)
            {
                layout.Walkable.Add(cell);
                layout.FloorAge[cell] = age;
            }
            layout.Rooms.Add(new DungeonRoom(bounds, age));

            var center = new Vector2Int(bounds.xMin + bounds.width / 2, bounds.yMin + bounds.height / 2);
            int topY = cells.Max(cell => cell.y);
            return new PlacedRoom
            {
                Template = template,
                Bounds = bounds,
                Cells = cells,
                Anchor = NearestCell(cells, center),
                TopRow = cells.Where(cell => cell.y == topY).ToList(),
                Corners = new List<Vector2Int>
                {
                    NearestCell(cells, new Vector2Int(bounds.xMin, bounds.yMin)),
                    NearestCell(cells, new Vector2Int(bounds.xMax - 1, bounds.yMin)),
                    NearestCell(cells, new Vector2Int(bounds.xMin, bounds.yMax - 1)),
                    NearestCell(cells, new Vector2Int(bounds.xMax - 1, bounds.yMax - 1))
                }
            };
        }

        static List<Vector2Int> ShapeCells(RoomTemplate template, RectInt bounds, System.Random random)
        {
            var cells = new List<Vector2Int>(bounds.width * bounds.height);
            int cutX = Mathf.Max(1, bounds.width / 4);
            int cutY = Mathf.Max(1, bounds.height / 4);
            float centerX = (bounds.xMin + bounds.xMax - 1) * 0.5f;
            float centerY = (bounds.yMin + bounds.yMax - 1) * 0.5f;
            float radiusX = Mathf.Max(1f, bounds.width * 0.5f);
            float radiusY = Mathf.Max(1f, bounds.height * 0.5f);
            int notchWidth = Mathf.Max(2, bounds.width / 3);
            int notchHeight = Mathf.Max(2, bounds.height / 3);
            bool notchRight = random.Next(2) == 0;
            bool notchTop = random.Next(2) == 0;

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                bool keep = true;
                switch (template.shape)
                {
                    case RoomShape.Cross:
                        keep = !((x < bounds.xMin + cutX || x >= bounds.xMax - cutX) &&
                                 (y < bounds.yMin + cutY || y >= bounds.yMax - cutY));
                        break;
                    case RoomShape.Ellipse:
                        float dx = (x - centerX) / radiusX;
                        float dy = (y - centerY) / radiusY;
                        keep = dx * dx + dy * dy <= 1f;
                        break;
                    case RoomShape.Notched:
                        bool inX = notchRight ? x >= bounds.xMax - notchWidth : x < bounds.xMin + notchWidth;
                        bool inY = notchTop ? y >= bounds.yMax - notchHeight : y < bounds.yMin + notchHeight;
                        keep = !(inX && inY);
                        break;
                    case RoomShape.Pillared:
                        keep = !IsPillar(bounds, new Vector2Int(x, y));
                        break;
                }
                if (keep) cells.Add(new Vector2Int(x, y));
            }

            if (cells.Count == 0)
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    cells.Add(new Vector2Int(x, y));
            return cells;
        }

        static bool IsPillar(RectInt bounds, Vector2Int cell)
        {
            if (cell.x <= bounds.xMin + 1 || cell.x >= bounds.xMax - 2) return false;
            if (cell.y <= bounds.yMin + 1 || cell.y >= bounds.yMax - 2) return false;
            return (cell.x - bounds.xMin) % 3 == 0 && (cell.y - bounds.yMin) % 3 == 0;
        }

        static IEnumerable<Vector2Int> PillarCells(PlacedRoom room)
        {
            if (room.Template.shape != RoomShape.Pillared) yield break;
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
            {
                var cell = new Vector2Int(x, y);
                if (IsPillar(room.Bounds, cell)) yield return cell;
            }
        }

        static void Carve(DungeonLayout layout, Vector2Int from, Vector2Int to, bool horizontalFirst)
        {
            if (horizontalFirst)
            {
                CarveHorizontal(layout, from.x, to.x, from.y);
                CarveVertical(layout, from.y, to.y, to.x);
            }
            else
            {
                CarveVertical(layout, from.y, to.y, from.x);
                CarveHorizontal(layout, from.x, to.x, to.y);
            }

            var corner = horizontalFirst ? new Vector2Int(to.x, from.y) : new Vector2Int(from.x, to.y);
            CarveCell(layout, corner);
            CarveCell(layout, corner + Vector2Int.right);
            CarveCell(layout, corner + Vector2Int.up);
            CarveCell(layout, corner + Vector2Int.one);
        }

        static void CarveHorizontal(DungeonLayout layout, int fromX, int toX, int y)
        {
            for (int x = Mathf.Min(fromX, toX); x <= Mathf.Max(fromX, toX); x++)
            {
                CarveCell(layout, new Vector2Int(x, y));
                CarveCell(layout, new Vector2Int(x, y + 1));
            }
        }

        static void CarveVertical(DungeonLayout layout, int fromY, int toY, int x)
        {
            for (int y = Mathf.Min(fromY, toY); y <= Mathf.Max(fromY, toY); y++)
            {
                CarveCell(layout, new Vector2Int(x, y));
                CarveCell(layout, new Vector2Int(x + 1, y));
            }
        }

        static void CarveCell(DungeonLayout layout, Vector2Int cell)
        {
            if (!layout.Walkable.Add(cell)) return;
            layout.Corridors.Add(cell);
            layout.FloorAge[cell] = 0.15f;
        }

        static void CarveExitDoorway(DungeonLayout layout, PlacedRoom exit)
        {
            int doorX = exit.Bounds.xMin + exit.Bounds.width / 2;
            int doorY = exit.Bounds.yMax;
            for (int y = exit.Anchor.y; y <= doorY + 1; y++)
            {
                CarveCell(layout, new Vector2Int(doorX - 1, y));
                CarveCell(layout, new Vector2Int(doorX, y));
            }
            for (int y = doorY; y <= doorY + 1; y++)
            for (int x = doorX - 1; x <= doorX; x++)
                layout.Doorway.Add(new Vector2Int(x, y));
            layout.ExitDoorPosition = new Vector2(doorX - 0.5f, doorY + 0.5f);
        }

        static void EnsureBanner(ChamberPlan plan)
        {
            foreach (var decoration in plan.WallDecorations.Values)
                if (decoration.Kind == WallDecorationKind.Banner) return;

            var layout = plan.Layout;
            var origin = layout.GridBounds.min;
            foreach (var cell in layout.Walls.OrderByDescending(cell => cell.y).ThenBy(cell => cell.x))
            {
                if (plan.WallDecorations.ContainsKey(cell)) continue;
                var mask = NeighborMask.From(layout.Walkable.Contains, cell);
                bool deep = WallClassifier.IsDeepWall(layout.Walkable.Contains, cell);
                if (WallClassifier.Classify(mask, deep, WallClassifier.StableHash(cell - origin, 17)) != "north") continue;
                if (!WallClassifier.IsStraightFrontWall(layout.Walkable.Contains, cell)) continue;
                plan.WallDecorations[cell] = new WallDecoration(
                    WallClassifier.SemanticToSprite["banner_blue"], null, WallDecorationKind.Banner);
                return;
            }
        }

        static void PlaceGrass(ChamberPlan plan, List<PlacedRoom> rooms, System.Random random)
        {
            if (plan.Theme.grassChance <= 0f) return;
            foreach (var room in rooms)
            {
                if (random.NextDouble() >= plan.Theme.grassChance) continue;
                foreach (var cell in room.Cells)
                    if (!plan.Layout.Corridors.Contains(cell)) plan.GrassCells.Add(cell);
            }
        }

        static void PlaceProps(ChamberPlan plan, List<PlacedRoom> rooms, System.Random random)
        {
            var props = plan.Theme.props?.Where(prop => prop != null).ToArray();
            if (props == null || props.Length == 0) return;

            var entry = Vector2Int.RoundToInt(plan.Layout.EntryPoint);
            var occupied = new HashSet<Vector2Int>(plan.Layout.Doorway);
            var used = new Dictionary<PropDefinition, int>();

            foreach (var room in rooms)
            {
                used.Clear();
                float density = plan.Theme.propDensity * room.Template.propDensity;
                int budget = Mathf.RoundToInt(room.Cells.Count * density * PropsPerCell);
                for (int i = 0; i < budget; i++)
                {
                    var definition = LevelLibrary.WeightedPick(props, random, prop => prop.weight);
                    if (definition == null) break;
                    used.TryGetValue(definition, out int placed);
                    if (placed >= definition.maxPerRoom) continue;
                    if (!TryPropCell(plan, room, definition, occupied, entry, random, out var cell)) continue;

                    used[definition] = placed + 1;
                    occupied.Add(cell);
                    plan.Props.Add(new PlacedProp(definition, cell + definition.offset));
                    if (definition.blocksMovement) plan.BlockedCells.Add(cell);
                }
            }
        }

        static bool TryPropCell(ChamberPlan plan, PlacedRoom room, PropDefinition definition,
            HashSet<Vector2Int> occupied, Vector2Int entry, System.Random random, out Vector2Int cell)
        {
            for (int attempt = 0; attempt < PropAttempts; attempt++)
            {
                cell = definition.placement switch
                {
                    PropPlacement.RoomCenter => room.Anchor,
                    PropPlacement.RoomCorner => room.Corners[random.Next(room.Corners.Count)],
                    PropPlacement.AgainstNorthWall when room.TopRow.Count > 0 =>
                        room.TopRow[random.Next(room.TopRow.Count)],
                    _ => room.Cells[random.Next(room.Cells.Count)]
                };
                if (occupied.Contains(cell)) continue;
                if (plan.Layout.Corridors.Contains(cell)) continue;
                if (!plan.Layout.Walkable.Contains(cell)) continue;
                if ((cell - entry).sqrMagnitude < EntryClearanceSquared) continue;
                return true;
            }

            cell = default;
            return false;
        }

        static void CollectSpawnAnchors(ChamberPlan plan, List<PlacedRoom> rooms)
        {
            var entry = plan.Layout.EntryPoint;
            var entryCell = Vector2Int.RoundToInt(entry);
            foreach (var room in rooms)
            foreach (var cell in room.Cells)
            {
                if (plan.Layout.Corridors.Contains(cell)) continue;
                if (plan.BlockedCells.Contains(cell)) continue;
                if (!plan.Layout.Walkable.Contains(cell)) continue;
                if ((cell - entryCell).sqrMagnitude < EntryClearanceSquared) continue;
                plan.SpawnAnchors.Add(cell);
            }

            plan.SpawnAnchors.Sort((left, right) =>
                (right - entry).sqrMagnitude.CompareTo((left - entry).sqrMagnitude));

            if (plan.SpawnAnchors.Count > 0) return;
            foreach (var cell in plan.Layout.Walkable) plan.SpawnAnchors.Add(cell);
        }

        static Vector2Int NearestCell(List<Vector2Int> cells, Vector2Int target)
        {
            var best = cells[0];
            int bestDistance = int.MaxValue;
            foreach (var cell in cells)
            {
                int distance = (cell - target).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = cell;
            }
            return best;
        }
    }
}
