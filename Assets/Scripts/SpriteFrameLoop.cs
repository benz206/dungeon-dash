using UnityEngine;

namespace DungeonDash
{
    public sealed class SpriteFrameLoop : MonoBehaviour
    {
        public Sprite[] Frames;
        public float Fps = 6f;

        SpriteRenderer _renderer;

        void Awake() => _renderer = GetComponent<SpriteRenderer>();

        void Update()
        {
            if (Frames == null || Frames.Length == 0) return;
            _renderer.sprite = Frames[Mathf.FloorToInt(Time.time * Fps) % Frames.Length];
        }
    }
}
