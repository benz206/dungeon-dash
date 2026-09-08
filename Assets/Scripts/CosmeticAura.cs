using UnityEngine;

namespace DungeonDash
{
    public sealed class CosmeticAura : MonoBehaviour
    {
        readonly SpriteRenderer[] _motes = new SpriteRenderer[3];
        SaveData _save;

        public void Initialize(SaveData save)
        {
            _save = save;
            for (int i = 0; i < _motes.Length; i++)
            {
                var node = new GameObject("Guild Wisp");
                node.transform.SetParent(transform, false);
                var renderer = node.AddComponent<SpriteRenderer>();
                renderer.sprite = UiSprites.Glow;
                node.transform.localScale = Vector3.one * 0.65f;
                _motes[i] = renderer;
            }
        }

        void LateUpdate()
        {
            int style = GuildCosmetics.Equipped(_save);
            for (int i = 0; i < _motes.Length; i++)
            {
                var mote = _motes[i];
                mote.enabled = style != 0;
                if (style == 0) continue;
                var position = GuildCosmetics.Orbit(i, Time.time);
                mote.transform.localPosition = position;
                mote.color = GuildCosmetics.Color(style) * 1.8f;
                mote.sortingOrder = YSort.Order(transform.position.y, position.y < -0.25f ? 4 : 0);
            }
        }
    }
}
