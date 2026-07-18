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

            foreach (var skin in catalog.characters)
            {
                if (Object.FindFirstObjectByType<PlayerController>() != null)
                {
                    game.SendMessage("Restart");
                    yield return null;
                }

                game.SendMessage("StartRun", skin);
                yield return null;

                Assert.That(GameObject.Find("Arena"), Is.Not.Null, $"{skin.id} did not build the dungeon");
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

        [UnityTest]
        public IEnumerator Weapons_MeleeHitsNearbyTargets_AndBowsFireArrows()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
            enemies[0].transform.position = player.transform.position + Vector3.right;
            game.UseWeapon(player.transform.position + Vector3.right * 0.75f, Vector2.right, 9999,
                "weapon_regular_sword", game.WeaponSprite("weapon_regular_sword"), false);
            yield return null;
            Assert.That(Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None), Has.Length.EqualTo(5));

            game.UseWeapon(player.transform.position, Vector2.right, 5,
                "weapon_bow", game.WeaponSprite("weapon_bow"), false);
            var projectile = Object.FindFirstObjectByType<ProjectileActor>();
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("weapon_arrow"));

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PlayerDamage_GrantsBriefImmunity()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            player.TakeDamage(1, player.transform.position + Vector3.left);
            player.TakeDamage(1, player.transform.position + Vector3.left);
            Assert.That(player.Health, Is.EqualTo(player.MaxHealth - 1));

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ClearingAChamber_UnlocksDoorAndLoadsTheNextChunkThroughTransition()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);
            yield return null;

            var firstArena = GameObject.Find("Arena");
            var player = Object.FindFirstObjectByType<PlayerController>();
            foreach (var enemy in Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                enemy.TakeDamage(9999);
            yield return null;

            Assert.That(game.RoomExitUnlocked, Is.True);
            Assert.That(GameObject.Find("Exit Door Leaf").GetComponent<SpriteRenderer>().sprite.name,
                Is.EqualTo("doors_leaf_open"));
            Assert.That(GameObject.Find("Exit Door").GetComponent<InteractionZone>().enabled, Is.True);

            game.SendMessage("BeginNextRoomTransition");
            Assert.That(game.TransitionActive, Is.True);
            for (int frame = 0; frame < 180 && game.TransitionActive; frame++) yield return null;

            Assert.That(game.TransitionActive, Is.False, "room transition did not finish");
            Assert.That(game.CurrentRoom, Is.EqualTo(2));
            Assert.That(GameObject.Find("Arena"), Is.Not.SameAs(firstArena));
            Assert.That(Object.FindFirstObjectByType<PlayerController>(), Is.SameAs(player),
                "room loading should preserve the active player and health");
            Assert.That(Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None), Has.Length.EqualTo(8));

            yield return new ExitPlayMode();
        }
    }
}
