using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class HudView : MonoBehaviour
    {
        const int MaxHeartIcons = 12;
        const float BarSpeed = 6f;

        DungeonGame _game;
        Image _portrait;
        readonly List<Image> _hearts = new();
        Text _coins;
        Image _weaponIcon;
        Text _weaponName;
        Text _weaponStats;
        Image _chamberBar;
        Text _chamberLabel;
        Text _chamberDetail;
        UiBar _clearBar;
        UiButton _vaultButton;
        UiButton _marketButton;
        Text _hint;
        CanvasGroup _group;
        float _clearAmount;
        int _lastCoins = -1;
        int _lastHealth = -1;
        int _lastChamber = -1;
        int _lastKills = -1;
        string _lastWeaponId;

        public void Initialize(DungeonGame game)
        {
            _game = game;
            var root = (RectTransform)transform;
            UiKit.Stretch(root, 0f, 0f, 0f, 0f);
            _group = UiKit.Group(root);

            BuildStatusPanel(root);
            BuildChamberBanner(root);
            BuildMenuButtons(root);
            BuildHint(root);
            Refresh();
        }

        void BuildStatusPanel(RectTransform parent)
        {
            var panel = UiKit.Panel("Status", parent);
            UiKit.Place(panel.rectTransform, 18f, 18f, 430f, 92f);

            var portraitFrame = UiKit.Inset("Portrait", panel.transform);
            UiKit.Place(portraitFrame.rectTransform, 10f, 10f, 72f, 72f);
            _portrait = UiKit.Icon("Hero", portraitFrame.transform, null);
            UiKit.Stretch(_portrait.rectTransform, 10f, 6f, 10f, 6f);

            var heartRow = UiKit.Node("Hearts", panel.transform);
            UiKit.Place(heartRow, 92f, 14f, 320f, 26f);
            for (int i = 0; i < MaxHeartIcons; i++)
            {
                var heart = UiKit.Icon($"Heart {i}", heartRow, null);
                UiKit.Place(heart.rectTransform, i * 26f, 0f, 24f, 24f);
                heart.enabled = false;
                _hearts.Add(heart);
            }

            var coinIcon = UiKit.Icon("Coin", panel.transform, null);
            UiKit.Place(coinIcon.rectTransform, 92f, 46f, 20f, 20f);
            var coinSprites = _game.Catalog.Catalog.coins;
            if (coinSprites.Length > 0) coinIcon.sprite = coinSprites[0];
            _coins = UiKit.Label("Coins", panel.transform, "0", 19, UiPalette.Gold);
            UiKit.Place(_coins.rectTransform, 118f, 44f, 80f, 24f);

            var weaponFrame = UiKit.Inset("Weapon", panel.transform);
            UiKit.Place(weaponFrame.rectTransform, 196f, 42f, 222f, 40f);
            _weaponIcon = UiKit.Icon("Icon", weaponFrame.transform, null);
            UiKit.Place(_weaponIcon.rectTransform, 6f, 5f, 30f, 30f);
            _weaponName = UiKit.Label("Name", weaponFrame.transform, string.Empty, 17, UiPalette.Cream);
            UiKit.Place(_weaponName.rectTransform, 42f, 3f, 174f, 20f);
            _weaponStats = UiKit.Label("Stats", weaponFrame.transform, string.Empty, 15, UiPalette.Muted);
            UiKit.Place(_weaponStats.rectTransform, 42f, 21f, 174f, 18f);
        }

        void BuildChamberBanner(RectTransform parent)
        {
            var banner = UiKit.Panel("Chamber", parent);
            UiKit.Corner(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                new Vector2(300f, 76f));

            _chamberBar = UiKit.Header("Accent", banner.transform, UiPalette.Crimson);
            UiKit.Stretch(_chamberBar.rectTransform, 6f, 6f, 6f, 6f);

            _chamberLabel = UiKit.Label("Name", _chamberBar.transform, "CHAMBER 01", 18, UiPalette.Cream,
                TextAnchor.UpperCenter, true);
            UiKit.Place(_chamberLabel.rectTransform, 0f, 10f, 288f, 24f);
            _chamberDetail = UiKit.Label("Detail", _chamberBar.transform, string.Empty, 16,
                UiPalette.Cream.Alpha(0.8f), TextAnchor.UpperCenter);
            UiKit.Place(_chamberDetail.rectTransform, 0f, 34f, 288f, 20f);

            _clearBar = UiKit.Bar("Clear", banner.transform, UiPalette.Gold);
            UiKit.Corner(_clearBar.Root, new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(264f, 8f));
        }

        void BuildMenuButtons(RectTransform parent)
        {
            _vaultButton = UiKit.PushButton("Vault", parent, "VAULT  [I]", ButtonTone.Primary,
                () => _game.SetInventoryOpen(true));
            UiKit.Corner(_vaultButton.Rect, new Vector2(1f, 1f), new Vector2(-186f, -20f), new Vector2(158f, 48f));

            _marketButton = UiKit.PushButton("Market", parent, "MARKET  [M]", ButtonTone.Primary,
                _game.OpenMarketOverlay);
            UiKit.Corner(_marketButton.Rect, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(158f, 48f));
        }

        void BuildHint(RectTransform parent)
        {
            var strip = UiKit.Inset("Hint", parent);
            UiKit.Corner(strip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(760f, 34f));
            strip.color = UiPalette.Ink.Alpha(0.72f);
            _hint = UiKit.Label("Text", strip.transform, string.Empty, 17, UiPalette.Muted,
                TextAnchor.MiddleCenter);
            UiKit.Stretch(_hint.rectTransform, 12f, 0f, 12f, 0f);
        }

        public void SetDimmed(bool dimmed) => _group.alpha = dimmed ? 0.35f : 1f;

        public void Refresh()
        {
            var skin = _game.ActiveSkin;
            if (skin != null && skin.idle.Length > 0) _portrait.sprite = skin.idle[0];
            _lastWeaponId = null;
            _lastCoins = -1;
            _lastHealth = -1;
            _lastChamber = -1;
            _lastKills = -1;
            Tick();
        }

        public void Tick()
        {
            var player = _game.Player;
            if (player != null && player.Health != _lastHealth)
            {
                _lastHealth = player.Health;
                RefreshHearts(player);
            }

            if (_game.Coins != _lastCoins)
            {
                _lastCoins = _game.Coins;
                _coins.text = _lastCoins.ToString();
            }

            var equipped = _game.EquippedArtifact;
            string weaponId = equipped?.id;
            if (weaponId != _lastWeaponId)
            {
                _lastWeaponId = weaponId;
                _weaponIcon.sprite = equipped == null ? null : _game.Catalog.Weapon(equipped.weaponId);
                _weaponIcon.enabled = _weaponIcon.sprite != null;
                _weaponName.text = equipped == null ? "UNARMED" : equipped.displayName;
                _weaponStats.text = equipped == null ? string.Empty : equipped.Stats;
                _weaponName.color = equipped == null ? UiPalette.Muted : UiPalette.Rarity(equipped.rarity);
            }

            bool inHub = _game.Mode == GameMode.HomeHub;
            if (_game.CurrentRoom != _lastChamber || _game.Kills != _lastKills)
            {
                _lastChamber = _game.CurrentRoom;
                _lastKills = _game.Kills;
                var theme = _game.Theme;
                _chamberBar.color = inHub ? UiPalette.Steel : theme != null ? theme.accent : UiPalette.Crimson;
                _chamberLabel.text = inHub ? "HOME BASE" : $"CHAMBER {_lastChamber:00}";
                _chamberDetail.text = inHub
                    ? "SAFE ROOM"
                    : theme != null ? theme.displayName.ToUpperInvariant() : $"{_lastKills} DEFEATED";
            }

            float target = inHub ? 1f : _game.ChamberClearProgress;
            _clearAmount = Mathf.MoveTowards(_clearAmount, target, BarSpeed * Time.unscaledDeltaTime);
            _clearBar.SetAmount(_clearAmount);
            _clearBar.Fill.color = _game.RoomExitUnlocked || inHub ? UiPalette.Verdant : UiPalette.Gold;

            _hint.text = inHub
                ? "WASD MOVE   ·   E INTERACT   ·   I VAULT   ·   M MARKET   ·   ESC PAUSE"
                : "WASD MOVE   ·   LMB ATTACK   ·   RMB DASH   ·   E INTERACT   ·   ESC PAUSE";
        }

        void RefreshHearts(PlayerController player)
        {
            var catalog = _game.Catalog.Catalog;
            int slots = Mathf.Min(MaxHeartIcons, Mathf.Max(0, player.MaxHealth / 2));
            for (int i = 0; i < _hearts.Count; i++)
            {
                bool used = i < slots;
                _hearts[i].enabled = used;
                if (!used) continue;
                int health = player.Health - i * 2;
                _hearts[i].sprite = health >= 2 ? catalog.heartFull
                    : health == 1 ? catalog.heartHalf
                    : catalog.heartEmpty;
            }
        }
    }
}
