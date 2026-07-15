using System.Collections;
using System.Collections.Generic;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class EnemyNavigatorTests
    {
        [Test]
        public void GridPathfinder_RoutesAroundBlockedCells()
        {
            var walkable = new HashSet<Vector2Int>
            {
                new(0, 0), new(0, 1), new(1, 1), new(2, 1), new(2, 0)
            };

            bool found = GridPathfinder.TryFindNextStep(walkable, new Vector2Int(0, 0),
                new Vector2Int(2, 0), out Vector2Int next);

            Assert.That(found, Is.True);
            Assert.That(next, Is.EqualTo(new Vector2Int(0, 1)));
        }

        [Test]
        public void GridPathfinder_RejectsAnUnreachableTarget()
        {
            var walkable = new HashSet<Vector2Int>
            {
                new(0, 0), new(2, 0)
            };

            Assert.That(GridPathfinder.TryFindNextStep(walkable, new Vector2Int(0, 0),
                new Vector2Int(2, 0), out _), Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyInRange_DamagesThePlayer()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            var enemy = Object.FindFirstObjectByType<EnemyActor>();
            int startingHealth = player.Health;
            enemy.transform.position = player.transform.position + Vector3.right * 0.5f;

            yield return new WaitForSeconds(0.1f);

            Assert.That(player.Health, Is.LessThan(startingHealth));
            yield return new ExitPlayMode();
        }
    }
}
