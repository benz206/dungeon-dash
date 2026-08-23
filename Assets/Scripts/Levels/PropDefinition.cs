using UnityEngine;

namespace DungeonDash
{
    public enum PropPlacement { OpenFloor, AgainstNorthWall, RoomCorner, RoomCenter }

    [CreateAssetMenu(menuName = "Dungeon Dash/Prop Definition")]
    public sealed class PropDefinition : ScriptableObject
    {
        public string id;
        public string[] spriteNames;
        public float framesPerSecond = 6f;
        public PropPlacement placement = PropPlacement.OpenFloor;
        public bool blocksMovement;
        [Min(0f)] public float weight = 1f;
        [Min(0)] public int maxPerRoom = 3;
        public Vector2 offset;
        [Min(0.1f)] public float scale = 1f;
        public Color tint = Color.white;
    }
}
