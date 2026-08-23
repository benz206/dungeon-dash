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
        public void NavField_RoutesAroundBlockedCells()
        {
            var field = new NavField();
            field.SetWalkable(new HashSet<Vector2Int>
            {
                new(0, 0), new(0, 1), new(1, 1), new(2, 1), new(2, 0)
            });
            field.Rebuild(new Vector2Int(2, 0));

            bool found = field.TryWaypoint(new Vector2(0f, 0f), new Vector2(2f, 0f), out Vector2 waypoint);

            Assert.That(found, Is.True);
            Assert.That(waypoint, Is.EqualTo(new Vector2(0f, 1f)));
        }

        [Test]
        public void NavField_RejectsAnUnreachableTarget()
        {
            var field = new NavField();
            field.SetWalkable(new HashSet<Vector2Int> { new(0, 0), new(2, 0) });
            field.Rebuild(new Vector2Int(2, 0));

            Assert.That(field.TryWaypoint(new Vector2(0f, 0f), new Vector2(2f, 0f), out _), Is.False);
        }

        [Test]
        public void NavField_SteersStraightAtTheTargetInsideItsOwnCell()
        {
            var field = new NavField();
            field.SetWalkable(new HashSet<Vector2Int> { new(0, 0), new(1, 0) });
            field.Rebuild(new Vector2Int(1, 0));

            Assert.That(field.TryWaypoint(new Vector2(1.1f, 0.2f), new Vector2(1.4f, 0.3f), out Vector2 waypoint), Is.True);
            Assert.That(waypoint, Is.EqualTo(new Vector2(1.4f, 0.3f)));
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
