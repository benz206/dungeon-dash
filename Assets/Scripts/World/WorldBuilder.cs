using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DungeonDash
{
    public sealed class WorldBuilder
    {
        const int FloorSortingOrder = -20;
        const int FlatPropSortingOrder = -19;
        const int HubHalfWidth = 12;
        const int HubHalfHeight = 7;

        static readonly Vector2Int[] CardinalOffsets =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        readonly CatalogIndex _catalog;
        readonly Dictionary<Sprite, Tile> _tiles = new();
        readonly Dictionary<PropDefinition, Sprite[]> _propFrames = new();
        readonly Dictionary<Vector2Int, int> _wallDistances = new();
        readonly Queue<Vector2Int> _distanceFrontier = new();

        public WorldBuilder(CatalogIndex catalog) => _catalog = catalog;

        public BuiltWorld BuildChamber(DungeonGame game, ChamberPlan plan, Action onExit)
        {
            var layout = plan.Layout;
            var world = new BuiltWorld
            {
                Root = new GameObject("Arena").transform,
                EntryPoint = layout.EntryPoint,
                Theme = plan.Theme
            };
            world.Walkable.UnionWith(layout.Walkable);
            world.Walkable.ExceptWith(plan.BlockedCells);
            world.SpawnAnchors.AddRange(plan.SpawnAnchors);

            BuildFloor(world.Root, plan);
            BuildWalls(world.Root, plan);
            BuildProps(world.Root, plan);
            world.ExitDoor = BuildDoor(game, world.Root, layout.ExitDoorPosition, "Exit Door",
                "NEXT CHAMBER", onExit, blocking: true);
            return world;
        }

        // A small hand-built room the player returns to between dungeon runs. Two portals sit inside.
        public BuiltWorld BuildHub(DungeonGame game, Action onMarket, Action onDungeon)
        {
            var world = new BuiltWorld { Root = new GameObject("Hub").transform, EntryPoint = Vector2.zero };
            var floorSprite = _catalog.Catalog.floors[0];
            var floorCells = new List<Vector2Int>();

            for (int x = -HubHalfWidth; x <= HubHalfWidth; x++)
            for (int y = -HubHalfHeight; y <= HubHalfHeight; y++)
            {
                var cell = new Vector2Int(x, y);
                bool border = x == -HubHalfWidth || x == HubHalfWidth ||
                              y == -HubHalfHeight || y == HubHalfHeight;
                if (border)
                {
                    string wallName = HubWallName(x, y);
                    var wall = CreateWall(cell, _catalog.Tile(wallName),
                        $"Hub Wall {wallName} {x},{y}", world.Root, Color.white);
                    wall.AddComponent<BoxCollider2D>();
                }
                else
                {
                    floorCells.Add(cell);
                    world.Walkable.Add(cell);
                }
            }

            PaintFloor(world.Root, floorCells, Color.white,
                cell => floorSprite, "Hub Floor");

            CreateHubZone(game, world.Root, "MARKET", new Vector2(-5f, 3f), onMarket);
            BuildDoor(game, world.Root, new Vector2(5f, HubHalfHeight - 2f), "Hub Door",
                "DUNGEON", onDungeon, blocking: false);
            return world;
        }

        static string HubWallName(int x, int y)
        {
            if (y == HubHalfHeight)
                return x == -HubHalfWidth ? "wall_top_left" : x == HubHalfWidth ? "wall_top_right" : "wall_top_mid";
            if (y == -HubHalfHeight)
                return x == -HubHalfWidth ? "wall_edge_bottom_left"
                    : x == HubHalfWidth ? "wall_edge_bottom_right" : "edge_down";
            return x == -HubHalfWidth ? "wall_left" : "wall_right";
        }

        void CreateHubZone(DungeonGame game, Transform parent, string label, Vector2 position, Action onInteract)
        {
            var chests = _catalog.Catalog.chests;
            var marker = CreateSprite($"Zone {label}", chests.Length > 0 ? chests[0] : null, position, 6, parent);
            marker.transform.localScale = Vector3.one * 1.1f;
            marker.AddComponent<InteractionZone>().Setup(game, label, onInteract);
        }

        void BuildFloor(Transform parent, ChamberPlan plan)
        {
            var layout = plan.Layout;
            float wear = plan.Theme == null ? 0.25f : plan.Theme.floorWear;
            var paths = _catalog.Catalog.paths;
            var grass = _catalog.Catalog.grass;

            PaintFloor(parent, layout.Walkable, plan.Theme == null ? Color.white : plan.Theme.floorTint, cell =>
            {
                if (layout.Corridors.Contains(cell) && paths.Length > 0)
                    return DungeonTileSelector.SelectPath(paths, cell);
                if (plan.GrassCells.Contains(cell) && grass.Length > 0)
                    return DungeonTileSelector.SelectGrass(grass, cell);
                float age = layout.FloorAge.TryGetValue(cell, out float value) ? value : 0.2f;
                return _catalog.Floor(Mathf.Clamp01((age + wear) * 0.5f), cell);
            }, "Floor");
        }

        void PaintFloor(Transform parent, ICollection<Vector2Int> cells, Color tint,
            Func<Vector2Int, Sprite> spriteFor, string name)
        {
            if (cells.Count == 0) return;

            var gridObject = new GameObject($"{name} Grid", typeof(Grid));
            gridObject.transform.SetParent(parent, false);
            var grid = gridObject.GetComponent<Grid>();
            grid.cellSize = Vector3.one;
            grid.cellGap = Vector3.zero;

            var mapObject = new GameObject($"{name} Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            mapObject.transform.SetParent(gridObject.transform, false);
            var tilemap = mapObject.GetComponent<Tilemap>();
            tilemap.tileAnchor = Vector3.zero;
            tilemap.color = tint;
            mapObject.GetComponent<TilemapRenderer>().sortingOrder = FloorSortingOrder;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var cell in cells)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            var block = new TileBase[width * height];
            foreach (var cell in cells)
                block[(cell.y - minY) * width + (cell.x - minX)] = TileFor(spriteFor(cell));
            tilemap.SetTilesBlock(new BoundsInt(minX, minY, 0, width, height, 1), block);
        }

        Tile TileFor(Sprite sprite)
        {
            if (sprite == null) return null;
            if (_tiles.TryGetValue(sprite, out var tile)) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = sprite.name;
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            _tiles[sprite] = tile;
            return tile;
        }

        void BuildWalls(Transform parent, ChamberPlan plan)
        {
            var layout = plan.Layout;
            var wallTint = plan.Theme == null ? Color.white : plan.Theme.wallTint;
            ComputeWallDistances(layout);

            foreach (var cell in layout.Walls)
            {
                bool forcedColumn = plan.ForcedColumns.Contains(cell);
                bool hasDecoration = plan.WallDecorations.TryGetValue(cell, out var decoration) && !forcedColumn;
                string spriteName = forcedColumn ? "column_wall"
                    : hasDecoration ? decoration.SpriteName
                    : DungeonTileSelector.WallSpriteName(layout, cell);
                string prefix = forcedColumn || !hasDecoration ? "Wall"
                    : decoration.Kind == WallDecorationKind.Banner ? "Banner"
                    : "Decor";

                int distance = hasDecoration ? 1 : _wallDistances.GetValueOrDefault(cell, int.MaxValue);
                float brightness = Mathf.Lerp(1f, 0.46f, Mathf.InverseLerp(1f, 4f, distance));
                var tint = wallTint * brightness;
                tint.a = 1f;

                var sprite = _catalog.Tile(spriteName);
                bool hasCollider = HasWalkableNeighbor(layout, cell);

                GameObject wallObject;
                if (spriteName == "column_wall")
                {
                    wallObject = CreateColumnWall(cell, sprite, $"{prefix} {spriteName}", parent, tint, hasCollider);
                }
                else
                {
                    wallObject = CreateWall(cell, sprite, $"{prefix} {spriteName}", parent, tint);
                    if (hasCollider) wallObject.AddComponent<BoxCollider2D>();
                }

                if (!hasDecoration || decoration.AnimFrameNames == null) continue;
                if (decoration.Kind != WallDecorationKind.FountainMid &&
                    decoration.Kind != WallDecorationKind.FountainBasin) continue;

                var frames = new Sprite[decoration.AnimFrameNames.Length];
                for (int i = 0; i < frames.Length; i++) frames[i] = _catalog.Tile(decoration.AnimFrameNames[i]);
                wallObject.GetComponent<SpriteRenderer>().sprite = frames[0];
                var loop = wallObject.AddComponent<SpriteFrameLoop>();
                loop.Frames = frames;
                loop.Fps = 5f;
            }
        }

        void BuildProps(Transform parent, ChamberPlan plan)
        {
            foreach (var placed in plan.Props)
            {
                var definition = placed.Definition;
                var frames = FramesFor(definition);
                if (frames.Length == 0) continue;

                int order = definition.blocksMovement
                    ? YSort.Order(placed.Position.y - definition.offset.y, 0)
                    : FlatPropSortingOrder;
                var propObject = CreateSprite($"Prop {definition.id}", frames[0], placed.Position, order, parent);
                propObject.transform.localScale = Vector3.one * definition.scale;
                propObject.GetComponent<SpriteRenderer>().color = definition.tint;

                if (frames.Length > 1 && definition.framesPerSecond > 0f)
                {
                    var loop = propObject.AddComponent<SpriteFrameLoop>();
                    loop.Frames = frames;
                    loop.Fps = definition.framesPerSecond;
                }

                if (!definition.blocksMovement) continue;
                var collider = propObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.86f, 0.72f);
                collider.offset = new Vector2(0f, -definition.offset.y);
            }
        }

        Sprite[] FramesFor(PropDefinition definition)
        {
            if (_propFrames.TryGetValue(definition, out var cached)) return cached;
            var frames = new List<Sprite>();
            if (definition.spriteNames != null)
                foreach (var name in definition.spriteNames)
                {
                    var sprite = _catalog.Tile(name);
                    if (sprite != null) frames.Add(sprite);
                }
            cached = frames.ToArray();
            _propFrames[definition] = cached;
            return cached;
        }

        ExitDoor BuildDoor(DungeonGame game, Transform parent, Vector2 center, string name,
            string label, Action onInteract, bool blocking)
        {
            int order = YSort.Order(center.y - 1f, 0);
            CreateSprite($"{name} Frame Left", _catalog.Tile("doors_frame_left"),
                center + Vector2.left * 1.5f, order, parent);
            CreateSprite($"{name} Frame Top", _catalog.Tile("doors_frame_top"),
                center + Vector2.up * 1.5f, order, parent);
            CreateSprite($"{name} Frame Right", _catalog.Tile("doors_frame_right"),
                center + Vector2.right * 1.5f, order, parent);
            var leaf = CreateSprite($"{name} Leaf", _catalog.Tile("doors_leaf_closed"), center, order + 1, parent);

            BoxCollider2D blocker = null;
            if (blocking)
            {
                blocker = leaf.AddComponent<BoxCollider2D>();
                blocker.offset = new Vector2(0f, -0.55f);
                blocker.size = new Vector2(1.8f, 0.7f);
            }

            var interaction = new GameObject(blocking ? name : $"Zone {label}");
            interaction.transform.position = center + Vector2.down * (blocking ? 1f : 1.7f);
            interaction.transform.SetParent(parent);
            var zone = interaction.AddComponent<InteractionZone>();
            zone.Setup(game, label, onInteract);

            if (!blocking) return null;
            return new ExitDoor(leaf.GetComponent<SpriteRenderer>(), blocker, zone,
                _catalog.Tile("doors_leaf_open"));
        }

        void ComputeWallDistances(DungeonLayout layout)
        {
            _wallDistances.Clear();
            _distanceFrontier.Clear();
            foreach (var wall in layout.Walls)
            {
                if (!HasWalkableNeighbor(layout, wall)) continue;
                _wallDistances[wall] = 1;
                _distanceFrontier.Enqueue(wall);
            }

            while (_distanceFrontier.Count > 0)
            {
                var cell = _distanceFrontier.Dequeue();
                int next = _wallDistances[cell] + 1;
                foreach (var offset in CardinalOffsets)
                {
                    var neighbor = cell + offset;
                    if (!layout.Walls.Contains(neighbor) || _wallDistances.ContainsKey(neighbor)) continue;
                    _wallDistances[neighbor] = next;
                    _distanceFrontier.Enqueue(neighbor);
                }
            }
        }

        static bool HasWalkableNeighbor(DungeonLayout layout, Vector2Int cell)
        {
            foreach (var offset in CardinalOffsets)
                if (layout.Walkable.Contains(cell + offset)) return true;
            return false;
        }

        static GameObject CreateWall(Vector2Int cell, Sprite sprite, string name, Transform parent, Color tint)
        {
            var position = new Vector2(cell.x, cell.y);
            var wall = CreateSprite(name, sprite, position, YSort.Order(position.y, 0), parent);
            wall.GetComponent<SpriteRenderer>().color = tint;
            return wall;
        }

        static GameObject CreateColumnWall(Vector2Int cell, Sprite sprite, string name, Transform parent,
            Color tint, bool hasCollider)
        {
            var position = new Vector2(cell.x, cell.y);
            var column = new GameObject(name);
            column.transform.position = position;
            column.transform.SetParent(parent);
            if (hasCollider) column.AddComponent<BoxCollider2D>().size = Vector2.one;
            var child = CreateSprite(name, sprite, position + new Vector2(0f, -1f),
                YSort.Order(position.y - 2f, 0), column.transform);
            child.GetComponent<SpriteRenderer>().color = tint;
            return column;
        }

        public static GameObject CreateSprite(string name, Sprite sprite, Vector2 position, int order,
            Transform parent = null)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.position = position;
            if (parent != null) spriteObject.transform.SetParent(parent);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return spriteObject;
        }
    }
}
