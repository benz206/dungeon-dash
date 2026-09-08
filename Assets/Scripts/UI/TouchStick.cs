using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonDash
{
    public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float Radius = 54f;
        int? _pointer;
        RectTransform _thumb;

        public Vector2 Value { get; private set; }

        public void Initialize(string label)
        {
            var root = (RectTransform)transform;
            var pad = UiKit.Inset("Pad", root);
            UiKit.Stretch(pad.rectTransform, 0f, 0f, 0f, 0f);
            pad.color = UiPalette.Ink.Alpha(0.65f);
            pad.raycastTarget = true;
            var frame = UiKit.Frame("Frame", root, UiPalette.Cream.Alpha(0.35f));
            UiKit.Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);
            _thumb = UiKit.Panel("Thumb", root).rectTransform;
            UiKit.Center(_thumb, 52f, 52f);
            var text = UiKit.Label("Label", root, label, 18, UiPalette.Cream, TextAnchor.MiddleCenter);
            UiKit.Corner(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -28f), new Vector2(180f, 26f));
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (_pointer.HasValue) return;
            _pointer = data.pointerId;
            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (_pointer != data.pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                data.position, data.pressEventCamera, out var position)) return;
            var offset = Vector2.ClampMagnitude(position / Radius, 1f);
            Value = offset.magnitude < 0.12f ? Vector2.zero : offset;
            if (_thumb != null) _thumb.anchoredPosition = offset * Radius;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (_pointer == data.pointerId) ResetInput();
        }

        public void ResetInput()
        {
            _pointer = null;
            Value = Vector2.zero;
            if (_thumb != null) _thumb.anchoredPosition = Vector2.zero;
        }

        void OnDisable() => ResetInput();
    }
}
