using DungeonDash;
using NUnit.Framework;

namespace DungeonDashTests
{
    public sealed class WeaponRulesTests
    {
        [TestCase("weapon_regular_sword")]
        [TestCase("weapon_katana")]
        [TestCase("weapon_hammer")]
        public void MeleeWeapons_DealMoreThanTheirBaseDamage(string weaponId)
        {
            Assert.That(WeaponRules.IsRanged(weaponId), Is.False);
            Assert.That(WeaponRules.AdjustedDamage(weaponId, 10), Is.GreaterThan(10));
        }

        [TestCase("weapon_bow")]
        [TestCase("weapon_bow_2")]
        [TestCase("weapon_green_magic_staff")]
        [TestCase("weapon_throwing_axe")]
        public void RangedWeapons_TradeDamageForReach(string weaponId)
        {
            Assert.That(WeaponRules.IsRanged(weaponId), Is.True);
            Assert.That(WeaponRules.AdjustedDamage(weaponId, 10), Is.LessThan(10));
        }

        [Test]
        public void BowArtifacts_FireArrowVisuals_WithoutMakingArrowsArtifacts()
        {
            Assert.That(WeaponRules.ProjectileSpriteId("weapon_bow"), Is.EqualTo(WeaponRules.ArrowId));
            Assert.That(WeaponRules.ProjectileSpriteId("weapon_bow_2"), Is.EqualTo(WeaponRules.ArrowId));
            Assert.That(WeaponRules.IsArtifactWeapon(WeaponRules.ArrowId), Is.False);
            Assert.That(WeaponRules.IsArtifactWeapon("weapon_bow"), Is.True);
        }

        [Test]
        public void ArtifactGenerator_ConvertsLegacyArrowRollsToBows()
        {
            var artifact = ArtifactGenerator.Roll(WeaponRules.ArrowId, new System.Random(1));

            Assert.That(artifact.weaponId, Is.EqualTo("weapon_bow"));
            Assert.That(artifact.displayName, Is.EqualTo("Bow"));
        }

        [Test]
        public void ArtifactStats_ReportEffectiveWeaponDamage()
        {
            var sword = new Artifact { weaponId = "weapon_regular_sword", damage = 10 };
            var bow = new Artifact { weaponId = "weapon_bow", damage = 10 };

            Assert.That(sword.EffectiveDamage, Is.EqualTo(14));
            Assert.That(bow.EffectiveDamage, Is.EqualTo(8));
            Assert.That(sword.Stats, Does.StartWith("14 dmg"));
            Assert.That(bow.Stats, Does.StartWith("8 dmg"));
        }
    }
}
