using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonDash;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class DungeonGeneratorTests
    {
        static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
        };

        [Test]
        public void Generate_CreatesConnectedRooms()
        {
            var layout = DungeonGenerator.Generate(new System.Random(7));

            Assert.That(layout.Rooms.Count, Is.InRange(4, 30));
            Assert.That(layout.Corridors, Is.Not.Empty);
            Assert.That(layout.Walkable, Does.Contain(Vector2Int.zero));

            var reached = new HashSet<Vector2Int> { Vector2Int.zero };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(Vector2Int.zero);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (layout.Walkable.Contains(next) && reached.Add(next)) pending.Enqueue(next);
                }
            }

            Assert.That(reached, Has.Count.EqualTo(layout.Walkable.Count));
            Assert.That(layout.Rooms.All(room => room.Age >= 0f && room.Age <= 1f), Is.True);
        }

        [Test]
        public void Generate_AcrossSeedsKeepsRoomsSeparateAndEveryFloorReachable()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var layout = DungeonGenerator.Generate(new System.Random(seed));
                Assert.That(layout.Rooms.Count, Is.GreaterThanOrEqualTo(4), $"Seed {seed} created too few rooms");

                for (int first = 0; first < layout.Rooms.Count; first++)
                for (int second = first + 1; second < layout.Rooms.Count; second++)
                    Assert.That(layout.Rooms[first].Bounds.Overlaps(layout.Rooms[second].Bounds), Is.False,
                        $"Seed {seed} created overlapping rooms {first} and {second}");

                var reached = new HashSet<Vector2Int> { Vector2Int.zero };
                var pending = new Queue<Vector2Int>();
                pending.Enqueue(Vector2Int.zero);
                while (pending.Count > 0)
                {
                    var cell = pending.Dequeue();
                    foreach (var direction in Directions)
                    {
                        var next = cell + direction;
                        if (layout.Walkable.Contains(next) && reached.Add(next)) pending.Enqueue(next);
                    }
                }

                Assert.That(reached, Has.Count.EqualTo(layout.Walkable.Count),
                    $"Seed {seed} created unreachable floor tiles");

                bool foundBannerRow = false;
                for (int i = 1; i < layout.Rooms.Count && !foundBannerRow; i++)
                {
                    var room = layout.Rooms[i];
                    for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        var cell = new Vector2Int(x, room.Bounds.yMax);
                        if (!layout.Walls.Contains(cell)) continue;
                        if (DungeonTileSelector.WallSpriteName(layout, cell) != "wall_top_mid") continue;
                        foundBannerRow = true;
                        break;
                    }
                }

                Assert.That(foundBannerRow, Is.True,
                    $"Seed {seed} had no satellite room with a wall_top_mid cell in its top wall row");
            }
        }

        [Test]
        public void WallSelection_UsesOnlyStructuralSprites()
        {
            var layout = DungeonGenerator.Generate(new System.Random(11));

            Assert.That(layout.Walls.Select(cell => DungeonTileSelector.WallSpriteName(layout, cell))
                .All(DungeonTileSelector.StructuralWallSpriteNames.Contains), Is.True);

            Assert.That(DungeonTileSelector.StructuralWallSpriteNames, Is.EquivalentTo(new[]
            {
                "wall_mid", "wall_top_mid", "wall_top_left", "wall_top_right", "edge_down",
                "wall_edge_bottom_left", "wall_edge_bottom_right", "wall_left", "wall_right",
                "wall_edge_left", "wall_edge_right", "wall_edge_mid_left", "wall_edge_mid_right",
                "wall_edge_top_left", "wall_edge_top_right", "wall_edge_tshape_left", "wall_edge_tshape_right",
                "wall_edge_tshape_bottom_left", "wall_edge_tshape_bottom_right", "wall_outer_top_left",
                "wall_outer_top_right", "wall_outer_mid_left", "wall_outer_mid_right", "wall_outer_front_left",
                "wall_outer_front_right", "column_wall"
            }));
        }

        [Test]
        public void SemanticSelection_ResolvesBannerSprites()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/Resources/GameCatalog.asset");

            Assert.That(DungeonTileSelector.FindByName(catalog.walls,
                "wall_banner_blue").name, Is.EqualTo("wall_banner_blue"));
        }

        [Test]
        public void FloorWear_IsRareInFreshRoomsAndIncreasesWithAge()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/Resources/GameCatalog.asset");
            int freshDamaged = CountDamaged(catalog.floors, 0f, new System.Random(3));
            int oldDamaged = CountDamaged(catalog.floors, 1f, new System.Random(3));

            Assert.That(freshDamaged, Is.LessThan(100));
            Assert.That(oldDamaged, Is.GreaterThan(700));
            Assert.That(oldDamaged, Is.GreaterThan(freshDamaged * 8));
        }

        [UnityTest]
        public IEnumerator Arena_RendersSemanticWallsAndBanners()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);
            yield return null;

            var arena = GameObject.Find("Arena");
            var renderers = arena.GetComponentsInChildren<SpriteRenderer>();
            var walls = renderers.Where(renderer => renderer.name.StartsWith("Wall ")).ToArray();
            var banners = renderers.Where(renderer => renderer.name.StartsWith("Banner ")).ToArray();

            Assert.That(walls, Is.Not.Empty);
            Assert.That(walls.All(renderer => DungeonTileSelector.StructuralWallSpriteNames.Contains(renderer.sprite.name)), Is.True);
            Assert.That(banners, Is.Not.Empty);
            Assert.That(banners.All(renderer => renderer.sprite.name.StartsWith("wall_banner_")), Is.True);

            yield return new ExitPlayMode();
        }

        static readonly HashSet<string> DamagedFloorNames = new() { "floor_4", "floor_6", "floor_7", "floor_8" };

        static int CountDamaged(Sprite[] floors, float age, System.Random random)
        {
            int count = 0;
            for (int i = 0; i < 2000; i++)
                if (DamagedFloorNames.Contains(DungeonTileSelector.SelectFloor(floors, age, random).name)) count++;
            return count;
        }
    }
}
