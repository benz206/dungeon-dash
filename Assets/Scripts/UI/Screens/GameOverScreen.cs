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

            var dialog = UiKit.Dialog(Root, "RUN ENDED", "EVERY EXPEDITION LEAVES ITS MARK",
                UiPalette.Crimson, 560f, 360f);
            PopTarget(dialog.Holder);

            _summary = UiKit.Wrapped("Summary", dialog.Body, string.Empty, 19, UiPalette.Cream,
                TextAnchor.UpperCenter);
            UiKit.Place(_summary.rectTransform, 20f, 14f, 484f, 130f);

            var home = UiKit.PushButton("Home", dialog.Body, "RETURN TO HOME BASE", ButtonTone.Danger,
                Game.ReturnToHub, 17);
            UiKit.Place(home.Rect, 60f, 168f, 404f, 66f);
        }

        public override void Refresh() => _summary.text =
            $"Reached chamber {Game.CurrentRoom} · {Game.Kills} defeated\n" +
            $"{Game.GuildRank} · BEST CLEAR {Game.BestChamberCleared:00}\n" +
            $"{Game.ProgressGoal}\n{Game.LifetimeKills} lifetime defeats\nArtifacts and coins are saved.";
    }
}
