using UnityEngine;

namespace DungeonDash
{
    public sealed class ProjectileActor : MonoBehaviour
    {
        const float Speed = 11f;
        const float Lifetime = 1.6f;

        DungeonGame _game;
        Vector2 _direction;
        int _damage;
        bool _critical;
        float _expires;

        public void Setup(DungeonGame game, Vector2 direction, int damage, bool critical = false)
        {
            _game = game;
            _direction = direction;
            _damage = damage;
            _critical = critical;
            _expires = Time.time + Lifetime;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 45f);
        }

        void Update()
        {
            if (!_game.CombatActive) return;
            transform.position += (Vector3)(_direction * (Speed * Time.deltaTime));
            var target = _game.ProjectileTarget(transform.position);
            if (target != null)
            {
                target.TakeDamage(_damage, transform.position - (Vector3)(_direction * 0.35f), _critical);
                Destroy(gameObject);
            }
            else if (Time.time >= _expires) Destroy(gameObject);
        }
    }
}
