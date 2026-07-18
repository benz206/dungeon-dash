using System;
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

        DungeonGame _game;
        string _label;
        Action _onInteract;
        GUIStyle _promptStyle;

        public void Setup(DungeonGame game, string label, Action onInteract)
        {
            _game = game;
            _label = label;
            _onInteract = onInteract;
        }

        bool PlayerInRange =>
            _game != null && _game.WorldRunning &&
            ((Vector2)transform.position - _game.PlayerPosition).sqrMagnitude < Radius * Radius;

        void Update()
        {
            if (!PlayerInRange) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                GameAudio.Play("ui_click", 0.5f);
                _onInteract?.Invoke();
            }
        }

        void OnGUI()
        {
            if (!PlayerInRange || Camera.main == null) return;
            _promptStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                font = UiTheme.BodyFont
            };
            _promptStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.9f);
            if (screen.z < 0f) return;
            var rect = new Rect(screen.x - 70f, Screen.height - screen.y - 22f, 140f, 26f);
            UiTheme.DrawRect(rect, new Color(0.02f, 0.04f, 0.08f, 0.9f));
            UiTheme.DrawBorder(rect, new Color(0.3f, 0.55f, 0.78f, 0.95f), 1f);
            GUI.Label(rect, $"[E] {_label}", _promptStyle);
        }
    }
}
