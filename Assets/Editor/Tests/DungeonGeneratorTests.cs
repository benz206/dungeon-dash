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
        public void GenerateChunk_AcrossSeedsBuildsBoundedConnectedRoomsWithARealDoorway()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var layout = DungeonGenerator.GenerateChunk(new System.Random(seed), seed + 1);
                Assert.That(layout.Rooms, Has.Count.EqualTo(2), $"Seed {seed} did not create a side wing");
                Assert.That(layout.Doorway, Has.Count.EqualTo(4), $"Seed {seed} doorway was not 2x2");
                Assert.That(layout.Doorway.All(layout.Walkable.Contains), Is.True,
                    $"Seed {seed} doorway was painted over by a wall");
                Assert.That(layout.Walls.Any(layout.Walkable.Contains), Is.False,
                    $"Seed {seed} has a wall/floor overlap");
                Assert.That(layout.Walls.Count, Is.LessThan(900),
                    $"Seed {seed} filled the whole grid with deep-wall sprites");
                Assert.That(layout.Walls.All(wall => layout.Walkable.Any(floor =>
                    Mathf.Max(Mathf.Abs(wall.x - floor.x), Mathf.Abs(wall.y - floor.y)) <= 3)), Is.True,
                    $"Seed {seed} contains a wall outside the readable boundary band");

                var start = Vector2Int.RoundToInt(layout.EntryPoint);
                Assert.That(layout.Walkable, Does.Contain(start), $"Seed {seed} entry point is not floor");
                var reached = new HashSet<Vector2Int> { start };
                var pending = new Queue<Vector2Int>();
                pending.Enqueue(start);
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
                    $"Seed {seed} contains disconnected chamber floor");
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

            var regularWalls = walls.Where(renderer => renderer.sprite.name != "column_wall").ToArray();
            var southWall = regularWalls.OrderBy(renderer => renderer.transform.position.y).First();
            var northWall = regularWalls.OrderByDescending(renderer => renderer.transform.position.y).First();
            Assert.That(southWall.sortingOrder, Is.GreaterThan(northWall.sortingOrder),
                "south/front wall faces must sort in front of north/rear wall faces");

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
