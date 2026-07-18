using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class PlayerCenteredCamera : MonoBehaviour
    {
        public Transform Target { get; private set; }

        public void SetTarget(Transform target)
        {
            Target = target;
            CenterNow();
        }

        public void CenterNow()
        {
            if (Target == null) return;
            transform.position = new Vector3(Target.position.x, Target.position.y, transform.position.z);
        }

        void LateUpdate()
        {
            CenterNow();
            if (!Application.isBatchMode) transform.position += (Vector3)GameFeel.ShakeOffset;
        }
    }

    public sealed class DungeonViewportSystem : MonoBehaviour
    {
        const int MinimapTextureSize = 256;
        const float MinimapWorldRadius = 10f;

        PlayerController _player;
        Camera _mainCamera;
        PlayerCenteredCamera _cameraFollow;
        Camera _minimapCamera;
        GameObject _minimap;
        RenderTexture _minimapTexture;
        Texture2D _circleTexture;
        Texture2D _ringTexture;

        public Camera MinimapCamera => _minimapCamera;
        public GameObject Minimap => _minimap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DungeonViewportSystem>() == null)
                new GameObject("Dungeon Viewport").AddComponent<DungeonViewportSystem>();
        }

        void Update()
        {
            EnsureMainCamera();

            var player = FindFirstObjectByType<PlayerController>();
            if (player != _player)
            {
                _player = player;
                if (_cameraFollow != null) _cameraFollow.SetTarget(_player == null ? null : _player.transform);
            }

            EnsureMinimap();
            bool hasPlayer = _player != null;
            if (_minimap.activeSelf != hasPlayer) _minimap.SetActive(hasPlayer);
            _minimapCamera.enabled = hasPlayer && !Application.isBatchMode;
            if (hasPlayer)
                _minimapCamera.transform.position = new Vector3(
                    _player.transform.position.x, _player.transform.position.y, -20f);
        }

        void EnsureMainCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null || mainCamera == _mainCamera) return;
            _mainCamera = mainCamera;
            _cameraFollow = _mainCamera.GetComponent<PlayerCenteredCamera>();
            if (_cameraFollow == null) _cameraFollow = _mainCamera.gameObject.AddComponent<PlayerCenteredCamera>();
            _cameraFollow.SetTarget(_player == null ? null : _player.transform);
        }

        void EnsureMinimap()
        {
            if (_minimap != null) return;

            var canvasObject = new GameObject("Dungeon HUD", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            _minimap = new GameObject("Minimap", typeof(RectTransform), typeof(Image), typeof(Mask));
            _minimap.transform.SetParent(canvasObject.transform, false);
            var minimapRect = _minimap.GetComponent<RectTransform>();
            minimapRect.anchorMin = Vector2.zero;
            minimapRect.anchorMax = Vector2.zero;
            minimapRect.pivot = Vector2.zero;
            minimapRect.anchoredPosition = new Vector2(24f, 24f);
            minimapRect.sizeDelta = new Vector2(136f, 136f);

            _circleTexture = CreateCircleTexture(128, false);
            var maskImage = _minimap.GetComponent<Image>();
            maskImage.sprite = Sprite.Create(_circleTexture, new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            maskImage.raycastTarget = false;
            _minimap.GetComponent<Mask>().showMaskGraphic = false;

            _minimapTexture = new RenderTexture(MinimapTextureSize, MinimapTextureSize, 16)
            {
                name = "Dungeon Minimap Texture",
                filterMode = FilterMode.Point
            };

            var mapImageObject = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            mapImageObject.transform.SetParent(_minimap.transform, false);
            Stretch(mapImageObject.GetComponent<RectTransform>());
            var mapImage = mapImageObject.GetComponent<RawImage>();
            mapImage.texture = _minimapTexture;
            mapImage.raycastTarget = false;

            _ringTexture = CreateCircleTexture(128, true);
            var frameObject = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(_minimap.transform, false);
            Stretch(frameObject.GetComponent<RectTransform>());
            var frame = frameObject.GetComponent<Image>();
            frame.sprite = Sprite.Create(_ringTexture, new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            frame.color = new Color(0.72f, 0.53f, 0.31f, 0.98f);
            frame.raycastTarget = false;

            var cameraObject = new GameObject("Minimap Camera");
            cameraObject.transform.SetParent(transform, false);
            _minimapCamera = cameraObject.AddComponent<Camera>();
            _minimapCamera.orthographic = true;
            _minimapCamera.orthographicSize = MinimapWorldRadius;
            _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            _minimapCamera.backgroundColor = new Color(0.015f, 0.025f, 0.045f);
            _minimapCamera.targetTexture = _minimapTexture;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Texture2D CreateCircleTexture(int size, bool ring)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "Minimap Ring" : "Minimap Circle",
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            float innerRadius = radius - 4f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                bool visible = distance <= radius && (!ring || distance >= innerRadius);
                pixels[y * size + x] = visible ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        void OnDestroy()
        {
            if (_minimapTexture != null) _minimapTexture.Release();
            if (_circleTexture != null) Destroy(_circleTexture);
            if (_ringTexture != null) Destroy(_ringTexture);
        }
    }
}
