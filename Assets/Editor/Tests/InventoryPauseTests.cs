using System.Collections;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class InventoryPauseTests
    {
        [UnityTest]
        public IEnumerator Inventory_PausesAndRestoresThePreviousTimeScale()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);

            try
            {
                Time.timeScale = 0.35f;
                game.SendMessage("SetInventoryOpen", true);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(game.WorldRunning, Is.False);

                game.SendMessage("SetInventoryOpen", false);
                Assert.That(Time.timeScale, Is.EqualTo(0.35f));
                Assert.That(game.WorldRunning, Is.True);
            }
            finally
            {
                Time.timeScale = 1f;
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PauseMenu_PausesAndRestoresThePreviousTimeScale()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);

            try
            {
                Time.timeScale = 0.4f;
                game.SendMessage("SetPauseOpen", true);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(game.WorldRunning, Is.False);

                game.SendMessage("SetPauseOpen", false);
                Assert.That(Time.timeScale, Is.EqualTo(0.4f));
                Assert.That(game.WorldRunning, Is.True);
            }
            finally
            {
                Time.timeScale = 1f;
            }
            yield return new ExitPlayMode();
        }
    }
}
