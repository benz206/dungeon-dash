using UnityEngine;

namespace DungeonDash
{
    // Pooled sprite-chunk particles (hit sparks, pickup glints, death puffs). All chunks
    // share one pre-instantiated pool so spawning during combat never allocates or
    // Instantiate()s. Overbright colors (>1) are used on sparks/explosions so bloom picks
    // them up (see RenderingBootstrap's threshold-1.05 volume).
    public sealed class PixelBurst : MonoBehaviour
    {
        const int PoolSize = 96;
        const int SortingOrder = 60;

        sealed class Chunk
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public bool Gravity;
        }

        static PixelBurst _instance;
        static Sprite _pixel;

        Chunk[] _pool;
        int _cursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<PixelBurst>() == null)
                new GameObject("Pixel Burst").AddComponent<PixelBurst>();
        }

        void Awake()
        {
            _instance = this;
            if (_pixel == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 16f);
            }

            _pool = new Chunk[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Chunk");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _pixel;
                renderer.sortingOrder = SortingOrder;
                renderer.enabled = false;
                _pool[i] = new Chunk { Transform = go.transform, Renderer = renderer };
            }
        }

        void Update()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var chunk = _pool[i];
                if (chunk.Life <= 0f) continue;
                chunk.Life -= Time.deltaTime;
                if (chunk.Life <= 0f)
                {
                    chunk.Renderer.enabled = false;
                    continue;
                }
                if (chunk.Gravity) chunk.Velocity += Vector2.down * (9f * Time.deltaTime);
                chunk.Transform.position += (Vector3)(chunk.Velocity * Time.deltaTime);
                var color = chunk.Renderer.color;
                color.a = Mathf.Clamp01(chunk.Life / chunk.MaxLife);
                chunk.Renderer.color = color;
            }
        }

        Chunk NextChunk()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                int index = (_cursor + i) % PoolSize;
                if (_pool[index].Life > 0f) continue;
                _cursor = (index + 1) % PoolSize;
                return _pool[index];
            }
            return null;
        }

        static void Emit(Vector2 position, int count, float speed, float speedVariance, Color color, float life, bool gravity, float scale)
        {
            if (_instance == null || Application.isBatchMode) return;
            for (int i = 0; i < count; i++)
            {
                var chunk = _instance.NextChunk();
                if (chunk == null) return;
                float angle = Random.value * Mathf.PI * 2f;
                float chunkSpeed = speed + Random.value * speedVariance;
                chunk.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * chunkSpeed;
                chunk.Transform.position = position;
                chunk.Transform.localScale = Vector3.one * scale;
                chunk.Renderer.color = color;
                chunk.Renderer.enabled = true;
                chunk.Life = life;
                chunk.MaxLife = life;
                chunk.Gravity = gravity;
            }
        }

        public static void EnemyDeathPuff(Vector2 position, string skinId) =>
            Emit(position, 7, 1.2f, 1.6f, PaletteFor(skinId), 0.4f, true, 0.09f);

        public static void CoinSparkle(Vector2 position) =>
            Emit(position, 5, 0.6f, 0.8f, new Color(2.2f, 1.9f, 0.4f), 0.35f, false, 0.06f);

        public static void PotionGlint(Vector2 position) =>
            Emit(position, 5, 0.5f, 0.6f, new Color(0.6f, 1.9f, 1.1f), 0.35f, false, 0.06f);

        public static void BombBurst(Vector2 position) =>
            Emit(position, 16, 2.2f, 2.4f, new Color(2.4f, 1.2f, 0.3f), 0.5f, true, 0.11f);

        public static void DashDust(Vector2 position, Vector2 dashDirection) =>
            Emit(position - dashDirection * 0.2f, 4, 0.4f, 0.5f, new Color(0.55f, 0.55f, 0.58f, 0.8f), 0.3f, false, 0.08f);

        public static void HitSpark(Vector2 position, Vector2 direction, bool critical)
        {
            Color color = critical ? new Color(2.4f, 1.8f, 0.5f) : new Color(2.2f, 2.2f, 2.4f);
            Emit(position, critical ? 8 : 4, critical ? 2.2f : 1.4f, 1.2f, color, 0.18f, false, critical ? 0.08f : 0.06f);
        }

        // Textures are imported non-readable, so we can't literally sample sprite pixels
        // at runtime; a stable per-enemy-type hue keeps each species visually consistent.
        static Color PaletteFor(string skinId)
        {
            int hash = string.IsNullOrEmpty(skinId) ? 0 : skinId.GetHashCode();
            float hue = (hash & 0x7fffffff) % 360 / 360f;
            return Color.HSVToRGB(hue, 0.6f, 0.9f);
        }
    }
}
