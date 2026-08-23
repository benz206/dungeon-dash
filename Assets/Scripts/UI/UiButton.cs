using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class UiButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        const float Responsiveness = 18f;
        const float Settled = 0.002f;

        public event Action Clicked;

        Image _background;
        Image _highlight;
        RectTransform _content;
        Graphic[] _contentGraphics;
        Color _rest;
        Color _hover;
        bool _pointerInside;
        bool _pointerDown;
        bool _interactable = true;
        float _highlightAmount;
        float _pressAmount;

        public RectTransform Rect => (RectTransform)transform;

        public bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                _pointerDown = false;
                Apply();
            }
        }

        public void Bind(Image background, Image highlight, RectTransform content, Color rest, Color hover)
        {
            _background = background;
            _highlight = highlight;
            _content = content;
            _contentGraphics = content == null ? Array.Empty<Graphic>() : content.GetComponentsInChildren<Graphic>(true);
            _rest = rest;
            _hover = hover;
            Apply();
        }

        void OnDisable()
        {
            _pointerInside = false;
            _pointerDown = false;
            _highlightAmount = 0f;
            _pressAmount = 0f;
            Apply();
        }

        void Update()
        {
            float highlightTarget = _interactable && _pointerInside ? 1f : 0f;
            float pressTarget = _interactable && _pointerDown ? 1f : 0f;
            if (Mathf.Abs(_highlightAmount - highlightTarget) < Settled &&
                Mathf.Abs(_pressAmount - pressTarget) < Settled) return;

            float step = 1f - Mathf.Exp(-Responsiveness * Time.unscaledDeltaTime);
            _highlightAmount = Approach(_highlightAmount, highlightTarget, step);
            _pressAmount = Approach(_pressAmount, pressTarget, step);
            Apply();
        }

        static float Approach(float current, float target, float step)
        {
            float next = Mathf.Lerp(current, target, step);
            return Mathf.Abs(next - target) < Settled ? target : next;
        }

        void Apply()
        {
            if (_background != null)
            {
                var color = Color.Lerp(_rest, _hover, _highlightAmount).Scale(1f - _pressAmount * 0.2f);
                _background.color = color.Alpha(_interactable ? _rest.a : _rest.a * 0.45f);
            }
            if (_highlight != null)
                _highlight.color = UiPalette.Gold.Alpha(_interactable ? _highlightAmount * 0.9f : 0f);
            if (_content != null)
                _content.anchoredPosition = new Vector2(0f, -2f * _pressAmount);

            float contentAlpha = _interactable ? 1f : 0.4f;
            foreach (var graphic in _contentGraphics)
                if (graphic != null) graphic.color = graphic.color.Alpha(contentAlpha);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (_interactable) GameAudio.Play("ui_hover_soft", 0.35f);
        }

        public void OnPointerExit(PointerEventData eventData) => _pointerInside = false;

        public void OnPointerDown(PointerEventData eventData) => _pointerDown = true;

        public void OnPointerUp(PointerEventData eventData) => _pointerDown = false;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactable) return;
            GameAudio.Play("ui_click", 0.5f);
            Clicked?.Invoke();
        }
    }
}
