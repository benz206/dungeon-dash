using System.Collections.Generic;
using System.Linq;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDashTests
{
    public sealed class ChamberBuilderTests
    {
        const int SeedCount = 120;

        static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
        };

        LevelLibrary _library;
        readonly List<ScriptableObject> _created = new();

        [SetUp]
        public void SetUp() => _library = BuildLibrary();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created) Object.DestroyImmediate(asset);
            _created.Clear();
        }

        T Make<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _created.Add(asset);
            return asset;
        }

        LevelLibrary BuildLibrary()
        {
            var crate = Make<PropDefinition>("crate");
            crate.id = "crate";
            crate.spriteNames = new[] { "crate" };
            crate.blocksMovement = true;
            crate.maxPerRoom = 4;

            var skull = Make<PropDefinition>("skull");
            skull.id = "skull";
            skull.spriteNames = new[] { "skull" };
            skull.placement = PropPlacement.AgainstNorthWall;

            var theme = Make<ChamberTheme>("theme");
            theme.id = "theme";
            theme.displayName = "Test Chamber";
            theme.grassChance = 0.5f;
            theme.propDensity = 0.8f;
            theme.props = new[] { crate, skull };

            var library = Make<LevelLibrary>("library");
            library.themes = new[] { theme };
            library.props = new[] { crate, skull };
            library.minRooms = 2;
            library.maxRooms = 4;
            library.templates = new[]
            {
                Template("entry", RoomRole.Entry, RoomShape.Rectangle),
                Template("combat", RoomRole.Combat, RoomShape.Pillared),
                Template("cross", RoomRole.Combat, RoomShape.Cross),
                Template("hall", RoomRole.Hall, RoomShape.Ellipse),
                Template("notch", RoomRole.Hall, RoomShape.Notched),
                Template("vault", RoomRole.Treasure, RoomShape.Rectangle)
            };
            return library;
        }

        RoomTemplate Template(string id, RoomRole role, RoomShape shape)
        {
            var template = Make<RoomTemplate>(id);
            template.id = id;
            template.role = role;
            template.shape = shape;
            template.minSize = new Vector2Int(11, 9);
            template.maxSize = new Vector2Int(17, 13);
            return template;
        }

        [Test]
        public void Build_AcrossSeedsKeepsEveryChamberConnectedAndExitReachable()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int depth = 1 + seed % 12;
                var plan = ChamberBuilder.Build(new System.Random(seed), depth, _library);
                var layout = plan.Layout;
                var entry = Vector2Int.RoundToInt(layout.EntryPoint);

                Assert.That(layout.Walkable, Does.Contain(entry), $"seed {seed} entry is not floor");
                Assert.That(layout.Doorway, Has.Count.EqualTo(4), $"seed {seed} doorway is not 2x2");
                Assert.That(layout.Doorway.All(layout.Walkable.Contains), Is.True,
                    $"seed {seed} doorway is sealed");
                Assert.That(layout.Walls.Any(layout.Walkable.Contains), Is.False,
                    $"seed {seed} has a wall on a floor cell");
                Assert.That(Reachable(layout, entry), Is.EqualTo(layout.Walkable.Count),
                    $"seed {seed} has floor the player cannot reach");
                Assert.That(layout.Doorway.All(cell => layout.GridBounds.Contains(cell)), Is.True,
                    $"seed {seed} doorway falls outside the wall band");
            }
        }

        [Test]
        public void Build_PlacesRoomsWithoutOverlapAndScalesCountWithDepth()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int depth = 1 + seed % 12;
                var rooms = ChamberBuilder.Build(new System.Random(seed), depth, _library).Layout.Rooms;

                Assert.That(rooms, Has.Count.InRange(1, _library.maxRooms), $"seed {seed} room count");
                for (int first = 0; first < rooms.Count; first++)
                for (int second = first + 1; second < rooms.Count; second++)
                    Assert.That(rooms[first].Bounds.Overlaps(rooms[second].Bounds), Is.False,
                        $"seed {seed} rooms {first} and {second} overlap");
            }
        }

        [Test]
        public void Build_KeepsSpawnAnchorsAndPropsOnOpenFloor()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var plan = ChamberBuilder.Build(new System.Random(seed), 1 + seed % 12, _library);
                var layout = plan.Layout;

                Assert.That(plan.SpawnAnchors, Is.Not.Empty, $"seed {seed} has nowhere to spawn enemies");
                foreach (var anchor in plan.SpawnAnchors)
                {
                    var cell = Vector2Int.RoundToInt(anchor);
                    Assert.That(layout.Walkable, Does.Contain(cell), $"seed {seed} spawn anchor is not floor");
                    Assert.That(plan.BlockedCells, Does.Not.Contain(cell),
                        $"seed {seed} spawn anchor sits inside a solid prop");
                }

                foreach (var prop in plan.Props)
                {
                    var cell = Vector2Int.RoundToInt(prop.Position - prop.Definition.offset);
                    Assert.That(layout.Walkable, Does.Contain(cell), $"seed {seed} prop is inside a wall");
                    Assert.That(layout.Corridors, Does.Not.Contain(cell), $"seed {seed} prop blocks a corridor");
                    Assert.That(layout.Doorway, Does.Not.Contain(cell), $"seed {seed} prop blocks the exit");
                }

                Assert.That(plan.Props.Select(prop => prop.Position).Distinct().Count(),
                    Is.EqualTo(plan.Props.Count), $"seed {seed} stacked two props on one cell");
            }
        }

        [Test]
        public void Build_AlwaysDressesTheChamberWithABanner()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var plan = ChamberBuilder.Build(new System.Random(seed), 1 + seed % 12, _library);
                Assert.That(plan.WallDecorations.Values.Any(x => x.Kind == WallDecorationKind.Banner), Is.True,
                    $"seed {seed} produced no banner");
                Assert.That(plan.WallDecorations.Keys.All(plan.Layout.Walls.Contains), Is.True,
                    $"seed {seed} decorated a cell that is not a wall");
            }
        }

        [Test]
        public void Build_ResolvesPillarsToFreestandingColumns()
        {
            var withColumns = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var plan = ChamberBuilder.Build(new System.Random(seed), 1 + seed % 12, _library);
                if (plan.ForcedColumns.Count > 0) withColumns++;
                Assert.That(plan.ForcedColumns.All(plan.Layout.Walls.Contains), Is.True,
                    $"seed {seed} marked a floor cell as a column");
                Assert.That(plan.ForcedColumns.Any(plan.Layout.Walkable.Contains), Is.False,
                    $"seed {seed} left a column standing on walkable floor");
            }
            Assert.That(withColumns, Is.GreaterThan(0), "no seed produced a pillared room");
        }

        [Test]
        public void Build_IsDeterministicForASeed()
        {
            var first = ChamberBuilder.Build(new System.Random(4242), 5, _library);
            var second = ChamberBuilder.Build(new System.Random(4242), 5, _library);

            Assert.That(second.Layout.Walkable, Is.EquivalentTo(first.Layout.Walkable));
            Assert.That(second.Layout.Walls, Is.EquivalentTo(first.Layout.Walls));
            Assert.That(second.Layout.EntryPoint, Is.EqualTo(first.Layout.EntryPoint));
            Assert.That(second.Layout.ExitDoorPosition, Is.EqualTo(first.Layout.ExitDoorPosition));
            Assert.That(second.Props.Count, Is.EqualTo(first.Props.Count));
        }

        static int Reachable(DungeonLayout layout, Vector2Int start)
        {
            var seen = new HashSet<Vector2Int> { start };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (layout.Walkable.Contains(next) && seen.Add(next)) pending.Enqueue(next);
                }
            }
            return seen.Count;
        }
    }
}
