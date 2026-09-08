using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class RunCheckpointTests
    {
        static BuiltWorld World(DungeonGame game) => (BuiltWorld)typeof(DungeonGame)
            .GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        static DungeonGame StartRun()
        {
            var game = Object.FindFirstObjectByType<DungeonGame>();
            game.SendMessage("StartRun", Resources.Load<GameCatalog>("GameCatalog").characters[0]);
            return game;
        }

        static void ReloadSavedSlot(DungeonGame game)
        {
            int index = game.Save.activeSlot;
            var serialized = JsonUtility.ToJson(SaveData.Load().ActiveSlotOrNull);
            game.ReturnToHub();
            game.Save.slots[index] = JsonUtility.FromJson<SaveData.CharacterSlot>(serialized);
            game.ContinueSlot(index);
        }

        [UnityTest]
        public IEnumerator Restart_RebuildsSameChamberWithRemainingCombatState()
        {
            yield return new EnterPlayMode();
            var game = StartRun();
            yield return null; // Flush actors destroyed by the previous world.
            var walkable = new HashSet<Vector2Int>(World(game).Walkable);
            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
            enemies[0].TakeDamage(9999);
            enemies[1].TakeDamage(3);
            game.Player.RestoreHealth(4);
            game.Player.transform.position = (Vector2)walkable.First();
            game.SendMessage("OnApplicationPause", true);
            var expected = SaveData.Load().ActiveSlotOrNull.run;
            Assert.That(expected.enemies.Count, Is.EqualTo(enemies.Length - 1));
            int coins = game.Coins;

            ReloadSavedSlot(game);
            yield return null;
            Assert.That(game.Mode, Is.EqualTo(GameMode.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(game.Player.Health, Is.EqualTo(4));
            Assert.That(game.PlayerPosition, Is.EqualTo(expected.playerPosition));
            Assert.That(World(game).Walkable.SetEquals(walkable), Is.True);
            Assert.That(game.Coins, Is.EqualTo(coins));
            Assert.That(game.Kills, Is.EqualTo(expected.kills));
            Assert.That(game.RoomExitUnlocked, Is.False);
            game.PersistSave();
            Assert.That(JsonUtility.ToJson(game.Save.ActiveSlotOrNull.run),
                Is.EqualTo(JsonUtility.ToJson(expected)));
            game.ReturnToHub();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CollectedChest_StaysCollectedAfterRestartAndExitStaysOpen()
        {
            yield return new EnterPlayMode();
            var game = StartRun();
            yield return null;
            foreach (var enemy in Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                enemy.TakeDamage(9999);
            Assert.That(game.RoomExitUnlocked, Is.True);
            var chest = Object.FindObjectsByType<PickupActor>(FindObjectsSortMode.None)
                .First(pickup => pickup.Capture()?.kind == PickupKind.Chest);
            chest.transform.position = game.PlayerPosition;
            int coins = game.Coins;
            chest.SendMessage("Update");
            Assert.That(game.Coins, Is.EqualTo(coins + 8 + game.CurrentRoom));
            Assert.That(SaveData.Load().ActiveSlotOrNull.run.pickups.Any(p => p.kind == PickupKind.Chest), Is.False);
            int rewardedCoins = game.Coins;

            ReloadSavedSlot(game);
            yield return null;
            Assert.That(game.RoomExitUnlocked, Is.True);
            Assert.That(Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<PickupActor>(FindObjectsSortMode.None)
                .Any(p => p.Capture()?.kind == PickupKind.Chest), Is.False);
            Assert.That(game.Coins, Is.EqualTo(rewardedCoins));
            game.ReturnToHub();
            Assert.That(SaveData.Load().ActiveSlotOrNull.run?.CanResume ?? false, Is.False);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SuspensionDuringRoomRebuild_PreservesACompleteCheckpoint()
        {
            yield return new EnterPlayMode();
            var game = StartRun();
            yield return null;
            foreach (var enemy in Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                enemy.TakeDamage(9999);
            game.SendMessage("BeginNextRoomTransition");
            for (int frame = 0; frame < 180 && World(game) != null; frame++) yield return null;
            Assert.That(World(game), Is.Null, "Must interrupt the frame between the old and new rooms.");
            game.SendMessage("OnApplicationPause", true);
            var checkpoint = SaveData.Load().ActiveSlotOrNull.run;
            Assert.That(checkpoint.wave, Is.EqualTo(1));
            Assert.That(checkpoint.enemies, Is.Empty);
            for (int frame = 0; frame < 180 && game.TransitionActive; frame++) yield return null;
            yield return null;
            Assert.That(game.Mode, Is.EqualTo(GameMode.Paused));
            Assert.That(SaveData.Load().ActiveSlotOrNull.run.wave, Is.EqualTo(2));
            ReloadSavedSlot(game);
            Assert.That(game.CurrentRoom, Is.EqualTo(2));
            Assert.That(game.RoomExitUnlocked, Is.False);
            game.ReturnToHub();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DefeatAndIncompatibleCheckpoint_ReturnToHub()
        {
            yield return new EnterPlayMode();
            var game = StartRun();
            game.GameOver();
            Assert.That(SaveData.Load().ActiveSlotOrNull.run?.CanResume ?? false, Is.False);
            game.ContinueSlot(game.Save.activeSlot);
            Assert.That(game.Mode, Is.EqualTo(GameMode.HomeHub));
            game.Save.ActiveSlotOrNull.run = new RunCheckpoint { version = -1, health = 5, wave = 3 };
            game.ContinueSlot(game.Save.activeSlot);
            Assert.That(game.Mode, Is.EqualTo(GameMode.HomeHub));
            Assert.That(SaveData.Load().ActiveSlotOrNull.run?.CanResume ?? false, Is.False);
            yield return new ExitPlayMode();
        }
    }
}
