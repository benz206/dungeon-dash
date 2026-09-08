using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class EnemyAttackTests
    {
        // These tests enter play mode from an EditMode runner, which does not wait
        // on WaitForSeconds. Yield frames until game time has actually advanced.
        public static IEnumerator Advance(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        static DungeonGame StartRun()
        {
            var game = Object.FindFirstObjectByType<DungeonGame>();
            game.SendMessage("StartRun", Resources.Load<GameCatalog>("GameCatalog").characters[0]);
            foreach (var navigator in Object.FindObjectsByType<EnemyNavigator>(FindObjectsSortMode.None))
                navigator.enabled = false;
            return game;
        }

        static EnemyActor Caster(DungeonGame game)
        {
            var world = (BuiltWorld)typeof(DungeonGame).GetField("_world",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            var field = new NavField();
            field.SetWalkable(world.Walkable);
            Vector2 origin = game.PlayerPosition + Vector2.right * 4f;
            Assert.That(game.ProjectilePathClear(origin, game.PlayerPosition), Is.True);
            var skin = game.Catalog.Enemy("necro");
            var node = WorldBuilder.CreateSprite("Test caster", skin.idle[0], origin, 8);
            node.AddComponent<EnemyNavigator>().Setup(game, field, 0f, true);
            var actor = node.AddComponent<EnemyActor>();
            actor.Setup(game, skin, 5);
            return actor;
        }

        [Test]
        public void Walls_BlockLineOfSightAcrossAProjectileStep()
        {
            var field = new NavField();
            field.SetWalkable(new HashSet<Vector2Int> { new(0, 0), new(1, 0), new(3, 0) });
            Assert.That(field.HasLineOfSight(Vector2.zero, Vector2.right), Is.True);
            Assert.That(field.HasLineOfSight(Vector2.zero, Vector2.right * 3f), Is.False);
        }

        [Test]
        public void DiagonalShots_CannotClipThroughTheCornerOfAWall()
        {
            var field = new NavField();
            var cells = new HashSet<Vector2Int> { new(0, 0), new(0, 1), new(1, 1) };
            field.SetWalkable(cells);
            Assert.That(field.HasLineOfSight(Vector2.zero, Vector2.one), Is.False);
            Assert.That(field.HasLineOfSight(Vector2.one, Vector2.zero), Is.False);
            cells.Add(new Vector2Int(1, 0));
            Assert.That(field.HasLineOfSight(Vector2.zero, Vector2.one), Is.True);
        }

        [UnityTest]
        public IEnumerator CasterLocksItsAim_AndMovingOutOfTheLineAvoidsDamage()
        {
            yield return new EnterPlayMode();
            var game = StartRun();
            var caster = Caster(game);
            var navigator = caster.GetComponent<EnemyNavigator>();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!navigator.IsWindingUp && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(navigator.IsWindingUp, Is.True);
            Assert.That(Object.FindObjectsByType<EnemyBolt>(FindObjectsSortMode.None), Is.Empty);
            int health = game.Player.Health;
            game.Player.transform.position += Vector3.up * 2f;
            yield return Advance(1.8f);
            Assert.That(game.Player.Health, Is.EqualTo(health));
            Assert.That(Object.FindObjectsByType<EnemyBolt>(FindObjectsSortMode.None).Length, Is.GreaterThan(0));
            Object.Destroy(caster.gameObject);
            game.ReturnToHub();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PendingAttack_FreezesDuringPauseAndIsCancelledByDefeat()
        {
            yield return new EnterPlayMode();
            var game = StartRun();
            var caster = Caster(game);
            var navigator = caster.GetComponent<EnemyNavigator>();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!navigator.IsWindingUp && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(navigator.IsWindingUp, Is.True);
            game.SetPauseOpen(true);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(navigator.IsWindingUp, Is.True);
            Assert.That(Object.FindObjectsByType<EnemyBolt>(FindObjectsSortMode.None), Is.Empty);
            game.SetPauseOpen(false);
            caster.TakeDamage(9999);
            yield return Advance(1f);
            Assert.That(Object.FindObjectsByType<EnemyBolt>(FindObjectsSortMode.None), Is.Empty);
            game.ReturnToHub();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EnemyBolt_HitsAStationaryHeroAndStopsAtWalls()
        {
            yield return new EnterPlayMode();
            var game = StartRun();
            int health = game.Player.Health;
            var bolt = EnemyBolt.Fire(game, game.PlayerPosition + Vector2.right * 2f,
                game.PlayerPosition, null);
            yield return Advance(0.5f);
            Assert.That(bolt == null, Is.True);
            Assert.That(game.Player.Health, Is.EqualTo(health - 1));
            bolt = EnemyBolt.Fire(game, new Vector2(1000f, 1000f), game.PlayerPosition, null);
            yield return null;
            yield return null;
            Assert.That(bolt == null, Is.True, "A projectile outside the walkable chamber must be removed.");
            game.ReturnToHub();
            yield return new ExitPlayMode();
        }
    }
}
