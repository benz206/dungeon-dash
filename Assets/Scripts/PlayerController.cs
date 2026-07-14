using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonDash
{
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public sealed class PlayerController : MonoBehaviour
    {
        DungeonGame _game;
        GameCatalog.CharacterSkin _skin;
        Rigidbody2D _body;
        SpriteRenderer _renderer;
        SpriteRenderer _weaponRenderer;
        Vector2 _move;
        Vector2 _aim = Vector2.right;
        float _nextAttack;
        float _invulnerableUntil;
        float _hitUntil;
        float _animationTime;

        public int Health { get; private set; } = 10;
        public int MaxHealth => 10;

        public void Setup(DungeonGame game, GameCatalog.CharacterSkin skin)
        {
            _game = game;
            _skin = skin;
            _body = GetComponent<Rigidbody2D>();
            _renderer = GetComponent<SpriteRenderer>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _renderer.sortingOrder = 10;
            if (skin.idle.Length > 0) _renderer.sprite = skin.idle[0];

            var weapon = new GameObject("Equipped Artifact");
            weapon.transform.SetParent(transform);
            _weaponRenderer = weapon.AddComponent<SpriteRenderer>();
            _weaponRenderer.sortingOrder = 11;
            RefreshWeapon();
        }

        public void RefreshWeapon()
        {
            if (_weaponRenderer != null)
                _weaponRenderer.sprite = _game.WeaponSprite(_game.EquippedArtifact?.weaponId);
        }

        void Update()
        {
            if (_game == null || !_game.AcceptsGameplayInput)
            {
                _move = Vector2.zero;
                Animate();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                _move = new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                _move = Vector2.ClampMagnitude(_move, 1f);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector3 world = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                Vector2 delta = world - transform.position;
                if (delta.sqrMagnitude > 0.01f) _aim = delta.normalized;
                if (mouse.leftButton.isPressed) TryAttack();
            }
            if (keyboard != null && keyboard.spaceKey.isPressed) TryAttack();

            _weaponRenderer.transform.localPosition = _aim * 0.62f;
            _weaponRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_aim.y, _aim.x) * Mathf.Rad2Deg - 45f);
            _weaponRenderer.flipY = _aim.x < 0f;
            _renderer.flipX = _aim.x < 0f;
            Animate();
        }

        void FixedUpdate()
        {
            if (_body != null)
                _body.linearVelocity = _move * (_skin?.speed ?? 5f);
        }

        void TryAttack()
        {
            var artifact = _game.EquippedArtifact;
            if (artifact == null || Time.time < _nextAttack) return;
            _nextAttack = Time.time + 1f / artifact.attacksPerSecond;
            bool critical = Random.value < artifact.criticalChance;
            _game.Fire(transform.position + (Vector3)(_aim * 0.75f), _aim,
                critical ? artifact.damage * 2 : artifact.damage, _weaponRenderer.sprite, critical);
        }

        void Animate()
        {
            if (_renderer == null || _skin == null) return;
            _animationTime += Time.deltaTime;
            var frames = Time.time < _hitUntil && _skin.hit.Length > 0
                ? _skin.hit
                : _move.sqrMagnitude > 0.01f ? _skin.run : _skin.idle;
            if (frames != null && frames.Length > 0)
                _renderer.sprite = frames[Mathf.FloorToInt(_animationTime * 10f) % frames.Length];
        }

        public void TakeDamage(int amount)
        {
            if (Time.time < _invulnerableUntil || !_game.AcceptsGameplayInput) return;
            _invulnerableUntil = Time.time + 0.65f;
            _hitUntil = Time.time + 0.2f;
            Health = Mathf.Max(0, Health - amount);
            if (Health == 0) _game.GameOver();
        }

        public void Heal(int amount) => Health = Mathf.Min(MaxHealth, Health + amount);
    }
}
