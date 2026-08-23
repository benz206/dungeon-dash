using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class PromptLayer : MonoBehaviour
    {
        const int Capacity = 6;
        const float Width = 176f;
        const float Height = 38f;
        const float WorldLift = 1.05f;

        sealed class Prompt
        {
            public RectTransform Root;
            public Image Frame;
            public Image KeyCap;
            public Text Key;
            public Text Label;
        }

        readonly Prompt[] _prompts = new Prompt[Capacity];
        Canvas _canvas;

        public void Initialize(Canvas canvas)
        {
            _canvas = canvas;
            var root = (RectTransform)transform;
            UiKit.Stretch(root, 0f, 0f, 0f, 0f);
            for (int i = 0; i < Capacity; i++) _prompts[i] = BuildPrompt(root, i);
        }

        static Prompt BuildPrompt(RectTransform parent, int index)
        {
            var panel = UiKit.Panel($"Prompt {index}", parent);
            UiKit.Corner(panel.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(Width, Height));

            var frame = UiKit.Frame("Frame", panel.transform, UiPalette.Gold);
            UiKit.Stretch(frame.rectTransform, -2f, -2f, -2f, -2f);

            var keyCap = UiKit.Fill("KeyCap", panel.transform, UiPalette.Gold);
            UiKit.Place(keyCap.rectTransform, 9f, 8f, 24f, 22f);
            var key = UiKit.Label("Key", keyCap.transform, "E", 14, UiPalette.Ink,
                TextAnchor.MiddleCenter, true);
            UiKit.Stretch(key.rectTransform, 0f, 0f, 0f, 0f);

            var label = UiKit.Label("Label", panel.transform, string.Empty, 17, UiPalette.Cream,
                TextAnchor.MiddleLeft);
            UiKit.Place(label.rectTransform, 40f, 8f, Width - 50f, 22f);

            panel.gameObject.SetActive(false);
            return new Prompt { Root = panel.rectTransform, Frame = frame, KeyCap = keyCap, Key = key, Label = label };
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            var zones = InteractionZone.Active;
            float scale = _canvas == null || _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;
            int used = 0;

            if (camera != null)
                for (int i = 0; i < zones.Count && used < Capacity; i++)
                {
                    var zone = zones[i];
                    if (zone == null || !zone.Available) continue;
                    Vector3 screen = camera.WorldToScreenPoint(zone.transform.position + Vector3.up * WorldLift);
                    if (screen.z < 0f) continue;

                    bool near = zone.PlayerInRange;
                    var prompt = _prompts[used++];
                    if (!prompt.Root.gameObject.activeSelf) prompt.Root.gameObject.SetActive(true);
                    prompt.Root.anchoredPosition = new Vector2(screen.x / scale - Width * 0.5f, screen.y / scale);
                    prompt.Label.text = zone.Label;
                    prompt.Label.color = UiPalette.Cream.Alpha(near ? 1f : 0.62f);
                    prompt.Frame.color = near ? UiPalette.Gold : UiPalette.PanelLight.Alpha(0.5f);
                    prompt.KeyCap.color = near ? UiPalette.Gold : UiPalette.Muted.Alpha(0.5f);
                    prompt.Root.localScale = Vector3.one * (near ? 1f : 0.88f);
                }

            for (int i = used; i < Capacity; i++)
                if (_prompts[i].Root.gameObject.activeSelf) _prompts[i].Root.gameObject.SetActive(false);
        }
    }
}
