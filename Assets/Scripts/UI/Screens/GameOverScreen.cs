using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class GameOverScreen : UiScreen
    {
        Text _summary;

        protected override void Build()
        {
            UiKit.Shade("Shade", Root, new Color(0.10f, 0.01f, 0.02f, 0.8f));

            var dialog = UiKit.Dialog(Root, "RUN ENDED", "THE DUNGEON KEEPS WHAT IT TAKES",
                UiPalette.Crimson, 560f, 320f);
            PopTarget(dialog.Holder);

            _summary = UiKit.Wrapped("Summary", dialog.Body, string.Empty, 19, UiPalette.Cream,
                TextAnchor.UpperCenter);
            UiKit.Place(_summary.rectTransform, 20f, 14f, 484f, 92f);

            var home = UiKit.PushButton("Home", dialog.Body, "RETURN TO HOME BASE", ButtonTone.Danger,
                Game.ReturnToHub, 17);
            UiKit.Place(home.Rect, 60f, 130f, 404f, 66f);
        }

        public override void Refresh() => _summary.text =
            $"Reached chamber {Game.CurrentRoom} with {Game.Kills} kills.\nArtifacts and coins are saved.";
    }
}
