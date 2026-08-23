using System;

namespace DungeonDash
{
    public static class HeroNames
    {
        public static string Name(string id) => Base(id) switch
        {
            "wizzard" => "Wizard",
            "doc" => "Plague Doctor",
            var value => Pretty(value)
        };

        public static string Role(string id) => Base(id) switch
        {
            "knight" => "Vanguard",
            "elf" => "Pathfinder",
            "dwarf" => "Sentinel",
            "lizard" => "Skirmisher",
            "wizzard" => "Arcanist",
            _ => "Apothecary"
        };

        public static string Variant(string id) => id.EndsWith("_m", StringComparison.Ordinal) ? "II" : "I";

        static string Base(string id) => id.EndsWith("_m", StringComparison.Ordinal)
            ? id.Substring(0, id.Length - 2)
            : id;

        static string Pretty(string value)
        {
            value = value.Replace('_', ' ');
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
