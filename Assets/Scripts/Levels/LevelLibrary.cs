using System.Linq;
using UnityEngine;

namespace DungeonDash
{
    [CreateAssetMenu(menuName = "Dungeon Dash/Level Library")]
    public sealed class LevelLibrary : ScriptableObject
    {
        public const string ResourcePath = "LevelLibrary";

        public ChamberTheme[] themes;
        public RoomTemplate[] templates;
        public PropDefinition[] props;
        [Min(1)] public int minRooms = 2;
        [Min(1)] public int maxRooms = 5;

        public bool IsUsable => themes is { Length: > 0 } && templates is { Length: > 0 };

        public ChamberTheme ThemeForDepth(int depth) =>
            themes[Mathf.Abs(depth - 1) / 2 % themes.Length];

        public int RoomCountForDepth(int depth) =>
            Mathf.Clamp(minRooms + (depth - 1) / 2, minRooms, maxRooms);

        public RoomTemplate PickTemplate(System.Random random, int depth, RoomRole role)
        {
            var pool = templates.Where(x => x != null && x.role == role && x.minDepth <= depth).ToArray();
            if (pool.Length == 0) pool = templates.Where(x => x != null && x.minDepth <= depth).ToArray();
            if (pool.Length == 0) pool = templates.Where(x => x != null).ToArray();
            return WeightedPick(pool, random, x => x.weight);
        }

        public static T WeightedPick<T>(T[] pool, System.Random random, System.Func<T, float> weight)
            where T : class
        {
            if (pool == null || pool.Length == 0) return null;
            float total = pool.Sum(weight);
            if (total <= 0f) return pool[random.Next(pool.Length)];
            double roll = random.NextDouble() * total;
            foreach (var candidate in pool)
            {
                roll -= weight(candidate);
                if (roll <= 0d) return candidate;
            }
            return pool[pool.Length - 1];
        }
    }
}
