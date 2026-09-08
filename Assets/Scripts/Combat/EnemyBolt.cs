using UnityEngine;

namespace DungeonDash
{
    public sealed class EnemyBolt : MonoBehaviour
    {
        const float Speed = 5f;
        DungeonGame _game;
        Vector2 _direction;
        float _expires;

        public static EnemyBolt Fire(DungeonGame game, Vector2 origin, Vector2 target, Transform parent)
        {
            var node = WorldBuilder.CreateSprite("Enemy bolt", UiSprites.Glow, origin, 101, parent);
            node.GetComponent<SpriteRenderer>().color = new Color(1f, 0.35f, 0.1f);
            node.transform.localScale = Vector3.one * 1.8f;
            var core = WorldBuilder.CreateSprite("Core", UiSprites.Solid, origin, 102, node.transform);
            core.transform.localScale = Vector3.one * 3f;
            var bolt = node.AddComponent<EnemyBolt>();
            bolt._game = game;
            bolt._direction = (target - origin).normalized;
            bolt._expires = Time.time + 2f;
            return bolt;
        }

        void Update()
        {
            if (!_game.CombatActive) return;
            Vector2 from = transform.position;
            Vector2 next = from + _direction * (Speed * Time.deltaTime);
            if (Time.time >= _expires || !_game.ProjectilePathClear(from, next))
            {
                Destroy(gameObject);
                return;
            }
            Vector2 segment = next - from;
            float t = segment.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector2.Dot(_game.PlayerPosition - from, segment) / segment.sqrMagnitude) : 0f;
            if (Vector2.Distance(_game.PlayerPosition, from + segment * t) < 0.45f)
            {
                _game.HurtPlayer(1, from);
                Destroy(gameObject);
                return;
            }
            transform.position = next;
        }
    }
}
