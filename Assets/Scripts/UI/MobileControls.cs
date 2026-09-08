using System;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class MobileControls : MonoBehaviour
    {
        public static bool Enabled => Application.isMobilePlatform ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "--touch-controls") >= 0;

        DungeonGame _game;
        TouchStick _move;
        TouchStick _aim;
        UiButton _dash;
        UiButton _interact;
        Text _interactLabel;
        bool _dashRequested;

        public Vector2 Move => isActiveAndEnabled ? _move.Value : Vector2.zero;
        public Vector2 Aim => isActiveAndEnabled ? _aim.Value : Vector2.zero;

        public void Initialize(DungeonGame game)
        {
            _game = game;
            UiKit.Stretch((RectTransform)transform, 0f, 0f, 0f, 0f);
            _move = BuildStick("Move", new Vector2(0f, 0f), new Vector2(34f, 58f), "MOVE");
            _aim = BuildStick("Aim", new Vector2(1f, 0f), new Vector2(-34f, 58f), "AIM + FIRE");
            _dash = UiKit.PushButton("Dash", transform, "DASH", ButtonTone.Primary,
                () => _dashRequested = true, 16);
            UiKit.Corner(_dash.Rect, new Vector2(1f, 0f), new Vector2(-218f, 76f), new Vector2(118f, 74f));
            _interact = UiKit.PushButton("Interact", transform, "INTERACT", ButtonTone.Primary,
                () => NearestInteraction()?.Interact(), 16);
            UiKit.Corner(_interact.Rect, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(280f, 64f));
            _interactLabel = UiKit.ButtonLabel(_interact);
            _interact.gameObject.SetActive(false);
        }

        TouchStick BuildStick(string name, Vector2 anchor, Vector2 offset, string label)
        {
            var node = UiKit.Node(name, transform);
            UiKit.Corner(node, anchor, offset, new Vector2(160f, 160f));
            // Pointer positions are measured from the centre of each pad.
            node.anchoredPosition += new Vector2((0.5f - anchor.x) * 160f, 80f);
            node.pivot = new Vector2(0.5f, 0.5f);
            var stick = node.gameObject.AddComponent<TouchStick>();
            stick.Initialize(label);
            return stick;
        }

        InteractionZone NearestInteraction()
        {
            InteractionZone nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var zone in InteractionZone.Active)
            {
                if (zone == null || !zone.PlayerInRange) continue;
                float candidate = ((Vector2)zone.transform.position - _game.PlayerPosition).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = zone;
                distance = candidate;
            }
            return nearest;
        }

        public bool ConsumeDash()
        {
            bool requested = _dashRequested;
            _dashRequested = false;
            return requested;
        }

        public void ResetInput()
        {
            _move?.ResetInput();
            _aim?.ResetInput();
            _dashRequested = false;
        }

        void Update()
        {
            if (!_game.AcceptsGameplayInput) ResetInput();
            _dash.Interactable = _game.AcceptsGameplayInput && _game.Player != null && _game.Player.DashReady;
            var zone = NearestInteraction();
            _interact.gameObject.SetActive(zone != null);
            if (zone != null) _interactLabel.text = zone.Label.ToUpperInvariant();
        }

        void OnDisable() => ResetInput();
    }
}
