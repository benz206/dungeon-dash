using UnityEngine;

namespace DungeonDash
{
    public static class GuildCosmetics
    {
        public const string ProductId = "dungeon_dash.guild_cosmetics";
        public const int StyleCount = 4;

        public static string Name(int style) => style switch
        {
            1 => "EMBER CROWN",
            2 => "ASTRAL WISP",
            3 => "VERDANT SOUL",
            _ => "CLASSIC"
        };

        public static Color Color(int style) => style switch
        {
            1 => new Color(1f, 0.61f, 0.22f),
            2 => new Color(0.55f, 0.66f, 1f),
            3 => new Color(0.38f, 0.92f, 0.66f),
            _ => UnityEngine.Color.white
        };

        public static bool CanEquip(SaveData save, int style) =>
            style >= 0 && style < StyleCount && (style == 0 || save.guildCosmeticsOwned);

        public static int Equipped(SaveData save) => CanEquip(save, save.cosmeticStyle) ? save.cosmeticStyle : 0;

        public static Vector2 Orbit(int index, float time) => new(
            Mathf.Cos(time * 1.8f + index * Mathf.PI * 2f / 3f) * 0.62f,
            Mathf.Sin(time * 1.8f + index * Mathf.PI * 2f / 3f) * 0.28f - 0.25f);
    }
}
