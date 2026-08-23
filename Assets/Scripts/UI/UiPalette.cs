using UnityEngine;

namespace DungeonDash
{
    public static class UiPalette
    {
        public static readonly Color Ink = new(0.020f, 0.022f, 0.030f);
        public static readonly Color Shadow = new(0.000f, 0.000f, 0.000f, 0.55f);
        public static readonly Color PanelFrame = new(0.088f, 0.098f, 0.135f);
        public static readonly Color PanelFill = new(0.132f, 0.148f, 0.196f);
        public static readonly Color PanelLight = new(0.300f, 0.330f, 0.415f);
        public static readonly Color PanelDark = new(0.055f, 0.062f, 0.086f);
        public static readonly Color InsetFill = new(0.062f, 0.072f, 0.100f);
        public static readonly Color RowFill = new(0.086f, 0.098f, 0.132f);
        public static readonly Color RowHover = new(0.128f, 0.150f, 0.200f);
        public static readonly Color RowSelected = new(0.160f, 0.200f, 0.272f);
        public static readonly Color Cream = new(0.945f, 0.918f, 0.824f);
        public static readonly Color Muted = new(0.560f, 0.585f, 0.640f);
        public static readonly Color Gold = new(0.855f, 0.663f, 0.318f);
        public static readonly Color Crimson = new(0.612f, 0.106f, 0.145f);
        public static readonly Color Ember = new(0.918f, 0.412f, 0.196f);
        public static readonly Color Steel = new(0.404f, 0.588f, 0.706f);
        public static readonly Color Verdant = new(0.408f, 0.706f, 0.365f);

        public static Color Rarity(string rarity) => rarity switch
        {
            "Mythic" => new Color(1f, 0.58f, 0.18f),
            "Epic" => new Color(0.72f, 0.40f, 1f),
            "Rare" => new Color(0.20f, 0.68f, 1f),
            _ => new Color(0.60f, 0.68f, 0.76f)
        };

        public static Color Alpha(this Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);

        public static Color Scale(this Color color, float amount) =>
            new(color.r * amount, color.g * amount, color.b * amount, color.a);
    }
}
