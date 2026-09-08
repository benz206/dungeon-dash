using DungeonDash;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDashTests
{
    public sealed class GuildCosmeticsTests
    {
        [Test]
        public void EditorSaves_DoNotOverwriteNormalPlayerProgress()
        {
            const string playerKey = "DungeonDash.Save.v2";
            const string qaKey = "DungeonDash.QA.Save.v2";
            string playerBefore = PlayerPrefs.GetString(playerKey, string.Empty);
            bool playerExisted = PlayerPrefs.HasKey(playerKey);
            string qaBefore = PlayerPrefs.GetString(qaKey, string.Empty);
            bool qaExisted = PlayerPrefs.HasKey(qaKey);
            try
            {
                new SaveData { guildCosmeticsOwned = true, cosmeticStyle = 3 }.Save();
                Assert.That(SaveData.Load().cosmeticStyle, Is.EqualTo(3));
                Assert.That(PlayerPrefs.GetString(playerKey, string.Empty), Is.EqualTo(playerBefore));
                Assert.That(PlayerPrefs.HasKey(playerKey), Is.EqualTo(playerExisted));
            }
            finally
            {
                if (qaExisted) PlayerPrefs.SetString(qaKey, qaBefore);
                else PlayerPrefs.DeleteKey(qaKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void FreePlayers_CannotEquipPaidOrInvalidStyles()
        {
            var save = new SaveData();
            Assert.That(GuildCosmetics.CanEquip(save, 0), Is.True);
            for (int style = 1; style < GuildCosmetics.StyleCount; style++)
                Assert.That(GuildCosmetics.CanEquip(save, style), Is.False);
            Assert.That(GuildCosmetics.CanEquip(save, -1), Is.False);
            Assert.That(GuildCosmetics.CanEquip(save, GuildCosmetics.StyleCount), Is.False);
        }

        [Test]
        public void OwnershipAndSelection_SurviveSaveSerializationWithoutACharacterSlot()
        {
            var save = new SaveData { guildCosmeticsOwned = true, cosmeticStyle = 2 };
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.That(loaded.slots, Is.Empty);
            Assert.That(GuildCosmetics.Equipped(loaded), Is.EqualTo(2));
            Assert.That(GuildCosmetics.CanEquip(loaded, GuildCosmetics.StyleCount), Is.False);
        }

        [Test]
        public void MissingOwnershipOrInvalidSelection_AlwaysRendersClassic()
        {
            var save = new SaveData { cosmeticStyle = 2 };
            Assert.That(GuildCosmetics.Equipped(save), Is.Zero);
            save.guildCosmeticsOwned = true;
            save.cosmeticStyle = 100;
            Assert.That(GuildCosmetics.Equipped(save), Is.Zero);
        }

        [Test]
        public void UninitializedStore_CannotStartOrGrantAPurchase()
        {
            var node = new GameObject("Store Test");
            try
            {
                var store = node.AddComponent<CosmeticStore>();
                Assert.That(store.CanBuy, Is.False);
                store.Buy();
                store.Restore();
                Assert.That(store.Busy, Is.False);
                Assert.That(store.Price, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(node);
            }
        }
    }
}
