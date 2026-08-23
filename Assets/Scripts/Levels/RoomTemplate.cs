using UnityEngine;

namespace DungeonDash
{
    public enum RoomShape { Rectangle, Cross, Ellipse, Pillared, Notched }
    public enum RoomRole { Entry, Combat, Treasure, Hall }

    [CreateAssetMenu(menuName = "Dungeon Dash/Room Template")]
    public sealed class RoomTemplate : ScriptableObject
    {
        public string id;
        public RoomRole role = RoomRole.Combat;
        public RoomShape shape = RoomShape.Rectangle;
        public Vector2Int minSize = new(9, 7);
        public Vector2Int maxSize = new(15, 11);
        [Min(0f)] public float weight = 1f;
        [Min(1)] public int minDepth = 1;
        [Range(0f, 2f)] public float propDensity = 1f;
        [Range(0f, 2f)] public float enemyShare = 1f;

        public Vector2Int RollSize(System.Random random) => new(
            random.Next(Mathf.Max(3, minSize.x), Mathf.Max(3, maxSize.x) + 1),
            random.Next(Mathf.Max(3, minSize.y), Mathf.Max(3, maxSize.y) + 1));
    }
}
