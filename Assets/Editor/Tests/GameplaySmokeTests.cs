using System.Collections;
using System.Linq;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class GameplaySmokeTests
    {
        [UnityTest]
        public IEnumerator EveryCharacterSkin_StartsAPlayableRun()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            Assert.That(game, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(GameObject.Find("Arena"), Is.Not.Null);

            foreach (var skin in catalog.characters)
            {
                if (Object.FindFirstObjectByType<PlayerController>() != null)
                {
                    game.SendMessage("Restart");
                    yield return null;
                }

                game.SendMessage("StartRun", skin);
                yield return null;

                var player = Object.FindFirstObjectByType<PlayerController>();
                Assert.That(player, Is.Not.Null, $"{skin.id} did not create a player");
                Assert.That(skin.idle.Contains(player.GetComponent<SpriteRenderer>().sprite), Is.True,
                    $"{skin.id} did not use its own sprite set");
                Assert.That(Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None), Has.Length.EqualTo(6),
                    $"{skin.id} did not start the first wave");
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Combat_KillingAnEnemyAdvancesTheRun()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);
            yield return null;

            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
            Assert.That(enemies, Has.Length.EqualTo(6));
            enemies[0].TakeDamage(9999);
            yield return null;

            Assert.That(Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None), Has.Length.EqualTo(5));
            yield return new ExitPlayMode();
        }
    }
}
