using System.Collections.Generic;
using System.Linq;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDashTests
{
    public sealed class WallClassifierTests
    {
        static bool RoomWalkable(Vector2Int cell) => cell.x >= 0 && cell.x <= 2 && cell.y >= 0 && cell.y <= 2;

        static bool InBounds(RectInt bounds, Vector2Int cell) =>
            cell.x >= bounds.xMin && cell.x < bounds.xMax && cell.y >= bounds.yMin && cell.y < bounds.yMax;

        [Test]
        public void Classify_3x3RoomRing_MatchesHandTracedGodotBranchOrder()
        {
            // Room floor = {0,1,2} x {0,1,2}. Unity up = Godot north; a wall row's Godot "S" (floor
            // below it) is Unity's cell+down, and Godot "N" (floor above) is Unity's cell+up. So the
            // wall row above the room (y=3) is a Godot-front ("north"-keyed) wall, and the wall row
            // below the room (y=-1) is the room's Godot "south" wall.
            AssertSprite(new Vector2Int(-1, -1), "wall_outer_front_left", "only NE floor -> outer_front_left");
            AssertSprite(new Vector2Int(0, -1), "edge_down", "N floor only, E/W both false -> south");
            AssertSprite(new Vector2Int(1, -1), "edge_down", "N floor only -> south");
            AssertSprite(new Vector2Int(2, -1), "edge_down", "N floor only -> south");
            AssertSprite(new Vector2Int(3, -1), "wall_outer_front_right", "only NW floor -> outer_front_right");

            AssertSprite(new Vector2Int(-1, 0), "wall_edge_left", "E floor, NE floor and SE false -> edge_left");
            AssertSprite(new Vector2Int(-1, 1), "wall_edge_mid_left", "E floor, NE and SE both floor -> edge_mid_left");
            AssertSprite(new Vector2Int(-1, 2), "wall_outer_mid_left", "E floor, SE floor and NE false -> outer_mid_left");

            AssertSprite(new Vector2Int(3, 0), "wall_edge_right", "W floor, NW floor and SW false -> edge_right");
            AssertSprite(new Vector2Int(3, 1), "wall_edge_mid_right", "W floor, NW and SW both floor -> edge_mid_right");
            AssertSprite(new Vector2Int(3, 2), "wall_outer_mid_right", "W floor, SW floor and NW false -> outer_mid_right");

            AssertSprite(new Vector2Int(-1, 3), "wall_outer_top_left", "only SE floor -> outer_top_left");
            AssertSprite(new Vector2Int(0, 3), "wall_edge_top_left", "S floor, SE floor and SW false -> edge_top_left");
            AssertSprite(new Vector2Int(1, 3), "wall_top_mid", "S floor, SE and SW both floor -> north (fallback)");
            AssertSprite(new Vector2Int(2, 3), "wall_edge_top_right", "S floor, SW floor and SE false -> edge_top_right");
            AssertSprite(new Vector2Int(3, 3), "wall_outer_top_right", "only SW floor -> outer_top_right");

            AssertSprite(new Vector2Int(10, 10), "wall_mid",
                "far from any floor: deep wall but StableHash((10,10),17) % 10 == 5 != 0 -> interior");
        }

        static void AssertSprite(Vector2Int cell, string expectedSprite, string trace)
        {
            string got = WallClassifier.SpriteName(RoomWalkable, cell, Vector2Int.zero);
            Assert.That(got, Is.EqualTo(expectedSprite), $"{cell}: {trace}");
        }

        [Test]
        public void Classify_ColumnWall_NorthSouthOnly()
        {
            var mask = new NeighborMask { N = true, S = true };
            Assert.That(WallClassifier.Classify(mask, false, 1), Is.EqualTo("column_wall"));
        }

        [Test]
        public void Classify_ColumnWall_EastWestOnly()
        {
            var mask = new NeighborMask { E = true, W = true };
            Assert.That(WallClassifier.Classify(mask, false, 1), Is.EqualTo("column_wall"));
        }

        [Test]
        public void Classify_ForkFloorPlan_ProducesTshapeAtBlockedMiddleProng()
        {
            // Minimal floor pattern that reaches the tshape branch: the target wall cell (0,1) has
            // floor directly below (S=(0,0)) and floor flanking it in its own row (W=(-1,1), E=(1,1)),
            // with no floor above (N=(0,2)). (1,0) is floor so SE=(1,0) is true while SW=(-1,0) is not
            // floor, selecting tshape_bottom_left over the plain "north" fallback.
            var floor = new HashSet<Vector2Int> { new(0, 0), new(1, 0), new(1, 1), new(-1, 1) };
            bool Walkable(Vector2Int c) => floor.Contains(c);

            string sprite = WallClassifier.SpriteName(Walkable, new Vector2Int(0, 1), Vector2Int.zero);
            Assert.That(sprite, Is.EqualTo("wall_edge_tshape_bottom_left"),
                "S,E,W floor and !N, with SE floor and !SW -> tshape_bottom_left");
        }

        [Test]
        public void PlanDecorations_IsDeterministic()
        {
            var room = new RectInt(1, 0, 199, 5);
            var gridBounds = new RectInt(-1, -1, 203, 13);
            bool Walkable(Vector2Int c) => InBounds(room, c);

            var first = WallClassifier.PlanDecorations(Walkable, gridBounds, new[] { room });
            var second = WallClassifier.PlanDecorations(Walkable, gridBounds, new[] { room });

            Assert.That(first.Count, Is.EqualTo(second.Count));
            foreach (var pair in first)
            {
                Assert.That(second.ContainsKey(pair.Key), Is.True, $"missing {pair.Key} on rerun");
                Assert.That(second[pair.Key].SpriteName, Is.EqualTo(pair.Value.SpriteName));
                Assert.That(second[pair.Key].Kind, Is.EqualTo(pair.Value.Kind));
            }
        }

        [Test]
        public void PlanDecorations_FountainAndGooStayInBoundsAndOffWalkableCells()
        {
            var room = new RectInt(1, 0, 199, 5);
            var gridBounds = new RectInt(-1, -1, 203, 13);
            bool Walkable(Vector2Int c) => InBounds(room, c);

            var decorations = WallClassifier.PlanDecorations(Walkable, gridBounds, new[] { room });

            // Forced hash hits for this exact layout/origin: x=74 rolls < 10 (fountain),
            // x=11 rolls in [10,36) (goo), x=2 rolls in [36,110) (flat decor).
            Assert.That(decorations[new Vector2Int(74, 5)].Kind, Is.EqualTo(WallDecorationKind.FountainBasin));
            Assert.That(decorations[new Vector2Int(74, 6)].Kind, Is.EqualTo(WallDecorationKind.FountainMid));
            Assert.That(decorations[new Vector2Int(74, 7)].Kind, Is.EqualTo(WallDecorationKind.FountainTop));
            Assert.That(decorations[new Vector2Int(11, 5)].Kind, Is.EqualTo(WallDecorationKind.GooBase));
            Assert.That(decorations[new Vector2Int(11, 6)].Kind, Is.EqualTo(WallDecorationKind.Goo));

            foreach (var pair in decorations)
            {
                Assert.That(Walkable(pair.Key), Is.False, $"{pair.Key} overlaps a walkable cell");
                Assert.That(InBounds(gridBounds, pair.Key), Is.True, $"{pair.Key} escaped gridBounds");
            }
        }

        [Test]
        public void PlanDecorations_FlatDecorOnlyOnStraightFrontWallCells()
        {
            var room = new RectInt(1, 0, 199, 5);
            var gridBounds = new RectInt(-1, -1, 203, 13);
            bool Walkable(Vector2Int c) => InBounds(room, c);

            var decorations = WallClassifier.PlanDecorations(Walkable, gridBounds, new[] { room });

            foreach (var pair in decorations.Where(entry => entry.Value.Kind == WallDecorationKind.FlatDecor))
                Assert.That(WallClassifier.IsStraightFrontWall(Walkable, pair.Key), Is.True,
                    $"{pair.Key} flat decor placed on a non-straight front wall");
        }

        [Test]
        public void PlanDecorations_GuaranteesBannerOnSecondRoomsNorthWall()
        {
            var room0 = new RectInt(0, 0, 5, 3);
            var room1 = new RectInt(20, 0, 5, 3);
            var gridBounds = new RectInt(-1, -1, 27, 7);
            bool Walkable(Vector2Int c) => InBounds(room0, c) || InBounds(room1, c);

            var decorations = WallClassifier.PlanDecorations(Walkable, gridBounds, new[] { room0, room1 });

            var banners = decorations.Where(entry => entry.Value.Kind == WallDecorationKind.Banner).ToList();
            Assert.That(banners, Has.Count.EqualTo(1));
            Assert.That(banners[0].Key, Is.EqualTo(new Vector2Int(22, 3)));
            Assert.That(banners[0].Value.SpriteName, Is.EqualTo("wall_banner_blue"));
        }
    }
}
