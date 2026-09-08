using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        CanvasScaler _scaler;
        Rect _lastArea;
        Vector2 _lastScreen;

        public void Initialize(CanvasScaler scaler)
        {
            _scaler = scaler;
            Refresh();
        }

        void Update() => Refresh();

        void Refresh()
        {
            var size = new Vector2(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (size.x <= 0f || size.y <= 0f || area.width <= 0f || area.height <= 0f) return;
            if (area == _lastArea && size == _lastScreen) return;
            _lastArea = area;
            _lastScreen = size;
            var root = (RectTransform)transform;
            root.anchorMin = area.min / size;
            root.anchorMax = area.max / size;
            root.offsetMin = root.offsetMax = Vector2.zero;
            // Preserve enough design space for fixed-size dialogs, including on 4:3 tablets.
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _scaler.referenceResolution = UiKit.Reference * size / area.size;
        }
    }
}
