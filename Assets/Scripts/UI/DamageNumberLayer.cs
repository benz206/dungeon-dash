using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public enum DamageNumberKind { Normal, Critical, PlayerHurt }

    public sealed class DamageNumberLayer : MonoBehaviour
    {
        const int Capacity = 24;
        const float Duration = 0.7f;
        const float RiseDistance = 0.95f;

        struct Entry
        {
            public Vector2 World;
            public float StartTime;
            public bool Active;
        }

        static DamageNumberLayer _instance;

        readonly Entry[] _entries = new Entry[Capacity];
        readonly Text[] _labels = new Text[Capacity];
        Canvas _canvas;
        RectTransform _root;
        int _cursor;

        public static void Spawn(Vector2 worldPosition, int amount, DamageNumberKind kind)
        {
            if (_instance == null || Application.isBatchMode) return;
            _instance.Emit(worldPosition, amount, kind);
        }

        public void Initialize(Canvas canvas)
        {
            _instance = this;
            _canvas = canvas;
            _root = (RectTransform)transform;
            UiKit.Stretch(_root, 0f, 0f, 0f, 0f);
            for (int i = 0; i < Capacity; i++)
            {
                var label = UiKit.Label($"Damage {i}", _root, string.Empty, 20, Color.white,
                    TextAnchor.MiddleCenter, true);
                UiKit.Corner(label.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(140f, 30f));
                label.gameObject.SetActive(false);
                _labels[i] = label;
            }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Emit(Vector2 worldPosition, int amount, DamageNumberKind kind)
        {
            int index = _cursor;
            _cursor = (_cursor + 1) % Capacity;
            _entries[index] = new Entry { World = worldPosition, StartTime = Time.time, Active = true };

            var label = _labels[index];
            label.text = amount.ToString();
            label.fontSize = kind switch
            {
                DamageNumberKind.Critical => 30,
                DamageNumberKind.PlayerHurt => 22,
                _ => 19
            };
            label.color = kind switch
            {
                DamageNumberKind.Critical => new Color(1f, 0.74f, 0.18f),
                DamageNumberKind.PlayerHurt => new Color(1f, 0.34f, 0.34f),
                _ => Color.white
            };
            label.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            float scale = _canvas == null ? 1f : _canvas.scaleFactor;
            if (scale <= 0f) scale = 1f;

            for (int i = 0; i < Capacity; i++)
            {
                if (!_entries[i].Active) continue;
                float age = Time.time - _entries[i].StartTime;
                if (age >= Duration)
                {
                    _entries[i].Active = false;
                    _labels[i].gameObject.SetActive(false);
                    continue;
                }

                float t = age / Duration;
                Vector3 world = _entries[i].World + Vector2.up * (0.35f + t * RiseDistance);
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z < 0f)
                {
                    _labels[i].gameObject.SetActive(false);
                    continue;
                }

                var label = _labels[i];
                if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
                label.rectTransform.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale);
                label.color = label.color.Alpha(1f - t * t);
            }
        }
    }
}
