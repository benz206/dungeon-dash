using UnityEngine;

namespace DungeonDash
{
    public sealed class ExitDoor
    {
        readonly SpriteRenderer _leaf;
        readonly BoxCollider2D _blocker;
        readonly InteractionZone _zone;
        readonly Sprite _openSprite;

        public ExitDoor(SpriteRenderer leaf, BoxCollider2D blocker, InteractionZone zone, Sprite openSprite)
        {
            _leaf = leaf;
            _blocker = blocker;
            _zone = zone;
            _openSprite = openSprite;
            if (_zone != null) _zone.enabled = false;
        }

        public bool Unlocked { get; private set; }
        public Vector2 Position => _zone == null ? Vector2.zero : (Vector2)_zone.transform.position;

        public void Unlock()
        {
            if (Unlocked) return;
            Unlocked = true;
            if (_leaf != null && _openSprite != null) _leaf.sprite = _openSprite;
            if (_blocker != null) _blocker.enabled = false;
            if (_zone != null) _zone.enabled = true;
        }
    }
}
