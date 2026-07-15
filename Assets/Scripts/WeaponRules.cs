using UnityEngine;

namespace DungeonDash
{
    public static class WeaponRules
    {
        public const string ArrowId = "weapon_arrow";

        public static bool IsArtifactWeapon(string weaponId) => weaponId != ArrowId;

        public static bool IsRanged(string weaponId) =>
            weaponId != null && (weaponId.Contains("bow") || weaponId.Contains("magic_staff") ||
                                 weaponId.Contains("throwing_axe"));

        public static string ProjectileSpriteId(string weaponId) =>
            weaponId != null && weaponId.Contains("bow") ? ArrowId : weaponId;

        public static int AdjustedDamage(string weaponId, int baseDamage) =>
            Mathf.Max(1, Mathf.RoundToInt(baseDamage * (IsRanged(weaponId) ? 0.75f : 1.35f)));
    }
}
