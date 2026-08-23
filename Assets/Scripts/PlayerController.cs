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
        Vector2 _dashDirection;
        Vector2 _knockbackDirection;
        float _nextAttack;
        float _nextDash;
        float _dashUntil;
        float _knockbackUntil;
        float _invulnerableUntil;
        float _hitUntil;
        float _flashUntil;
        float _animationTime;

        const float DashSpeed = 13f;
        const float DashDuration = 0.16f;
        const float DashCooldown = 0.75f;

        public int Health { get; private set; }
        public int MaxHealth { get; private set; } = 10;
        public float DamageMod { get; private set; } = 1f;

        public void Setup(DungeonGame game, GameCatalog.CharacterSkin skin)
        {
            _game = game;
            _skin = skin;
            // Un-regenerated catalogs deserialize maxHealth/damageMod as 0 — fall back to sane defaults.
            MaxHealth = skin.maxHealth > 0f ? Mathf.RoundToInt(skin.maxHealth) : 10;
            Health = MaxHealth;
            DamageMod = skin.damageMod > 0f ? skin.damageMod : 1f;
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
            weapon.transform.localScale = Vector3.one * 0.55f;
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
                if (mouse.rightButton.wasPressedThisFrame) TryDash();
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
            if (_body == null) return;
            if (Time.time < _dashUntil)
                _body.linearVelocity = _dashDirection * DashSpeed;
            else if (Time.time < _knockbackUntil)
                _body.linearVelocity = _knockbackDirection * 7.5f;
            else
                _body.linearVelocity = _move * (_skin?.speed ?? 5f);
        }

        void TryAttack()
        {
            if (!_game.CombatActive) return; // no attacks outside the dungeon (e.g. the hub)
            var artifact = _game.EquippedArtifact;
            if (artifact == null || Time.time < _nextAttack) return;
            _nextAttack = Time.time + 1f / artifact.attacksPerSecond;
            bool critical = Random.value < artifact.criticalChance;
            int damage = Mathf.RoundToInt(artifact.EffectiveDamage * DamageMod) * (critical ? 2 : 1);
            _game.UseWeapon(transform.position + (Vector3)(_aim * 0.75f), _aim,
                damage, artifact.weaponId, _weaponRenderer.sprite, critical);
        }

        void TryDash()
        {
            if (Time.time < _nextDash) return;
            _dashDirection = _move.sqrMagnitude > 0.01f ? _move.normalized : _aim;
            _dashUntil = Time.time + DashDuration;
            _nextDash = Time.time + DashCooldown;
            PixelBurst.DashDust(transform.position, _dashDirection);
            GameAudio.Play("dash_whoosh", 0.6f);
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
            _renderer.color = Time.time < _flashUntil
                ? new Color(3.5f, 3.5f, 3.5f, 1f)
                : Color.white;
            _renderer.sortingOrder = YSort.Order(transform.position.y, 2);
            if (_weaponRenderer != null) _weaponRenderer.sortingOrder = _renderer.sortingOrder + 1;
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, transform.position - (Vector3)_aim);
        }

        public void TakeDamage(int amount, Vector2 sourcePosition)
        {
            if (Time.time < _invulnerableUntil || !_game.AcceptsGameplayInput) return;
            _invulnerableUntil = Time.time + 0.65f;
            _hitUntil = Time.time + 0.2f;
            _flashUntil = Time.time + 0.12f;
            Vector2 away = (Vector2)transform.position - sourcePosition;
            _knockbackDirection = away.sqrMagnitude > 0.01f ? away.normalized : -_aim;
            _knockbackUntil = Time.time + 0.14f;
            Health = Mathf.Max(0, Health - amount);
            DamageNumberLayer.Spawn(transform.position, amount, DamageNumberKind.PlayerHurt);
            GameAudio.Play("player_hurt", 0.8f);
            GameFeel.Shake(0.4f);
            GameFeel.HitStop();
            if (Health == 0) _game.GameOver();
        }

        public void Heal(int amount) => Health = Mathf.Min(MaxHealth, Health + amount);
    }
}
