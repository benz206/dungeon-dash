using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class SlotScreen : UiScreen
    {
        const float CardGap = 22f;

        RectTransform _cards;

        protected override void Build()
        {
            var dialog = UiKit.Dialog(Root, "DELVERS", "CONTINUE A SAVE SLOT OR REGISTER A NEW OPERATIVE",
                UiPalette.Crimson, 1000f, 560f);
            PopTarget(dialog.Holder);

            var back = UiKit.PushButton("Back", dialog.HeaderActions, "BACK", ButtonTone.Ghost, Game.ShowTitle, 14);
            UiKit.Corner(back.Rect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(112f, 38f));

            _cards = UiKit.Node("Cards", dialog.Body);
            UiKit.Stretch(_cards, 0f, 0f, 0f, 0f);
        }

        public override void Refresh()
        {
            UiKit.Clear(_cards);
            float width = (964f - CardGap * (SaveData.MaxSlots - 1)) / SaveData.MaxSlots;
            for (int i = 0; i < SaveData.MaxSlots; i++)
            {
                var card = UiKit.Inset($"Slot {i}", _cards);
                UiKit.Place(card.rectTransform, i * (width + CardGap), 0f, width, 440f);
                if (i < Game.Save.slots.Count) BuildFilled(card.rectTransform, width, i);
                else BuildEmpty(card.rectTransform, width);
            }
        }

        void BuildEmpty(RectTransform card, float width)
        {
            var label = UiKit.Label("Empty", card, "EMPTY", 19, UiPalette.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(label.rectTransform, 0f, 178f, width, 26f);
            var create = UiKit.PushButton("Create", card, "+ NEW DELVER", ButtonTone.Primary,
                Game.OpenHeroPicker, 15);
            UiKit.Place(create.Rect, 24f, 216f, width - 48f, 52f);
        }

        void BuildFilled(RectTransform card, float width, int index)
        {
            var slot = Game.Save.slots[index];
            var skin = Game.Catalog.Character(slot.characterId);

            var accent = UiKit.Fill("Accent", card, UiPalette.Gold);
            UiKit.Place(accent.rectTransform, 0f, 0f, width, 5f);

            var name = UiKit.Label("Name", card, HeroNames.Name(skin.id).ToUpperInvariant(), 20,
                UiPalette.Cream, TextAnchor.UpperCenter, true);
            UiKit.Place(name.rectTransform, 0f, 22f, width, 26f);
            var role = UiKit.Label("Role", card, HeroNames.Role(skin.id).ToUpperInvariant(), 16,
                UiPalette.Gold, TextAnchor.UpperCenter);
            UiKit.Place(role.rectTransform, 0f, 50f, width, 20f);

            var preview = UiKit.Inset("Preview", card);
            UiKit.Place(preview.rectTransform, width / 2f - 66f, 82f, 132f, 142f);
            if (skin.idle.Length > 0)
            {
                var portrait = UiKit.Icon("Portrait", preview.transform, skin.idle[0]);
                UiKit.Stretch(portrait.rectTransform, 14f, 10f, 14f, 10f);
            }

            var stats = UiKit.Label("Stats", card,
                $"{slot.coins} COINS   ·   {slot.inventory.Count} ARTIFACTS", 17,
                UiPalette.Muted, TextAnchor.UpperCenter);
            UiKit.Place(stats.rectTransform, 0f, 238f, width, 22f);

            int captured = index;
            var play = UiKit.PushButton("Continue", card, "CONTINUE", ButtonTone.Primary,
                () => Game.ContinueSlot(captured), 16);
            UiKit.Place(play.Rect, 24f, 322f, width - 48f, 50f);

            var remove = UiKit.PushButton("Delete", card, "DELETE", ButtonTone.Danger,
                () => Game.DeleteSlotAt(captured), 14);
            UiKit.Place(remove.Rect, 24f, 380f, width - 48f, 42f);
        }
    }
}
