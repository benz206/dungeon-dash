using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonDash
{
    // Godot is y-down; these names follow Godot's screen directions
    // (S = world down = toward the room a front-face wall overlooks).
    // Do not "fix" the signs.
    public struct NeighborMask
    {
        public bool N, S, E, W, NE, NW, SE, SW;

        public static NeighborMask From(Func<Vector2Int, bool> walkable, Vector2Int cell)
        {
            return new NeighborMask
            {
                N = walkable(cell + Vector2Int.up),
                S = walkable(cell + Vector2Int.down),
                E = walkable(cell + Vector2Int.right),
                W = walkable(cell + Vector2Int.left),
                NE = walkable(cell + Vector2Int.up + Vector2Int.right),
                NW = walkable(cell + Vector2Int.up + Vector2Int.left),
                SE = walkable(cell + Vector2Int.down + Vector2Int.right),
                SW = walkable(cell + Vector2Int.down + Vector2Int.left),
            };
        }
    }

    public enum WallDecorationKind
    {
        Banner,
        FlatDecor,
        Goo,
        GooBase,
        FountainTop,
        FountainMid,
        FountainBasin
    }

    public readonly struct WallDecoration
    {
        public string SpriteName { get; }
        public string[] AnimFrameNames { get; }
        public WallDecorationKind Kind { get; }

        public WallDecoration(string spriteName, string[] animFrameNames, WallDecorationKind kind)
        {
            SpriteName = spriteName;
            AnimFrameNames = animFrameNames;
            Kind = kind;
        }
    }

    public static class WallClassifier
    {
        // Mirrors Level.gd's WALL_TEXTURE_PATHS keys -> sprite basenames.
        public static readonly Dictionary<string, string> SemanticToSprite = new()
        {
            ["interior"] = "wall_mid",
            ["north"] = "wall_top_mid",
            ["north_left"] = "wall_top_left",
            ["north_right"] = "wall_top_right",
            ["south"] = "edge_down",
            ["south_left"] = "wall_edge_bottom_left",
            ["south_right"] = "wall_edge_bottom_right",
            ["west"] = "wall_left",
            ["east"] = "wall_right",
            ["edge_left"] = "wall_edge_left",
            ["edge_right"] = "wall_edge_right",
            ["edge_mid_left"] = "wall_edge_mid_left",
            ["edge_mid_right"] = "wall_edge_mid_right",
            ["edge_top_left"] = "wall_edge_top_left",
            ["edge_top_right"] = "wall_edge_top_right",
            ["tshape_left"] = "wall_edge_tshape_left",
            ["tshape_right"] = "wall_edge_tshape_right",
            ["tshape_bottom_left"] = "wall_edge_tshape_bottom_left",
            ["tshape_bottom_right"] = "wall_edge_tshape_bottom_right",
            ["outer_top_left"] = "wall_outer_top_left",
            ["outer_top_right"] = "wall_outer_top_right",
            ["outer_mid_left"] = "wall_outer_mid_left",
            ["outer_mid_right"] = "wall_outer_mid_right",
            ["outer_front_left"] = "wall_outer_front_left",
            ["outer_front_right"] = "wall_outer_front_right",
            ["column_wall"] = "column_wall",
            ["banner_blue"] = "wall_banner_blue",
            ["banner_green"] = "wall_banner_green",
            ["banner_red"] = "wall_banner_red",
            ["banner_yellow"] = "wall_banner_yellow",
            ["hole_1"] = "wall_hole_1",
            ["hole_2"] = "wall_hole_2",
            ["skull"] = "skull",
            ["goo"] = "wall_goo",
            ["goo_base"] = "wall_goo_base",
            ["fountain_top_1"] = "wall_fountain_top_1",
            ["fountain_top_2"] = "wall_fountain_top_2",
            ["fountain_top_3"] = "wall_fountain_top_3",
            ["fountain_mid_blue"] = "wall_fountain_mid_blue_anim_f0",
            ["fountain_mid_red"] = "wall_fountain_mid_red_anim_f0",
            ["fountain_basin_blue"] = "wall_fountain_basin_blue_anim_f0",
            ["fountain_basin_red"] = "wall_fountain_basin_red_anim_f0",
        };

        // The semantic keys Classify() can return (structural wall shapes only, no decoration overlays).
        static readonly string[] StructuralKeys =
        {
            "interior", "north", "north_left", "north_right", "south", "south_left", "south_right",
            "west", "east", "edge_left", "edge_right", "edge_mid_left", "edge_mid_right",
            "edge_top_left", "edge_top_right", "tshape_left", "tshape_right",
            "tshape_bottom_left", "tshape_bottom_right", "outer_top_left", "outer_top_right",
            "outer_mid_left", "outer_mid_right", "outer_front_left", "outer_front_right", "column_wall"
        };

        public static readonly IReadOnlyCollection<string> StructuralSpriteNames =
            StructuralKeys.Select(key => SemanticToSprite[key]).Distinct().ToArray();

        static readonly string[] WallFaceDecorKeys =
            { "banner_blue", "banner_green", "banner_red", "banner_yellow", "hole_1", "hole_2", "skull" };

        static readonly string[] WallFountainTopKeys = { "fountain_top_1", "fountain_top_2", "fountain_top_3" };

        static readonly string[] BannerColors = { "blue", "green", "red", "yellow" };

        // Transliteration of Level.gd's _classify_wall_tile. Branch order matters; do not reorder.
        public static string Classify(NeighborMask m, bool isDeepWall, int hash)
        {
            int orthogonalCount = (m.N ? 1 : 0) + (m.S ? 1 : 0) + (m.W ? 1 : 0) + (m.E ? 1 : 0);

            if (m.S && m.E && m.W && !m.N)
            {
                if (m.SE && !m.SW) return "tshape_bottom_left";
                if (m.SW && !m.SE) return "tshape_bottom_right";
                return "north";
            }

            if (m.N && m.E && m.W && !m.S)
            {
                if (m.NE && !m.NW) return "tshape_left";
                if (m.NW && !m.NE) return "tshape_right";
                return "south";
            }

            if ((m.N && m.S && !m.E && !m.W) || (m.E && m.W && !m.N && !m.S))
                return "column_wall";

            if (m.S && !m.N)
            {
                if (m.E && !m.W) return "north_left";
                if (m.W && !m.E) return "north_right";
                if (m.SE && !m.SW) return "edge_top_left";
                if (m.SW && !m.SE) return "edge_top_right";
                return "north";
            }

            if (m.N && !m.S)
            {
                if (m.E && !m.W) return "south_left";
                if (m.W && !m.E) return "south_right";
                return "south";
            }

            if (m.E && !m.W)
            {
                if (m.NE && m.SE) return "edge_mid_left";
                if (m.NE) return "edge_left";
                if (m.SE) return "outer_mid_left";
                return "west";
            }

            if (m.W && !m.E)
            {
                if (m.NW && m.SW) return "edge_mid_right";
                if (m.NW) return "edge_right";
                if (m.SW) return "outer_mid_right";
                return "east";
            }

            if (m.SE && !m.SW && !m.NE && !m.NW) return "outer_top_left";
            if (m.SW && !m.SE && !m.NE && !m.NW) return "outer_top_right";
            if (m.NE && !m.NW && !m.SE && !m.SW) return "outer_front_left";
            if (m.NW && !m.NE && !m.SE && !m.SW) return "outer_front_right";
            if (m.SE && m.NE && orthogonalCount == 0) return "outer_mid_left";
            if (m.SW && m.NW && orthogonalCount == 0) return "outer_mid_right";

            if (isDeepWall && hash % 10 == 0) return "column_wall";
            return "interior";
        }

        // Port of _is_deep_wall, with the Godot y-flip applied.
        public static bool IsDeepWall(Func<Vector2Int, bool> walkable, Vector2Int cell)
        {
            return !walkable(cell + Vector2Int.up * 2) && !walkable(cell + Vector2Int.down * 2) &&
                   !walkable(cell + Vector2Int.left * 2) && !walkable(cell + Vector2Int.right * 2);
        }

        // Port of _is_straight_front_wall, with the Godot y-flip applied.
        public static bool IsStraightFrontWall(Func<Vector2Int, bool> walkable, Vector2Int cell)
        {
            return walkable(cell + Vector2Int.down) &&
                   walkable(cell + Vector2Int.down + Vector2Int.left) &&
                   walkable(cell + Vector2Int.down + Vector2Int.right);
        }

        public static string SpriteName(Func<Vector2Int, bool> walkable, Vector2Int cell, Vector2Int gridLocalOrigin)
        {
            var mask = NeighborMask.From(walkable, cell);
            bool deep = IsDeepWall(walkable, cell);
            int hash = StableHash(cell - gridLocalOrigin, 17);
            return SemanticToSprite[Classify(mask, deep, hash)];
        }

        // Port of _stable_hash. Uses 32-bit unchecked math + a sign-bit mask instead of Godot's
        // 64-bit abs() (avoids the Math.Abs(int.MinValue) trap); only used mod small divisors so
        // this does not need to reproduce Godot's exact numeric values, only be deterministic.
        public static int StableHash(Vector2Int localCell, int salt)
        {
            unchecked
            {
                int value = localCell.x * 73856093;
                value ^= localCell.y * 19349663;
                value ^= salt * 83492791;
                return value & 0x7FFFFFFF;
            }
        }

        static string PickKey(string[] keys, Vector2Int local, int salt) => keys[StableHash(local, salt) % keys.Length];

        static bool InBounds(RectInt bounds, Vector2Int cell) =>
            cell.x >= bounds.xMin && cell.x < bounds.xMax && cell.y >= bounds.yMin && cell.y < bounds.yMax;

        // Port of _apply_wall_decorations (plus a Unity-only banner-guarantee pass, see below).
        public static Dictionary<Vector2Int, WallDecoration> PlanDecorations(
            Func<Vector2Int, bool> walkable, RectInt gridBounds, IReadOnlyList<RectInt> roomBounds)
        {
            var origin = new Vector2Int(gridBounds.xMin, gridBounds.yMin);
            var reserved = new HashSet<Vector2Int>();
            var decorations = new Dictionary<Vector2Int, WallDecoration>();

            // Godot scans y ascending (screen-top first, since Godot y increases downward).
            // World-up is Godot-north, so screen-top-first means Unity world-y descending here.
            for (int y = gridBounds.yMax - 1; y >= gridBounds.yMin; y--)
            for (int x = gridBounds.xMin; x < gridBounds.xMax; x++)
            {
                var cell = new Vector2Int(x, y);
                if (walkable(cell)) continue;

                var local = cell - origin;
                var mask = NeighborMask.From(walkable, cell);
                bool deep = IsDeepWall(walkable, cell);
                if (Classify(mask, deep, StableHash(local, 17)) != "north") continue;
                if (!IsStraightFrontWall(walkable, cell)) continue;

                int roll = StableHash(local, 101) % 1000;
                if (roll < 10 && CanPlaceFountain(walkable, gridBounds, reserved, cell))
                {
                    PlaceFountain(decorations, reserved, local, cell);
                    continue;
                }

                if (roll < 36 && CanPlaceGoo(walkable, gridBounds, reserved, cell))
                {
                    PlaceGoo(decorations, reserved, cell);
                    continue;
                }

                if (roll < 110 && !reserved.Contains(cell))
                {
                    string key = PickKey(WallFaceDecorKeys, local, 131);
                    decorations[cell] = new WallDecoration(SemanticToSprite[key], null, FlatDecorKind(key));
                    reserved.Add(cell);
                }
            }

            ApplyBannerGuarantee(walkable, gridBounds, roomBounds, decorations, reserved);
            return decorations;
        }

        static WallDecorationKind FlatDecorKind(string key) =>
            key.StartsWith("banner_") ? WallDecorationKind.Banner : WallDecorationKind.FlatDecor;

        static bool CanPlaceFountain(Func<Vector2Int, bool> walkable, RectInt gridBounds, HashSet<Vector2Int> reserved, Vector2Int cell)
        {
            for (int offset = 0; offset < 3; offset++)
            {
                var pos = cell + Vector2Int.up * offset;
                if (!InBounds(gridBounds, pos)) return false;
                if (reserved.Contains(pos) || walkable(pos)) return false;
            }
            return true;
        }

        static bool CanPlaceGoo(Func<Vector2Int, bool> walkable, RectInt gridBounds, HashSet<Vector2Int> reserved, Vector2Int cell)
        {
            var gooPos = cell + Vector2Int.up;
            if (!InBounds(gridBounds, gooPos)) return false;
            return !reserved.Contains(gooPos) && !reserved.Contains(cell) && !walkable(gooPos);
        }

        static void PlaceFountain(Dictionary<Vector2Int, WallDecoration> decorations, HashSet<Vector2Int> reserved, Vector2Int local, Vector2Int cell)
        {
            bool useBlue = StableHash(local, 151) % 2 == 0;
            string color = useBlue ? "blue" : "red";
            var topPos = cell + Vector2Int.up * 2;
            var midPos = cell + Vector2Int.up;
            var basinPos = cell;

            string topKey = PickKey(WallFountainTopKeys, local, 181);
            decorations[topPos] = new WallDecoration(SemanticToSprite[topKey], null, WallDecorationKind.FountainTop);
            decorations[midPos] = new WallDecoration(SemanticToSprite["fountain_mid_" + color], FountainFrames("mid", color), WallDecorationKind.FountainMid);
            decorations[basinPos] = new WallDecoration(SemanticToSprite["fountain_basin_" + color], FountainFrames("basin", color), WallDecorationKind.FountainBasin);

            reserved.Add(topPos);
            reserved.Add(midPos);
            reserved.Add(basinPos);
        }

        static string[] FountainFrames(string part, string color) => new[]
        {
            $"wall_fountain_{part}_{color}_anim_f0",
            $"wall_fountain_{part}_{color}_anim_f1",
            $"wall_fountain_{part}_{color}_anim_f2",
        };

        static void PlaceGoo(Dictionary<Vector2Int, WallDecoration> decorations, HashSet<Vector2Int> reserved, Vector2Int cell)
        {
            var gooPos = cell + Vector2Int.up;
            var basePos = cell;
            decorations[gooPos] = new WallDecoration(SemanticToSprite["goo"], null, WallDecorationKind.Goo);
            decorations[basePos] = new WallDecoration(SemanticToSprite["goo_base"], null, WallDecorationKind.GooBase);
            reserved.Add(gooPos);
            reserved.Add(basePos);
        }

        // Unity addition, not present in Level.gd: guarantees each non-first room gets a banner
        // on its north wall (if a valid, unreserved "north" cell exists), cycling through colors.
        static void ApplyBannerGuarantee(Func<Vector2Int, bool> walkable, RectInt gridBounds,
            IReadOnlyList<RectInt> roomBounds, Dictionary<Vector2Int, WallDecoration> decorations, HashSet<Vector2Int> reserved)
        {
            var origin = new Vector2Int(gridBounds.xMin, gridBounds.yMin);
            for (int i = 1; i < roomBounds.Count; i++)
            {
                var room = roomBounds[i];
                int y = room.yMax;
                int center = room.xMin + room.width / 2;

                Vector2Int? found = null;
                foreach (int x in AlternatingXs(center, room.xMin, room.xMax))
                {
                    var cell = new Vector2Int(x, y);
                    if (!InBounds(gridBounds, cell)) continue;
                    if (reserved.Contains(cell)) continue;
                    if (walkable(cell)) continue;

                    var mask = NeighborMask.From(walkable, cell);
                    bool deep = IsDeepWall(walkable, cell);
                    int hash = StableHash(cell - origin, 17);
                    if (Classify(mask, deep, hash) != "north") continue;

                    found = cell;
                    break;
                }

                if (found == null) continue;
                string key = "banner_" + BannerColors[(i - 1) % BannerColors.Length];
                decorations[found.Value] = new WallDecoration(SemanticToSprite[key], null, WallDecorationKind.Banner);
                reserved.Add(found.Value);
            }
        }

        static IEnumerable<int> AlternatingXs(int center, int min, int maxExclusive)
        {
            if (center >= min && center < maxExclusive) yield return center;
            for (int offset = 1; offset < maxExclusive - min; offset++)
            {
                int right = center + offset;
                int left = center - offset;
                bool rightInRange = right < maxExclusive;
                bool leftInRange = left >= min;
                if (!rightInRange && !leftInRange) yield break;
                if (rightInRange && right >= min) yield return right;
                if (leftInRange && left < maxExclusive) yield return left;
            }
        }
    }
}
