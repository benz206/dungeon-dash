using UnityEngine;

namespace DungeonDash
{
    public sealed class TitleScreen : UiScreen
    {
        const int RosterPreview = 6;

        protected override void Build()
        {
            var dialog = UiKit.Dialog(Root, "DUNGEON DASH", "A POCKET ROGUELITE",
                UiPalette.Crimson, 720f, 430f);
            PopTarget(dialog.Holder);

            var tagline = UiKit.Label("Tagline", dialog.Body,
                "CHOOSE A DELVER   ·   CLEAR CHAMBERS   ·   HUNT RARE ARTIFACTS",
                17, UiPalette.Muted, TextAnchor.UpperCenter);
            UiKit.Place(tagline.rectTransform, 0f, 4f, 684f, 24f);

            var roster = UiKit.Node("Roster", dialog.Body);
            UiKit.Place(roster, 20f, 40f, 644f, 116f);
            var characters = Game.Catalog.Catalog.characters;
            for (int i = 0; i < RosterPreview; i++)
            {
                var slot = UiKit.Inset($"Slot {i}", roster);
                UiKit.Place(slot.rectTransform, i * 108f, 0f, 96f, 112f);
                if (i >= characters.Length || characters[i].idle.Length == 0) continue;
                var portrait = UiKit.Icon("Portrait", slot.transform, characters[i].idle[0]);
                UiKit.Stretch(portrait.rectTransform, 12f, 8f, 12f, 8f);
            }

            var enter = UiKit.PushButton("Enter", dialog.Body, "ENTER THE DUNGEON", ButtonTone.Primary,
                Game.OpenSlotSelect, 18);
            UiKit.Place(enter.Rect, 20f, 180f, 420f, 66f);

            var quit = UiKit.PushButton("Quit", dialog.Body, "QUIT", ButtonTone.Danger, Game.QuitGame, 18);
            UiKit.Place(quit.Rect, 458f, 180f, 206f, 66f);
        }
    }
}
