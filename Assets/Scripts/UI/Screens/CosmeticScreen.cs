using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class CosmeticScreen : UiScreen
    {
        readonly Image[] _motes = new Image[3];
        readonly Image[] _frames = new Image[GuildCosmetics.StyleCount];
        Text _status;
        Text _selectedName;
        Text _purchaseLabel;
        Text _equipLabel;
        UiButton _purchase;
        UiButton _restore;
        UiButton _retry;
        UiButton _equip;
        int _preview = 1;

        protected override void Build()
        {
            UiKit.Shade("Shade", Root, UiPalette.Ink.Alpha(0.9f));
            var dialog = UiKit.Dialog(Root, "GUILD COLLECTION", "THREE ANIMATED AURAS FOR EVERY HERO",
                UiPalette.Steel, 1000f, 600f);
            PopTarget(dialog.Holder);
            var back = UiKit.PushButton("Back", dialog.HeaderActions, "BACK", ButtonTone.Ghost,
                Game.CloseCosmetics, 14);
            UiKit.Corner(back.Rect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(112f, 48f));

            var preview = UiKit.Inset("Preview", dialog.Body);
            UiKit.Place(preview.rectTransform, 0f, 0f, 264f, 310f);
            var hero = UiKit.Icon("Hero", preview.transform, Game.Catalog.Catalog.characters[0].idle[0]);
            UiKit.Center(hero.rectTransform, 112f, 160f);
            for (int i = 0; i < _motes.Length; i++)
            {
                _motes[i] = UiKit.Glow($"Wisp {i}", preview.transform, UiPalette.Gold);
                UiKit.Center(_motes[i].rectTransform, 66f, 66f);
            }
            var previewLabel = UiKit.Label("Preview Label", preview.transform, "PREVIEW", 17,
                UiPalette.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(previewLabel.rectTransform, 0f, 12f, 264f, 28f);
            _selectedName = UiKit.Label("Style Name", preview.transform, string.Empty, 16,
                UiPalette.Cream, TextAnchor.MiddleCenter, true);
            UiKit.Place(_selectedName.rectTransform, 0f, 266f, 264f, 32f);

            var description = UiKit.Wrapped("Description", dialog.Body,
                "Make your delver your own. Choose a glowing companion aura that follows you into every chamber. " +
                "All three styles work with every hero.", 23, UiPalette.Cream);
            UiKit.Place(description.rectTransform, 288f, 2f, 668f, 92f);

            for (int i = 0; i < GuildCosmetics.StyleCount; i++)
            {
                int style = i;
                var button = UiKit.PushButton($"Style {i}", dialog.Body, GuildCosmetics.Name(i),
                    ButtonTone.Primary, () => { _preview = style; Refresh(); }, 11);
                UiKit.Place(button.Rect, 288f + i % 2 * 340f, 112f + i / 2 * 64f, 326f, 54f);
                _frames[i] = UiKit.Frame("Selected", button.transform, Color.clear);
                UiKit.Stretch(_frames[i].rectTransform, 0f, 0f, 0f, 0f);
            }

            _equip = UiKit.PushButton("Equip", dialog.Body, "EQUIP", ButtonTone.Primary,
                () => { Game.Cosmetics.Equip(_preview); Refresh(); }, 15);
            UiKit.Place(_equip.Rect, 288f, 250f, 326f, 60f);
            _equipLabel = UiKit.ButtonLabel(_equip);
            var note = UiKit.Wrapped("Note", dialog.Body, "COSMETIC ONLY\nYour stats and loot stay the same.",
                19, UiPalette.Muted);
            UiKit.Place(note.rectTransform, 632f, 254f, 320f, 56f);

            _purchase = UiKit.PushButton("Buy Pack", dialog.Body, "UNAVAILABLE", ButtonTone.Primary,
                () => Game.Cosmetics.Buy(), 16);
            UiKit.Place(_purchase.Rect, 0f, 332f, 474f, 64f);
            _purchaseLabel = UiKit.ButtonLabel(_purchase);
            _restore = UiKit.PushButton("Restore", dialog.Body, "RESTORE PURCHASES", ButtonTone.Ghost,
                () => Game.Cosmetics.Restore(), 14);
            UiKit.Place(_restore.Rect, 492f, 332f, 290f, 64f);
            _retry = UiKit.PushButton("Retry", dialog.Body, "RETRY", ButtonTone.Ghost,
                () => Game.Cosmetics.Connect(), 14);
            UiKit.Place(_retry.Rect, 798f, 332f, 166f, 64f);
            _status = UiKit.Wrapped("Status", dialog.Body, string.Empty, 20, UiPalette.Muted,
                TextAnchor.UpperCenter);
            UiKit.Place(_status.rectTransform, 0f, 414f, 964f, 66f);
        }

        public override void Refresh()
        {
            for (int i = 0; i < _frames.Length; i++)
                _frames[i].color = i == _preview ? GuildCosmetics.Color(i) : Color.clear;
            _selectedName.text = GuildCosmetics.Name(_preview);
            _selectedName.color = GuildCosmetics.Color(_preview);
            RefreshStore();
        }

        void RefreshStore()
        {
            var store = Game.Cosmetics;
            _status.text = store.Status;
            _purchase.Interactable = store.CanBuy;
            _purchaseLabel.text = store.Owned ? "COLLECTION OWNED"
                : store.Busy ? "PLEASE WAIT..."
                : store.CanBuy ? $"UNLOCK ALL  ·  {store.Price}" : "UNAVAILABLE";
            _restore.Interactable = store.Ready && !store.Busy;
            _retry.Interactable = store.Supported && !store.Ready && !store.Busy;
            bool equipped = GuildCosmetics.Equipped(Game.Save) == _preview;
            bool unlocked = GuildCosmetics.CanEquip(Game.Save, _preview);
            _equip.Interactable = unlocked && !equipped;
            _equipLabel.text = equipped ? "EQUIPPED" : unlocked ? "EQUIP STYLE" : "PREVIEW ONLY";
        }

        protected override void Tick()
        {
            RefreshStore();
            for (int i = 0; i < _motes.Length; i++)
            {
                _motes[i].enabled = _preview != 0;
                _motes[i].color = GuildCosmetics.Color(_preview);
                _motes[i].rectTransform.anchoredPosition = GuildCosmetics.Orbit(i, Time.unscaledTime) * 112f;
            }
        }
    }
}
