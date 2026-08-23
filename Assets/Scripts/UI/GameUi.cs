using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class GameUi : MonoBehaviour
    {
        const int TransitionBands = 12;
        const float BandStagger = 0.018f;
        const float ToastDuration = 3.2f;

        readonly Dictionary<GameMode, UiScreen> _screens = new();
        readonly List<UiScreen> _all = new();
        readonly Image[] _bands = new Image[TransitionBands];

        DungeonGame _game;
        Canvas _canvas;
        RectTransform _screenLayer;
        RectTransform _overlayLayer;
        HudView _hud;
        TitleBackdrop _backdrop;
        RectTransform _transitionRoot;
        RectTransform _transitionPlate;
        Text _transitionLabel;
        RectTransform _toastRoot;
        Text _toastLabel;
        CanvasGroup _toastGroup;
        float _toastUntil;

        public HudView Hud => _hud;

        public void Initialize(DungeonGame game)
        {
            _game = game;
            BuildCanvas();
            BuildLayers();
            BuildScreens();
            BuildTransition();
            BuildToast();
        }

        void BuildCanvas()
        {
            var canvasObject = new GameObject("Dungeon UI", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiKit.Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject("Event System", typeof(EventSystem));
            eventSystemObject.transform.SetParent(transform, false);
            var module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
        }

        void BuildLayers()
        {
            var backdropNode = UiKit.Node("Title Backdrop", _canvas.transform);
            _backdrop = backdropNode.gameObject.AddComponent<TitleBackdrop>();
            _backdrop.Initialize(_game.Catalog);

            var hudLayer = UiKit.Node("HUD Layer", _canvas.transform);
            UiKit.Stretch(hudLayer, 0f, 0f, 0f, 0f);
            _hud = hudLayer.gameObject.AddComponent<HudView>();
            _hud.Initialize(_game);

            var promptNode = UiKit.Node("Prompts", _canvas.transform);
            promptNode.gameObject.AddComponent<PromptLayer>().Initialize(_canvas);

            var damageNode = UiKit.Node("Damage Numbers", _canvas.transform);
            damageNode.gameObject.AddComponent<DamageNumberLayer>().Initialize(_canvas);

            _screenLayer = UiKit.Node("Screens", _canvas.transform);
            UiKit.Stretch(_screenLayer, 0f, 0f, 0f, 0f);

            _overlayLayer = UiKit.Node("Overlay", _canvas.transform);
            UiKit.Stretch(_overlayLayer, 0f, 0f, 0f, 0f);
        }

        void BuildScreens()
        {
            Register(GameMode.StartScreen, Create<TitleScreen>("Title"));
            Register(GameMode.CharacterSelect, Create<SlotScreen>("Slots"));
            Register(GameMode.Inventory, Create<VaultScreen>("Vault"));
            Register(GameMode.Market, Create<MarketScreen>("Market"));
            Register(GameMode.Paused, Create<PauseScreen>("Pause"));
            Register(GameMode.GameOver, Create<GameOverScreen>("Game Over"));
            HeroPicker = Create<HeroPickerScreen>("Hero Picker");
        }

        public HeroPickerScreen HeroPicker { get; private set; }

        T Create<T>(string name) where T : UiScreen
        {
            var node = UiKit.Node(name, _screenLayer);
            var screen = node.gameObject.AddComponent<T>();
            screen.Initialize(this, _game);
            _all.Add(screen);
            return screen;
        }

        void Register(GameMode mode, UiScreen screen) => _screens[mode] = screen;

        void BuildTransition()
        {
            _transitionRoot = UiKit.Node("Transition", _overlayLayer);
            UiKit.Stretch(_transitionRoot, 0f, 0f, 0f, 0f);
            float bandHeight = UiKit.Reference.y / TransitionBands;
            for (int i = 0; i < TransitionBands; i++)
            {
                var band = UiKit.Fill($"Band {i}", _transitionRoot, new Color(0.012f, 0.015f, 0.024f));
                var rect = band.rectTransform;
                rect.anchorMin = new Vector2(i % 2 == 0 ? 0f : 1f, 1f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(i % 2 == 0 ? 0f : 1f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * bandHeight);
                rect.sizeDelta = new Vector2(0f, bandHeight + 1f);
                _bands[i] = band;
            }

            _transitionPlate = UiKit.Node("Plate", _transitionRoot);
            UiKit.Center(_transitionPlate, 460f, 92f);
            var plate = UiKit.Panel("Frame", _transitionPlate);
            UiKit.Stretch(plate.rectTransform, 0f, 0f, 0f, 0f);
            var accent = UiKit.Header("Accent", plate.transform, UiPalette.Crimson);
            UiKit.Stretch(accent.rectTransform, 8f, 8f, 8f, 8f);
            _transitionLabel = UiKit.Label("Label", accent.transform, string.Empty, 22, UiPalette.Cream,
                TextAnchor.MiddleCenter, true);
            UiKit.Stretch(_transitionLabel.rectTransform, 10f, 0f, 10f, 0f);
            _transitionRoot.gameObject.SetActive(false);
        }

        void BuildToast()
        {
            _toastRoot = UiKit.Node("Toast", _overlayLayer);
            UiKit.Corner(_toastRoot, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(620f, 46f));
            _toastGroup = UiKit.Group(_toastRoot);
            var panel = UiKit.Panel("Frame", _toastRoot);
            UiKit.Stretch(panel.rectTransform, 0f, 0f, 0f, 0f);
            var stripe = UiKit.Fill("Stripe", panel.transform, UiPalette.Gold);
            UiKit.Place(stripe.rectTransform, 8f, 8f, 5f, 30f);
            _toastLabel = UiKit.Label("Text", panel.transform, string.Empty, 19, UiPalette.Cream,
                TextAnchor.MiddleCenter);
            UiKit.Stretch(_toastLabel.rectTransform, 22f, 0f, 16f, 0f);
            _toastGroup.alpha = 0f;
        }

        public void Toast(string message)
        {
            _toastLabel.text = message;
            _toastUntil = Time.unscaledTime + ToastDuration;
        }

        public void SetMode(GameMode mode, bool heroPicker)
        {
            foreach (var screen in _all) screen.Hide();
            if (heroPicker) HeroPicker.Show();
            else if (_screens.TryGetValue(mode, out var target)) target.Show();

            bool titleScene = mode is GameMode.StartScreen or GameMode.CharacterSelect;
            if (_backdrop.gameObject.activeSelf != titleScene) _backdrop.gameObject.SetActive(titleScene);

            bool worldVisible = mode is GameMode.HomeHub or GameMode.InDungeon or GameMode.Inventory
                or GameMode.Market or GameMode.Paused or GameMode.GameOver;
            if (_hud.gameObject.activeSelf != worldVisible) _hud.gameObject.SetActive(worldVisible);
            if (!worldVisible) return;
            _hud.SetDimmed(mode is GameMode.Inventory or GameMode.Market or GameMode.Paused or GameMode.GameOver);
            _hud.Refresh();
        }

        public void RefreshActive()
        {
            foreach (var screen in _all) if (screen.Visible) screen.Refresh();
            if (_hud.gameObject.activeSelf) _hud.Refresh();
        }

        public void SetTransition(float amount, string label)
        {
            bool active = amount > 0f;
            if (_transitionRoot.gameObject.activeSelf != active) _transitionRoot.gameObject.SetActive(active);
            if (!active) return;

            float span = 1f - (TransitionBands - 1) * BandStagger;
            for (int i = 0; i < TransitionBands; i++)
            {
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((amount - i * BandStagger) / span));
                var rect = _bands[i].rectTransform;
                rect.sizeDelta = new Vector2(UiKit.Reference.x * progress, rect.sizeDelta.y);
            }

            _transitionLabel.text = label;
            float plate = Mathf.InverseLerp(0.72f, 0.9f, amount);
            _transitionPlate.gameObject.SetActive(plate > 0f);
            _transitionPlate.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, plate);
        }

        void Update()
        {
            float remaining = _toastUntil - Time.unscaledTime;
            float target = remaining > 0f ? Mathf.Clamp01(remaining / 0.4f) : 0f;
            if (!Mathf.Approximately(_toastGroup.alpha, target))
                _toastGroup.alpha = Mathf.MoveTowards(_toastGroup.alpha, target, 6f * Time.unscaledDeltaTime);
            _toastRoot.anchoredPosition = new Vector2(0f, -124f + (1f - _toastGroup.alpha) * 22f);
            if (_hud.gameObject.activeSelf) _hud.Tick();
        }
    }
}
