using System.Collections;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class FirstRunGuideTests
    {
        [UnityTest]
        public IEnumerator Guide_AdvancesFromRealActionsAndRemembersCompletion()
        {
            yield return new EnterPlayMode();
            var game = Object.FindFirstObjectByType<DungeonGame>();
            var skin = Resources.Load<GameCatalog>("GameCatalog").characters[0];
            game.SendMessage("StartRun", skin);
            game.ReturnToHub();
            game.Save.ActiveSlotOrNull.tutorialActions = 0;
            Assert.That(game.TutorialHint, Does.StartWith("MOVE"));
            game.Player.transform.position += Vector3.right * 2.1f;
            game.SendMessage("Update");
            Assert.That(game.TutorialHint, Does.StartWith("ENTER"));

            game.SendMessage("StartRun", skin);
            yield return null;
            Assert.That(game.TutorialHint, Does.StartWith("ATTACK"));
            game.UseWeapon(game.PlayerPosition, Vector2.right, 1, "weapon_bow",
                game.WeaponSprite("weapon_bow"), false);
            Assert.That(game.TutorialHint, Does.StartWith("EVADE"));
            game.Player.SendMessage("TryDash");
            Assert.That(game.TutorialHint, Does.StartWith("CLEAR"));
            foreach (var enemy in Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                enemy.TakeDamage(9999);
            Assert.That(game.TutorialHint, Does.StartWith("DESCEND"));
            game.SendMessage("BeginNextRoomTransition");
            for (int frame = 0; frame < 180 && game.TransitionActive; frame++) yield return null;
            Assert.That(game.TransitionActive, Is.False);
            Assert.That(game.TutorialHint, Is.Null);
            Assert.That(SaveData.Load().ActiveSlotOrNull.tutorialActions, Is.EqualTo((int)TutorialAction.All));
            game.ReturnToHub();
            Assert.That(game.TutorialHint, Is.Null);
            game.Save.ActiveSlotOrNull.tutorialActions = 0;
            game.HideTutorial();
            Assert.That(SaveData.Load().ActiveSlotOrNull.tutorialActions, Is.EqualTo((int)TutorialAction.All));
            yield return new ExitPlayMode();
        }
    }
}
