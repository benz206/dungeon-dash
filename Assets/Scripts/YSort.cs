using UnityEngine;

namespace DungeonDash
{
    // Shared Y-sort formula so player/enemy/pickup renderers order correctly against
    // each other while floor/wall sortingOrder constants (all << Base) stay untouched.
    public static class YSort
    {
        const int Base = 1000;
        const float Scale = 16f;

        public static int Order(float y, int tie) => Base - Mathf.RoundToInt(y * Scale) + tie;
    }
}
