using UnityEngine;

namespace DungeonDash
{
    public abstract class UiScreen : MonoBehaviour
    {
        const float FadeSpeed = 7f;

        CanvasGroup _group;
        RectTransform _pop;
        float _amount;

        protected DungeonGame Game { get; private set; }
        protected GameUi Ui { get; private set; }
        protected RectTransform Root => (RectTransform)transform;

        public bool Visible { get; private set; }

        public void Initialize(GameUi ui, DungeonGame game)
        {
            Ui = ui;
            Game = game;
            UiKit.Stretch(Root, 0f, 0f, 0f, 0f);
            _group = UiKit.Group(Root);
            Build();
            _amount = 0f;
            ApplyVisibility();
            gameObject.SetActive(false);
        }

        protected abstract void Build();

        protected void PopTarget(RectTransform target) => _pop = target;

        public virtual void Refresh() { }

        protected virtual void Tick() { }

        public void Show()
        {
            if (Visible) return;
            Visible = true;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide() => Visible = false;

        void Update()
        {
            float target = Visible ? 1f : 0f;
            if (!Mathf.Approximately(_amount, target))
            {
                _amount = Mathf.MoveTowards(_amount, target, FadeSpeed * Time.unscaledDeltaTime);
                ApplyVisibility();
                if (!Visible && _amount <= 0f)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }
            if (Visible) Tick();
        }

        void ApplyVisibility()
        {
            _group.alpha = _amount;
            bool live = _amount > 0.5f;
            _group.blocksRaycasts = live;
            _group.interactable = live;
            if (_pop != null)
                _pop.localScale = Vector3.one * Mathf.Lerp(0.955f, 1f, Mathf.SmoothStep(0f, 1f, _amount));
        }
    }
}
