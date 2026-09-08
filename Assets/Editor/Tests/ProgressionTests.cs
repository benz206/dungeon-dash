using System.Collections;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class ProgressionTests
    {
        [TestCase(0, 0, false, 3, 10)]
        [TestCase(0, 0, true, 4, 10)]
        [TestCase(6, 25, false, 6, 25)]
        public void OlderCheckpoint_PreservesKnownProgressWithoutReducingRecords(
            int best, int kills, bool cleared, int expectedBest, int expectedKills)
        {
            var previous = SaveData.Load();
            try
            {
                var data = new SaveData();
                var slot = data.CreateSlot("knight");
                slot.bestChamberCleared = best;
                slot.lifetimeKills = kills;
                slot.run = new RunCheckpoint { wave = 4, kills = 10, health = 5 };
                if (!cleared) slot.run.enemies.Add(new RunCheckpoint.Enemy { skinId = "goblin", health = 5 });
                data.Save();
                var loaded = SaveData.Load().ActiveSlotOrNull;
                Assert.That(loaded.bestChamberCleared, Is.EqualTo(expectedBest));
                Assert.That(loaded.lifetimeKills, Is.EqualTo(expectedKills));
            }
            finally { previous.Save(); }
        }

        [UnityTest]
        public IEnumerator ClearedChambersEarnRank_AndResumingDoesNotCountKillsTwice()
        {
            yield return new EnterPlayMode();
            var game = Object.FindFirstObjectByType<DungeonGame>();
            game.SendMessage("StartRun", Resources.Load<GameCatalog>("GameCatalog").characters[0]);
            game.Save.ActiveSlotOrNull.bestChamberCleared = 0;
            game.Save.ActiveSlotOrNull.lifetimeKills = 0;
            Assert.That(game.GuildRank, Is.EqualTo("DELVER"));
            for (int room = 1; room <= 3; room++)
            {
                yield return null;
                foreach (var enemy in Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                    enemy.TakeDamage(9999);
                Assert.That(game.BestChamberCleared, Is.EqualTo(room));
                if (room == 3) break;
                game.SendMessage("BeginNextRoomTransition");
                for (int frame = 0; frame < 180 && game.TransitionActive; frame++) yield return null;
                Assert.That(game.TransitionActive, Is.False);
            }
            Assert.That(game.GuildRank, Is.EqualTo("SCOUT"));
            Assert.That(game.Save.ActiveSlotOrNull.lifetimeKills, Is.EqualTo(24));
            int index = game.Save.activeSlot;
            var saved = JsonUtility.ToJson(SaveData.Load().ActiveSlotOrNull);
            game.ReturnToHub();
            game.Save.slots[index] = JsonUtility.FromJson<SaveData.CharacterSlot>(saved);
            game.ContinueSlot(index);
            Assert.That(game.GuildRank, Is.EqualTo("SCOUT"));
            Assert.That(game.Save.ActiveSlotOrNull.lifetimeKills, Is.EqualTo(24));
            game.GameOver();
            game.ReturnToHub();
            Assert.That(SaveData.Load().ActiveSlotOrNull.bestChamberCleared, Is.EqualTo(3));
            yield return new ExitPlayMode();
        }
    }
}
