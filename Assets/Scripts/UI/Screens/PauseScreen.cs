using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class PauseScreen : UiScreen
    {
        const int LoadoutSlots = 6;

        readonly List<Image> _slotIcons = new();
        Image _portrait;
        Text _heroName;
        Text _runLine;
        Text _volume;

        protected override void Build()
        {
            UiKit.Shade("Shade", Root, UiPalette.Ink.Alpha(0.78f));

            var dialog = UiKit.Dialog(Root, "PAUSED", "THE DUNGEON HOLDS ITS BREATH",
                UiPalette.Crimson, 700f, 440f);
            PopTarget(dialog.Holder);

            var portraitFrame = UiKit.Inset("Portrait", dialog.Body);
            UiKit.Place(portraitFrame.rectTransform, 0f, 0f, 124f, 124f);
            _portrait = UiKit.Icon("Hero", portraitFrame.transform, null);
            UiKit.Stretch(_portrait.rectTransform, 14f, 10f, 14f, 10f);

            _heroName = UiKit.Label("Name", dialog.Body, string.Empty, 20, UiPalette.Cream,
                TextAnchor.UpperLeft, true);
            UiKit.Place(_heroName.rectTransform, 140f, 8f, 480f, 26f);
            _runLine = UiKit.Label("Run", dialog.Body, string.Empty, 17, UiPalette.Muted);
            UiKit.Place(_runLine.rectTransform, 140f, 40f, 480f, 22f);

            var loadout = UiKit.Node("Loadout", dialog.Body);
            UiKit.Place(loadout, 140f, 72f, 524f, 56f);
            for (int i = 0; i < LoadoutSlots; i++)
            {
                var slot = UiKit.Inset($"Slot {i}", loadout);
                UiKit.Place(slot.rectTransform, i * 62f, 0f, 54f, 54f);
                var icon = UiKit.Icon("Icon", slot.transform, null);
                UiKit.Stretch(icon.rectTransform, 9f, 9f, 9f, 9f);
                _slotIcons.Add(icon);
            }

            var home = UiKit.PushButton("Home", dialog.Body, "ABANDON RUN", ButtonTone.Danger,
                Game.ReturnToHub, 16);
            UiKit.Place(home.Rect, 0f, 156f, 200f, 62f);

            var resume = UiKit.PushButton("Resume", dialog.Body, "RESUME", ButtonTone.Primary,
                () => Game.SetPauseOpen(false), 16);
            UiKit.Place(resume.Rect, 216f, 156f, 220f, 62f);

            var volumePanel = UiKit.Inset("Volume", dialog.Body);
            UiKit.Place(volumePanel.rectTransform, 452f, 156f, 212f, 62f);
            var down = UiKit.PushButton("Down", volumePanel.transform, "-", ButtonTone.Ghost,
                () => Change(-1), 18);
            UiKit.Place(down.Rect, 8f, 8f, 46f, 46f);
            var label = UiKit.Label("Label", volumePanel.transform, "VOLUME", 15, UiPalette.Muted,
                TextAnchor.UpperCenter);
            UiKit.Place(label.rectTransform, 58f, 8f, 96f, 18f);
            _volume = UiKit.Label("Value", volumePanel.transform, string.Empty, 20, UiPalette.Cream,
                TextAnchor.UpperCenter, true);
            UiKit.Place(_volume.rectTransform, 58f, 28f, 96f, 26f);
            var up = UiKit.PushButton("Up", volumePanel.transform, "+", ButtonTone.Ghost,
                () => Change(1), 18);
            UiKit.Place(up.Rect, 158f, 8f, 46f, 46f);
        }

        void Change(int direction)
        {
            Game.ChangeVolume(direction);
            _volume.text = $"{Game.VolumeStep * 25}%";
        }

        public override void Refresh()
        {
            var skin = Game.ActiveSkin;
            UiKit.SetIcon(_portrait, skin != null && skin.idle.Length > 0 ? skin.idle[0] : null);
            _heroName.text = skin == null ? string.Empty : HeroNames.Name(skin.id).ToUpperInvariant();
            _runLine.text = Game.Mode == GameMode.HomeHub
                ? "HOME BASE  ·  SAFE ROOM"
                : $"CHAMBER {Game.CurrentRoom:00}   ·   {Game.Kills} DEFEATED";
            _volume.text = $"{Game.VolumeStep * 25}%";

            var inventory = Game.Inventory;
            for (int i = 0; i < _slotIcons.Count; i++)
            {
                UiKit.SetIcon(_slotIcons[i],
                    i < inventory.Count ? Game.Catalog.Weapon(inventory[i].weaponId) : null);
            }
        }
    }
}
