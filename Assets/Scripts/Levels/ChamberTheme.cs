using UnityEngine;

namespace DungeonDash
{
    [CreateAssetMenu(menuName = "Dungeon Dash/Chamber Theme")]
    public sealed class ChamberTheme : ScriptableObject
    {
        public string id;
        public string displayName = "Chamber";
        [Range(0f, 1f)] public float floorWear = 0.25f;
        [Range(0f, 1f)] public float grassChance;
        [Range(0f, 1f)] public float propDensity = 0.35f;
        public Color floorTint = Color.white;
        public Color wallTint = Color.white;
        public Color accent = new(0.72f, 0.53f, 0.31f);
        public PropDefinition[] props;
    }
}
