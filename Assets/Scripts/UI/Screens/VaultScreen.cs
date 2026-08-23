using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class VaultScreen : UiScreen
    {
        const float RowHeight = 78f;
        const float RowGap = 8f;

        readonly List<Artifact> _rows = new();
        readonly List<Image> _rowFrames = new();
        RectTransform _listContent;
        Text _summary;
        Artifact _selected;

        Image _detailIcon;
        Image _detailAccent;
        Text _detailRarity;
        Text _detailName;
        Text _detailQuality;
        Text _detailValue;
        UiBar _qualityBar;
        UiBar _damageBar;
        UiBar _speedBar;
        UiBar _critBar;
        Text _damageValue;
        Text _speedValue;
        Text _critValue;
        UiButton _equipButton;
        UiButton _listButton;
        Text _detailHint;
        Image _detailEmpty;

        protected override void Build()
        {
            var shade = UiKit.Shade("Shade", Root, UiPalette.Ink.Alpha(0.72f));
            shade.raycastTarget = true;

            var dialog = UiKit.Dialog(Root, "THE VAULT", "LOADOUT & ARTIFACTS  ·  STRONGEST FINDS FIRST",
                UiPalette.Crimson, 1084f, 640f);
            PopTarget(dialog.Holder);

            _summary = UiKit.Label("Summary", dialog.HeaderActions, string.Empty, 17, UiPalette.Gold,
                TextAnchor.MiddleRight);
            UiKit.Corner(_summary.rectTransform, new Vector2(1f, 0.5f), new Vector2(-124f, 0f),
                new Vector2(260f, 24f));

            var close = UiKit.PushButton("Close", dialog.HeaderActions, "RESUME  [I]", ButtonTone.Ghost,
                () => Game.SetInventoryOpen(false), 14);
            UiKit.Corner(close.Rect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(120f, 38f));

            var list = UiKit.ScrollList("List", dialog.Body, out _listContent);
            UiKit.Place((RectTransform)list.transform, 0f, 0f, 620f, 538f);

            var detail = UiKit.Node("Detail", dialog.Body);
            UiKit.Place(detail, 636f, 0f, 412f, 538f);
            BuildDetail(detail);
        }

        void BuildDetail(RectTransform parent)
        {
            var panel = UiKit.Inset("Panel", parent);
            UiKit.Stretch(panel.rectTransform, 0f, 0f, 0f, 0f);

            _detailEmpty = UiKit.Fill("Empty", panel.transform, Color.clear);
            UiKit.Stretch(_detailEmpty.rectTransform, 0f, 0f, 0f, 0f);
            var emptyLabel = UiKit.Label("Text", _detailEmpty.transform, "SELECT AN ARTIFACT", 18,
                UiPalette.Muted, TextAnchor.MiddleCenter);
            UiKit.Stretch(emptyLabel.rectTransform, 0f, 0f, 0f, 0f);

            _detailAccent = UiKit.Fill("Accent", panel.transform, UiPalette.Gold);
            UiKit.Place(_detailAccent.rectTransform, 0f, 0f, 412f, 5f);

            _detailRarity = UiKit.Label("Rarity", panel.transform, string.Empty, 16, UiPalette.Gold);
            UiKit.Place(_detailRarity.rectTransform, 22f, 16f, 368f, 22f);
            _detailName = UiKit.Label("Name", panel.transform, string.Empty, 20, UiPalette.Cream);
            UiKit.Place(_detailName.rectTransform, 22f, 40f, 368f, 28f);

            var preview = UiKit.Fill("Preview", panel.transform, new Color(0.02f, 0.035f, 0.06f));
            UiKit.Place(preview.rectTransform, 24f, 82f, 118f, 118f);
            var frame = UiKit.Frame("Frame", preview.transform, UiPalette.PanelLight.Alpha(0.65f));
            UiKit.Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);
            _detailIcon = UiKit.Icon("Icon", preview.transform, null);
            UiKit.Stretch(_detailIcon.rectTransform, 16f, 16f, 16f, 16f);

            var qualityLabel = UiKit.Label("Quality Label", panel.transform, "ROLL QUALITY", 15, UiPalette.Muted);
            UiKit.Place(qualityLabel.rectTransform, 158f, 84f, 230f, 20f);
            _detailQuality = UiKit.Label("Quality", panel.transform, string.Empty, 26, UiPalette.Cream, TextAnchor.UpperLeft, true);
            UiKit.Place(_detailQuality.rectTransform, 158f, 106f, 230f, 34f);
            _qualityBar = UiKit.Bar("Quality Bar", panel.transform, UiPalette.Gold);
            UiKit.Place(_qualityBar.Root, 158f, 150f, 230f, 10f);
            _detailValue = UiKit.Label("Value", panel.transform, string.Empty, 16, UiPalette.Muted);
            UiKit.Place(_detailValue.rectTransform, 158f, 166f, 230f, 20f);

            (_damageBar, _damageValue) = BuildStat(panel.transform, "DAMAGE", 224f);
            (_speedBar, _speedValue) = BuildStat(panel.transform, "ATTACK SPEED", 288f);
            (_critBar, _critValue) = BuildStat(panel.transform, "CRITICAL CHANCE", 352f);

            _equipButton = UiKit.PushButton("Equip", panel.transform, "EQUIP", ButtonTone.Primary,
                () => Game.Equip(_selected), 15);
            UiKit.Place(_equipButton.Rect, 24f, 428f, 178f, 48f);

            _listButton = UiKit.PushButton("List", panel.transform, "LIST", ButtonTone.Danger,
                () => Game.ListArtifact(_selected), 15);
            UiKit.Place(_listButton.Rect, 214f, 428f, 178f, 48f);

            _detailHint = UiKit.Label("Hint", panel.transform, string.Empty, 15, UiPalette.Muted,
                TextAnchor.UpperCenter);
            UiKit.Place(_detailHint.rectTransform, 20f, 486f, 372f, 20f);
        }

        static (UiBar bar, Text value) BuildStat(Transform parent, string label, float y)
        {
            var name = UiKit.Label($"{label} Label", parent, label, 15, UiPalette.Muted);
            UiKit.Place(name.rectTransform, 24f, y, 220f, 20f);
            var value = UiKit.Label($"{label} Value", parent, string.Empty, 17, UiPalette.Cream,
                TextAnchor.MiddleRight);
            UiKit.Place(value.rectTransform, 200f, y, 190f, 20f);
            var bar = UiKit.Bar($"{label} Bar", parent, UiPalette.Gold);
            UiKit.Place(bar.Root, 24f, y + 26f, 366f, 10f);
            return (bar, value);
        }

        public override void Refresh()
        {
            var inventory = Game.Inventory;
            var equipped = Game.EquippedArtifact;
            if (_selected == null || !inventory.Contains(_selected))
                _selected = equipped ?? inventory.OrderByDescending(x => x.quality).FirstOrDefault();

            _summary.text = $"{Game.Coins} COINS   ·   {inventory.Count} ARTIFACTS";

            UiKit.Clear(_listContent);
            _rows.Clear();
            _rowFrames.Clear();

            var ordered = inventory
                .OrderByDescending(x => equipped != null && x.id == equipped.id)
                .ThenByDescending(x => x.quality)
                .ToArray();

            for (int i = 0; i < ordered.Length; i++) BuildRow(ordered[i], i, equipped);
            UiKit.SetContentHeight(_listContent, ordered.Length * (RowHeight + RowGap));
            ShowDetail();
        }

        void BuildRow(Artifact artifact, int index, Artifact equipped)
        {
            var rarity = UiPalette.Rarity(artifact.rarity);
            var row = UiKit.Fill($"Row {index}", _listContent, UiPalette.RowFill);
            UiKit.Place(row.rectTransform, 0f, index * (RowHeight + RowGap), 588f, RowHeight);
            row.raycastTarget = true;

            var edge = UiKit.Fill("Edge", row.transform, rarity);
            UiKit.Place(edge.rectTransform, 0f, 0f, 5f, RowHeight);

            var frame = UiKit.Frame("Frame", row.transform, Color.clear);
            UiKit.Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);

            var icon = UiKit.Icon("Icon", row.transform, Game.Catalog.Weapon(artifact.weaponId));
            UiKit.Place(icon.rectTransform, 16f, 12f, 54f, 54f);

            var name = UiKit.Label("Name", row.transform,
                $"{artifact.rarity.ToUpperInvariant()}  {artifact.displayName}", 18, rarity);
            UiKit.Place(name.rectTransform, 82f, 12f, 372f, 24f);
            var stats = UiKit.Label("Stats", row.transform, artifact.Stats, 16, UiPalette.Muted);
            UiKit.Place(stats.rectTransform, 82f, 40f, 372f, 22f);

            var quality = UiKit.Label("Quality", row.transform, $"Q{artifact.quality}", 18, UiPalette.Cream,
                TextAnchor.MiddleRight);
            UiKit.Place(quality.rectTransform, 462f, 12f, 112f, 24f);
            bool isEquipped = equipped != null && artifact.id == equipped.id;
            var tag = UiKit.Label("Tag", row.transform,
                isEquipped ? "EQUIPPED" : $"{artifact.Price} COINS", 15,
                isEquipped ? UiPalette.Verdant : UiPalette.Gold, TextAnchor.MiddleRight);
            UiKit.Place(tag.rectTransform, 462f, 42f, 112f, 20f);

            var button = row.gameObject.AddComponent<UiButton>();
            button.Bind(row, frame, null, UiPalette.RowFill, UiPalette.RowHover);
            var captured = artifact;
            button.Clicked += () =>
            {
                _selected = captured;
                ShowDetail();
            };

            _rows.Add(artifact);
            _rowFrames.Add(frame);
        }

        void ShowDetail()
        {
            bool has = _selected != null;
            _detailEmpty.gameObject.SetActive(!has);
            for (int i = 0; i < _rows.Count; i++)
                _rowFrames[i].color = _rows[i] == _selected ? UiPalette.Gold : Color.clear;
            if (!has) return;

            var artifact = _selected;
            var equipped = Game.EquippedArtifact;
            var rarity = UiPalette.Rarity(artifact.rarity);

            _detailAccent.color = rarity;
            _detailRarity.text = artifact.rarity.ToUpperInvariant();
            _detailRarity.color = rarity;
            _detailName.text = artifact.displayName;
            UiKit.SetIcon(_detailIcon, Game.Catalog.Weapon(artifact.weaponId));
            _detailQuality.text = artifact.quality.ToString();
            _detailValue.text = $"VALUE  {artifact.Price} COINS";
            _qualityBar.SetAmount(artifact.quality / 100f);
            _qualityBar.Fill.color = rarity;

            _damageValue.text = artifact.EffectiveDamage.ToString();
            _damageBar.SetAmount(artifact.EffectiveDamage / 40f);
            _damageBar.Fill.color = rarity;
            _speedValue.text = $"{artifact.attacksPerSecond:0.00} / SEC";
            _speedBar.SetAmount(artifact.attacksPerSecond / 3.5f);
            _speedBar.Fill.color = rarity;
            _critValue.text = $"{artifact.criticalChance * 100f:0}%";
            _critBar.SetAmount(artifact.criticalChance / 0.3f);
            _critBar.Fill.color = rarity;

            bool isEquipped = equipped != null && artifact.id == equipped.id;
            _equipButton.Interactable = !isEquipped;
            UiKit.ButtonLabel(_equipButton).text = isEquipped ? "EQUIPPED" : "EQUIP";
            _listButton.Interactable = !isEquipped && !Game.Market.Busy;
            UiKit.ButtonLabel(_listButton).text = $"LIST  ·  {artifact.Price}";
            _detailHint.text = isEquipped
                ? "Equip another artifact before listing this one."
                : "Listing moves this artifact to the global market.";
        }
    }
}
