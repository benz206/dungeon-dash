using UnityEngine;

namespace DungeonDash
{
    public sealed class MeleeSwingActor : MonoBehaviour
    {
        float _startTime;
        float _startAngle;
        SpriteRenderer _renderer;

        public void Setup(Vector2 direction, bool critical)
        {
            _startTime = Time.time;
            _startAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _renderer = GetComponent<SpriteRenderer>();
            transform.localScale = Vector3.one * (critical ? 0.78f : 0.62f);
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
        }
    }
}
