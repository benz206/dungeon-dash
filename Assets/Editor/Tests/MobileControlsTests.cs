using System.Collections;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DungeonDashTests
{
    public sealed class MobileControlsTests
    {
        GameObject _events;
        GameObject _node;
        TouchStick _stick;

        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("Touch Test Events", typeof(EventSystem));
            _node = new GameObject("Touch Test Pad", typeof(RectTransform));
            _stick = _node.AddComponent<TouchStick>();
            _stick.Initialize("MOVE");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_node);
            Object.DestroyImmediate(_events);
        }

        PointerEventData Pointer(int id, Vector2 position) =>
            new(_events.GetComponent<EventSystem>()) { pointerId = id, position = position };

        [Test]
        public void Drag_ClampsMovementAndAppliesDeadZone()
        {
            var pointer = Pointer(1, new Vector2(540f, 540f));
            _stick.OnPointerDown(pointer);
            Assert.That(_stick.Value.magnitude, Is.EqualTo(1f).Within(0.001f));
            pointer.position = Vector2.one;
            _stick.OnDrag(pointer);
            Assert.That(_stick.Value, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SecondFinger_CannotStealOrReleaseAnOccupiedStick()
        {
            var first = Pointer(1, new Vector2(54f, 0f));
            var second = Pointer(2, new Vector2(0f, 54f));
            _stick.OnPointerDown(first);
            _stick.OnPointerDown(second);
            _stick.OnDrag(second);
            _stick.OnPointerUp(second);
            Assert.That(_stick.Value, Is.EqualTo(Vector2.right));
            _stick.OnPointerUp(first);
            Assert.That(_stick.Value, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ResetInput_ClearsHeldInputAndAllowsANewFinger()
        {
            _stick.OnPointerDown(Pointer(1, new Vector2(54f, 0f)));
            _stick.ResetInput();
            Assert.That(_stick.Value, Is.EqualTo(Vector2.zero));
            _stick.OnPointerDown(Pointer(2, new Vector2(0f, 54f)));
            Assert.That(_stick.Value, Is.EqualTo(Vector2.up));
        }
    }

    public sealed class MobileLifecycleTests
    {
        [UnityTest]
        public IEnumerator PauseDuringHitStop_ResumesAtNormalSpeed()
        {
            yield return new EnterPlayMode();
            var game = Object.FindFirstObjectByType<DungeonGame>();
            game.SendMessage("StartRun", Resources.Load<GameCatalog>("GameCatalog").characters[0]);
            var feel = Object.FindFirstObjectByType<GameFeel>();
            // HitStop is suppressed in batch mode; recreate its production state.
            typeof(GameFeel).GetField("_hitStopActive", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(feel, true);
            Time.timeScale = 0.05f;
            game.SetPauseOpen(true);
            Assert.That(Time.timeScale, Is.Zero);
            yield return null;
            game.SetPauseOpen(false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            game.ReturnToHub();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HidingControls_ReleasesHeldMovement()
        {
            yield return new EnterPlayMode();
            var node = new GameObject("Touch Test Pad", typeof(RectTransform));
            var stick = node.AddComponent<TouchStick>();
            stick.Initialize("MOVE");
            try
            {
                stick.OnPointerDown(new PointerEventData(EventSystem.current)
                    { pointerId = 1, position = new Vector2(54f, 0f) });
                Assert.That(stick.Value, Is.EqualTo(Vector2.right));
                node.SetActive(false);
                Assert.That(stick.Value, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.Destroy(node);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Suspension_SavesProgressAndWaitsForExplicitResume()
        {
            yield return new EnterPlayMode();
            var game = Object.FindFirstObjectByType<DungeonGame>();
            game.SendMessage("StartRun", Resources.Load<GameCatalog>("GameCatalog").characters[0]);
            int previousCoins = game.Coins;
            try
            {
                Time.timeScale = 0.4f;
                game.Coins += 7;
                game.SendMessage("OnApplicationPause", true);
                Assert.That(game.Mode, Is.EqualTo(GameMode.Paused));
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(SaveData.Load().ActiveSlotOrNull.coins, Is.EqualTo(previousCoins + 7));
                game.SendMessage("OnApplicationPause", false);
                Assert.That(game.Mode, Is.EqualTo(GameMode.Paused));
                game.SetPauseOpen(false);
                Assert.That(Time.timeScale, Is.EqualTo(0.4f));
            }
            finally
            {
                game.Coins = previousCoins;
                game.PersistSave();
                Time.timeScale = 1f;
            }
            yield return new ExitPlayMode();
        }
    }
}
