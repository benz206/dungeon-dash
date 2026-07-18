using UnityEngine;

namespace DungeonDash
{
    // Trauma-based camera shake + brief hit-stop. Auto-boots like the other DungeonDash
    // systems (DungeonViewportSystem, RenderingBootstrap) so callers just use the static API.
    public sealed class GameFeel : MonoBehaviour
    {
        const float HitStopDip = 0.05f;
        const float TraumaDecayPerSecond = 3f;
        const float MaxShakeOffset = 0.3f;

        static GameFeel _instance;

        float _trauma;
        float _hitStopTimer;
        bool _hitStopActive;
        float _shakeSeed;

        public static Vector2 ShakeOffset { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<GameFeel>() == null)
                new GameObject("Game Feel").AddComponent<GameFeel>();
        }

        void Awake()
        {
            _instance = this;
            _shakeSeed = Random.value * 1000f;
            ShakeOffset = Vector2.zero;
        }

        public static void Shake(float amount)
        {
            if (_instance == null) return;
            _instance._trauma = Mathf.Clamp01(_instance._trauma + amount);
        }

        // Only takes effect while the game is actually running (Time.timeScale == 1),
        // so it never fights the inventory/market pause (which drives timeScale to 0).
        public static void HitStop(float duration = 0.06f)
        {
            if (_instance == null || Application.isBatchMode || !Mathf.Approximately(Time.timeScale, 1f)) return;
            Time.timeScale = HitStopDip;
            _instance._hitStopActive = true;
            _instance._hitStopTimer = duration;
        }

        void Update()
        {
            if (_hitStopActive)
            {
                if (!Mathf.Approximately(Time.timeScale, HitStopDip))
                {
                    // Something else (pause) took over timeScale — back off, don't stomp it.
                    _hitStopActive = false;
                }
                else
                {
                    _hitStopTimer -= Time.unscaledDeltaTime;
                    if (_hitStopTimer <= 0f)
                    {
                        Time.timeScale = 1f;
                        _hitStopActive = false;
                    }
                }
            }

            if (_trauma > 0f)
            {
                _trauma = Mathf.Max(0f, _trauma - TraumaDecayPerSecond * Time.unscaledDeltaTime);
                float shake = _trauma * _trauma;
                float t = Time.unscaledTime * 22f;
                float x = Mathf.PerlinNoise(_shakeSeed, t) * 2f - 1f;
                float y = Mathf.PerlinNoise(_shakeSeed + 37f, t) * 2f - 1f;
                ShakeOffset = new Vector2(x, y) * shake * MaxShakeOffset;
            }
            else if (ShakeOffset != Vector2.zero)
            {
                ShakeOffset = Vector2.zero;
            }
        }
    }
}
