using UnityEngine;

namespace DungeonDash
{
    public enum DamageNumberKind { Normal, Critical, PlayerHurt }

    // Pooled, world-anchored floating damage text drawn from DungeonGame.OnGUI via Draw().
    public static class FloatingDamageNumbers
    {
        struct Entry
        {
            public Vector2 WorldPosition;
            public string Text;
            public DamageNumberKind Kind;
            public float StartTime;
            public bool Active;
        }

        const int Capacity = 24;
        const float Duration = 0.7f;

        static readonly Entry[] _entries = new Entry[Capacity];
        static int _cursor;

        static GUIStyle _normalStyle;
        static GUIStyle _criticalStyle;
        static GUIStyle _hurtStyle;

        public static void Spawn(Vector2 worldPosition, int amount, DamageNumberKind kind)
        {
            if (Application.isBatchMode) return;
            _entries[_cursor].WorldPosition = worldPosition;
            _entries[_cursor].Text = amount.ToString();
            _entries[_cursor].Kind = kind;
            _entries[_cursor].StartTime = Time.time;
            _entries[_cursor].Active = true;
            _cursor = (_cursor + 1) % Capacity;
        }

        public static void Draw(Camera camera, float uiScale)
        {
            if (camera == null) return;
            EnsureStyles();
            for (int i = 0; i < Capacity; i++)
            {
                if (!_entries[i].Active) continue;
                float age = Time.time - _entries[i].StartTime;
                if (age >= Duration)
                {
                    _entries[i].Active = false;
                    continue;
                }

                float t = age / Duration;
                Vector3 world = _entries[i].WorldPosition + Vector2.up * (0.35f + t * 0.6f);
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z < 0f) continue;

                var style = _entries[i].Kind switch
                {
                    DamageNumberKind.Critical => _criticalStyle,
                    DamageNumberKind.PlayerHurt => _hurtStyle,
                    _ => _normalStyle
                };
                var state = style.normal;
                var color = state.textColor;
                color.a = 1f - t;
                state.textColor = color;
                style.normal = state;

                float x = screen.x / uiScale - 40f;
                float y = (Screen.height - screen.y) / uiScale - 14f;
                GUI.Label(new Rect(x, y, 80f, 28f), _entries[i].Text, style);
            }
        }

        static void EnsureStyles()
        {
            if (_normalStyle != null) return;
            _normalStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                font = UiTheme.BodyFont
            };
            _normalStyle.normal.textColor = Color.white;
            _criticalStyle = new GUIStyle(_normalStyle) { fontSize = 27 };
            _criticalStyle.normal.textColor = new Color(1f, 0.72f, 0.15f);
            _hurtStyle = new GUIStyle(_normalStyle) { fontSize = 20 };
            _hurtStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
        }
    }
}
