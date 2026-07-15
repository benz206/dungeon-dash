using System.Collections;
using System.Linq;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DungeonDashTests
{
    public sealed class DungeonViewportTests
    {
        [Test]
        public void CenterNow_PreservesDepthAndCentersCameraOnTarget()
        {
            var cameraObject = new GameObject("Camera");
            var target = new GameObject("Target");
            try
            {
                cameraObject.transform.position = new Vector3(12f, -4f, -10f);
                target.transform.position = new Vector3(-3f, 8f, 2f);
                var follow = cameraObject.AddComponent<PlayerCenteredCamera>();

                follow.SetTarget(target.transform);

                Assert.That(cameraObject.transform.position, Is.EqualTo(new Vector3(-3f, 8f, -10f)));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator Run_CentersCameraAndShowsCircularBottomLeftMinimap()
        {
            yield return new EnterPlayMode();

            var game = Object.FindFirstObjectByType<DungeonGame>();
            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            game.SendMessage("StartRun", catalog.characters[0]);
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            var viewport = Object.FindFirstObjectByType<DungeonViewportSystem>();
            var cameraFollow = Camera.main.GetComponent<PlayerCenteredCamera>();
            Assert.That(viewport, Is.Not.Null);
            Assert.That(cameraFollow, Is.Not.Null);
            Assert.That(cameraFollow.Target, Is.EqualTo(player.transform));
            Assert.That((Vector2)Camera.main.transform.position, Is.EqualTo((Vector2)player.transform.position));

            Assert.That(viewport.Minimap.activeSelf, Is.True);
            Assert.That(viewport.Minimap.GetComponent<Mask>(), Is.Not.Null);
            var minimapRect = viewport.Minimap.GetComponent<RectTransform>();
            Assert.That(minimapRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(minimapRect.anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(viewport.MinimapCamera.targetTexture, Is.Not.Null);
            Assert.That(viewport.Minimap.GetComponentsInChildren<Graphic>()
                .All(graphic => !graphic.raycastTarget), Is.True);

            yield return new ExitPlayMode();
        }
    }
}
