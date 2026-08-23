using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonDash
{
    /// A hub portal: shows a "Press E" prompt when the player stands near it and
    /// invokes a callback on E. Proximity is a simple distance check against the
    /// player position — no physics triggers needed.
    public sealed class InteractionZone : MonoBehaviour
    {
        const float Radius = 1.1f;

        static readonly List<InteractionZone> _active = new();

        DungeonGame _game;
        Action _onInteract;

        public static IReadOnlyList<InteractionZone> Active => _active;

        public string Label { get; private set; } = string.Empty;

        public bool Available => _game != null && _game.WorldRunning;

        public bool PlayerInRange =>
            Available && ((Vector2)transform.position - _game.PlayerPosition).sqrMagnitude < Radius * Radius;

        public void Setup(DungeonGame game, string label, Action onInteract)
        {
            _game = game;
            Label = label;
            _onInteract = onInteract;
        }

        void OnEnable() => _active.Add(this);

        void OnDisable() => _active.Remove(this);

        void Update()
        {
            if (!PlayerInRange) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;
            GameAudio.Play("ui_click", 0.5f);
            _onInteract?.Invoke();
        }
    }
}
