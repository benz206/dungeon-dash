using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class HeroPickerScreen : UiScreen
    {
        const float CardWidth = 226f;
        const float CardHeight = 104f;
        const float CardGap = 10f;
        const int Columns = 3;

        readonly List<Image> _cardFrames = new();
        readonly List<GameCatalog.CharacterSkin> _order = new();
        RectTransform _rosterContent;
        RectTransform _detail;
        GameCatalog.CharacterSkin _selected;
        Image _detailPortrait;
        Text _detailName;
        Text _detailRole;
        Text _healthValue;
        Text _movementValue;
        Text _damageValue;
        UiBar _health;
        UiBar _movement;
        UiBar _damage;

        protected override void Build()
        {
            var dialog = UiKit.Dialog(Root, "NEW DELVER", "REGISTRY  /  CHOOSE YOUR OPERATIVE",
                UiPalette.Crimson, 1120f, 640f);
            PopTarget(dialog.Holder);

            var cancel = UiKit.PushButton("Cancel", dialog.HeaderActions, "CANCEL", ButtonTone.Ghost,
                Game.CancelHeroPicker, 14);
            UiKit.Corner(cancel.Rect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(120f, 38f));

            var roster = UiKit.ScrollList("Roster", dialog.Body, out _rosterContent);
            UiKit.Place(roster.GetComponent<Image>().rectTransform, 0f, 0f, 736f, 538f);

            _detail = UiKit.Node("Detail", dialog.Body);
            UiKit.Place(_detail, 752f, 0f, 332f, 538f);

            BuildRoster();
            BuildDetail();
        }

        void BuildRoster()
        {
            var characters = Game.Catalog.Catalog.characters;
            for (int i = 0; i < characters.Length; i++)
            {
                var skin = characters[i];
                _order.Add(skin);

                var card = UiKit.Fill($"Hero {skin.id}", _rosterContent, UiPalette.RowFill);
                UiKit.Place(card.rectTransform, (i % Columns) * (CardWidth + CardGap),
                    (i / Columns) * (CardHeight + CardGap), CardWidth, CardHeight);
                card.raycastTarget = true;

                var edge = UiKit.Fill("Edge", card.transform, UiPalette.PanelLight);
                UiKit.Place(edge.rectTransform, 0f, 0f, 4f, CardHeight);

                var frame = UiKit.Frame("Frame", card.transform, Color.clear);
                UiKit.Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);
                _cardFrames.Add(frame);

                if (skin.idle.Length > 0)
                {
                    var portrait = UiKit.Icon("Portrait", card.transform, skin.idle[0]);
                    UiKit.Place(portrait.rectTransform, 12f, 8f, 76f, 88f);
                }

                var name = UiKit.Label("Name", card.transform, HeroNames.Name(skin.id).ToUpperInvariant(),
                    17, UiPalette.Cream);
                UiKit.Place(name.rectTransform, 96f, 14f, CardWidth - 106f, 22f);
                var role = UiKit.Label("Role", card.transform,
                    $"{HeroNames.Role(skin.id).ToUpperInvariant()}  /  {HeroNames.Variant(skin.id)}",
                    15, UiPalette.Gold);
                UiKit.Place(role.rectTransform, 96f, 38f, CardWidth - 106f, 20f);
                var speed = UiKit.Label("Speed", card.transform, $"MOVE  {skin.speed:0.0}", 15, UiPalette.Muted);
                UiKit.Place(speed.rectTransform, 96f, 64f, CardWidth - 106f, 20f);

                var button = card.gameObject.AddComponent<UiButton>();
                button.Bind(card, frame, null, UiPalette.RowFill, UiPalette.RowHover);
                var captured = skin;
                button.Clicked += () => Select(captured);
            }

            int rows = Mathf.CeilToInt(_order.Count / (float)Columns);
            UiKit.SetContentHeight(_rosterContent, rows * (CardHeight + CardGap));
        }

        void BuildDetail()
        {
            var panel = UiKit.Inset("Panel", _detail);
            UiKit.Stretch(panel.rectTransform, 0f, 0f, 0f, 0f);

            _detailRole = UiKit.Label("Role", panel.transform, string.Empty, 16, UiPalette.Gold);
            UiKit.Place(_detailRole.rectTransform, 22f, 18f, 288f, 22f);

            var preview = UiKit.Fill("Preview", panel.transform, new Color(0.018f, 0.027f, 0.04f));
            UiKit.Place(preview.rectTransform, 76f, 46f, 180f, 156f);
            var frame = UiKit.Frame("Frame", preview.transform, UiPalette.PanelLight.Alpha(0.6f));
            UiKit.Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);
            _detailPortrait = UiKit.Icon("Portrait", preview.transform, null);
            UiKit.Stretch(_detailPortrait.rectTransform, 16f, 12f, 16f, 12f);

            _detailName = UiKit.Label("Name", panel.transform, string.Empty, 21, UiPalette.Cream,
                TextAnchor.UpperCenter, true);
            UiKit.Place(_detailName.rectTransform, 16f, 214f, 300f, 28f);

            (_health, _healthValue) = BuildStat(panel.transform, "HEALTH", 262f, new Color(0.84f, 0.36f, 0.41f));
            (_movement, _movementValue) = BuildStat(panel.transform, "MOVEMENT", 320f, UiPalette.Steel);
            (_damage, _damageValue) = BuildStat(panel.transform, "DAMAGE", 378f, UiPalette.Gold);

            var confirm = UiKit.PushButton("Confirm", panel.transform, "CONFIRM DELVER", ButtonTone.Primary,
                () => Game.ConfirmNewSlot(_selected), 16);
            UiKit.Place(confirm.Rect, 24f, 464f, 284f, 52f);
        }

        static (UiBar bar, Text value) BuildStat(Transform parent, string label, float y, Color color)
        {
            var name = UiKit.Label($"{label} Label", parent, label, 15, UiPalette.Muted);
            UiKit.Place(name.rectTransform, 24f, y, 180f, 20f);
            var value = UiKit.Label($"{label} Value", parent, string.Empty, 16, UiPalette.Cream,
                TextAnchor.MiddleRight);
            UiKit.Place(value.rectTransform, 148f, y, 160f, 20f);
            var bar = UiKit.Bar($"{label} Bar", parent, color);
            UiKit.Place(bar.Root, 24f, y + 24f, 284f, 10f);
            return (bar, value);
        }

        public override void Refresh()
        {
            if (_selected == null || System.Array.IndexOf(Game.Catalog.Catalog.characters, _selected) < 0)
                _selected = Game.Catalog.Catalog.characters[0];
            Select(_selected);
        }

        void Select(GameCatalog.CharacterSkin skin)
        {
            _selected = skin;
            for (int i = 0; i < _cardFrames.Count; i++)
                _cardFrames[i].color = _order[i] == skin ? UiPalette.Gold : Color.clear;

            UiKit.SetIcon(_detailPortrait, skin.idle.Length > 0 ? skin.idle[0] : null);
            _detailName.text = HeroNames.Name(skin.id).ToUpperInvariant();
            _detailRole.text = $"{HeroNames.Role(skin.id).ToUpperInvariant()}  /  APPEARANCE {HeroNames.Variant(skin.id)}";

            float health = skin.maxHealth > 0f ? skin.maxHealth : 10f;
            float damage = skin.damageMod > 0f ? skin.damageMod : 1f;
            _health.SetAmount(health / 16f);
            _healthValue.text = health.ToString("0");
            _movement.SetAmount(Mathf.InverseLerp(4f, 6f, skin.speed));
            _movementValue.text = skin.speed.ToString("0.0");
            _damage.SetAmount(Mathf.InverseLerp(0.8f, 1.6f, damage));
            _damageValue.text = $"x{damage:0.0}";
        }
    }
}
