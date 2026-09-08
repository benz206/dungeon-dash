using UnityEngine;

namespace DungeonDash
{
    public sealed class AttackTelegraph
    {
        readonly Transform _root;
        readonly SpriteRenderer _shape;
        readonly bool _ranged;
        static Sprite _ring;

        public AttackTelegraph(Transform parent, bool ranged)
        {
            _ranged = ranged;
            _root = new GameObject("Attack warning").transform;
            _root.SetParent(parent, false);
            _shape = _root.gameObject.AddComponent<SpriteRenderer>();
            _shape.sprite = ranged ? UiSprites.Solid : Ring();
            _shape.sortingOrder = 100;
            Hide();
        }

        public void Show(Vector2 origin, Vector2 target, float progress)
        {
            _root.gameObject.SetActive(true);
            _shape.color = Color.Lerp(new Color(1f, 0.6f, 0.15f, 0.45f),
                new Color(1f, 0.25f, 0.1f, 0.95f), progress);
            if (_ranged)
            {
                Vector2 delta = target - origin;
                _root.position = (origin + target) * 0.5f;
                _root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                _root.localScale = new Vector3(delta.magnitude / 0.04f, 2f, 1f);
            }
            else
            {
                _root.position = origin;
                _root.localScale = Vector3.one * 1.6f;
            }
        }

        public void Hide() => _root.gameObject.SetActive(false);
        public void Destroy() { if (_root != null) Object.Destroy(_root.gameObject); }

        static Sprite Ring()
        {
            if (_ring != null) return _ring;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), Vector2.one * 15.5f);
                pixels[y * size + x] = radius is >= 13f and <= 15.5f ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            _ring = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
            _ring.hideFlags = HideFlags.HideAndDontSave;
            return _ring;
        }
    }
}
