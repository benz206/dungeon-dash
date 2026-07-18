using UnityEngine;

namespace DungeonDash
{
    public sealed class MeleeSwingActor : MonoBehaviour
    {
        float _startTime;
        float _startAngle;
        float _baseScale;
        SpriteRenderer _renderer;

        public void Setup(Vector2 direction, bool critical)
        {
            _startTime = Time.time;
            _startAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = critical ? 0.78f : 0.62f;
            transform.localScale = Vector3.one * _baseScale * 1.35f;
        }

        void Update()
        {
            float progress = (Time.time - _startTime) / 0.14f;
            if (progress >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float angle = _startAngle + Mathf.Lerp(-65f, 65f, progress);
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 45f);
            _renderer.color = new Color(1f, 1f, 1f, 1f - progress);
            transform.localScale = Vector3.one * _baseScale * Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(progress * 5f));
        }
    }
}
