using UnityEngine;

namespace DungeonDash
{
    public enum PickupKind { Coin, Potion, Chest, Bomb, Artifact }

    public sealed class PickupActor : MonoBehaviour
    {
        const float MagnetRadius = 1.5f;
        const float MagnetSpeed = 6f;
        const float PickupRadius = 0.7f;
        const float Lifetime = 25f;

        DungeonGame _game;
        PickupKind _kind;
        Artifact _artifact;
        Sprite[] _frames;
        SpriteRenderer _renderer;
        float _spawnTime;

        public void Setup(DungeonGame game, PickupKind kind, Artifact artifact, Sprite[] frames = null)
        {
            _game = game;
            _kind = kind;
            _artifact = artifact;
            _frames = frames;
            _renderer = GetComponent<SpriteRenderer>();
            _spawnTime = Time.time;
        }

        void Update()
        {
            if (!_game.CombatActive) return;
            if (_frames != null && _frames.Length > 0)
                _renderer.sprite = _frames[Mathf.FloorToInt(Time.time * 8f) % _frames.Length];
            _renderer.sortingOrder = YSort.Order(transform.position.y, 0);
            if (_kind == PickupKind.Coin)
            {
                Vector2 toPlayer = _game.PlayerPosition - (Vector2)transform.position;
                if (toPlayer.sqrMagnitude < MagnetRadius * MagnetRadius)
                    transform.position = Vector2.MoveTowards(transform.position, _game.PlayerPosition,
                        MagnetSpeed * Time.deltaTime);
            }
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.07f);
            if (((Vector2)transform.position - _game.PlayerPosition).sqrMagnitude < PickupRadius * PickupRadius)
            {
                if (_kind == PickupKind.Coin) { PixelBurst.CoinSparkle(transform.position); GameAudio.Play("coin", 0.5f); }
                else if (_kind == PickupKind.Potion) { PixelBurst.PotionGlint(transform.position); GameAudio.Play("potion", 0.5f); }
                _game.Collect(_kind, _artifact);
                Destroy(gameObject);
            }
            else if (Time.time - _spawnTime > Lifetime) Destroy(gameObject);
        }
    }
}
