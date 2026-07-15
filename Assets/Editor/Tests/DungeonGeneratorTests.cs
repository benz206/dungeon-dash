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
        public void Generate_CreatesConnectedRoomsAndOpenDoorways()
        {
            var layout = DungeonGenerator.Generate(new System.Random(7));

            Assert.That(layout.Rooms, Has.Count.EqualTo(7));
            Assert.That(layout.Corridors, Is.Not.Empty);
            Assert.That(layout.Doors, Is.Not.Empty);
            Assert.That(layout.Doors.All(door => door.IsOpen && layout.Walkable.Contains(door.Position)), Is.True);

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
            }
        }

        [Test]
        public void WallSelection_UsesOnlyStructuralSprites()
        {
            var layout = DungeonGenerator.Generate(new System.Random(11));
            var structuralNames = new HashSet<string>
            {
                "wall_top_left", "wall_top_mid", "wall_top_right", "edge_down",
                "wall_left", "wall_mid", "wall_right"
            };

            Assert.That(layout.Walls.Select(cell => DungeonTileSelector.WallSpriteName(layout, cell))
                .All(structuralNames.Contains), Is.True);
        }

        [Test]
        public void SemanticSelection_DistinguishesDoorStateAndBannerColor()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/Resources/GameCatalog.asset");

            Assert.That(DungeonTileSelector.FindByName(catalog.walls,
                DungeonTileSelector.DoorSpriteName(true)).name, Is.EqualTo("doors_leaf_open"));
            Assert.That(DungeonTileSelector.FindByName(catalog.walls,
                DungeonTileSelector.DoorSpriteName(false)).name, Is.EqualTo("doors_leaf_closed"));
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
        public IEnumerator Arena_RendersSemanticWallsDoorsAndBanners()
        {
            yield return new EnterPlayMode();

            var arena = GameObject.Find("Arena");
            var renderers = arena.GetComponentsInChildren<SpriteRenderer>();
            var walls = renderers.Where(renderer => renderer.name.StartsWith("Wall ")).ToArray();
            var doors = renderers.Where(renderer => renderer.name == "Door open").ToArray();
            var banners = renderers.Where(renderer => renderer.name.StartsWith("Banner ")).ToArray();

            Assert.That(walls, Is.Not.Empty);
            Assert.That(walls.All(renderer => renderer.sprite.name is "wall_top_left" or "wall_top_mid" or
                "wall_top_right" or "edge_down" or "wall_left" or "wall_mid" or "wall_right"), Is.True);
            Assert.That(doors, Is.Not.Empty);
            Assert.That(doors.All(renderer => renderer.sprite.name == "doors_leaf_open"), Is.True);
            Assert.That(banners, Is.Not.Empty);
            Assert.That(banners.All(renderer => renderer.sprite.name.StartsWith("wall_banner_")), Is.True);

            yield return new ExitPlayMode();
        }

        static int CountDamaged(Sprite[] floors, float age, System.Random random)
        {
            int count = 0;
            for (int i = 0; i < 2000; i++)
                if (DungeonTileSelector.SelectFloor(floors, age, random).name != "floor_1") count++;
            return count;
        }
    }
}
