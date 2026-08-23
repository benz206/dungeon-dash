using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class MarketScreen : UiScreen
    {
        const float RowHeight = 72f;
        const float RowGap = 8f;
        const float StatusInterval = 0.4f;

        RectTransform _listContent;
        Text _status;
        Text _balance;
        UiButton _refresh;
        UiButton _claim;
        UiButton _retry;
        float _nextStatusCheck;
        string _lastStatus;

        protected override void Build()
        {
            UiKit.Shade("Shade", Root, UiPalette.Ink.Alpha(0.72f));

            var dialog = UiKit.Dialog(Root, "GLOBAL MARKET", "TRADE ARTIFACTS WITH OTHER DELVERS",
                UiPalette.Gold, 900f, 660f);
            PopTarget(dialog.Holder);

            var close = UiKit.PushButton("Close", dialog.HeaderActions, "CLOSE  [M]", ButtonTone.Ghost,
                Game.CloseMarket, 14);
            UiKit.Corner(close.Rect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(126f, 38f));

            var bar = UiKit.Inset("Status", dialog.Body);
            UiKit.Place(bar.rectTransform, 0f, 0f, 864f, 52f);
            _status = UiKit.Label("Text", bar.transform, string.Empty, 17, UiPalette.Muted);
            UiKit.Place(_status.rectTransform, 16f, 6f, 520f, 40f);
            _balance = UiKit.Label("Balance", bar.transform, string.Empty, 19, UiPalette.Gold,
                TextAnchor.MiddleRight);
            UiKit.Place(_balance.rectTransform, 546f, 6f, 302f, 40f);

            _refresh = UiKit.PushButton("Refresh", dialog.Body, "REFRESH", ButtonTone.Primary,
                Game.RefreshMarket, 14);
            UiKit.Place(_refresh.Rect, 0f, 64f, 168f, 40f);
            _claim = UiKit.PushButton("Claim", dialog.Body, "CLAIM PROCEEDS", ButtonTone.Primary,
                Game.ClaimMarket, 14);
            UiKit.Place(_claim.Rect, 180f, 64f, 218f, 40f);
            _retry = UiKit.PushButton("Retry", dialog.Body, "RETRY ONLINE", ButtonTone.Ghost,
                Game.RetryOnlineMarket, 14);
            UiKit.Place(_retry.Rect, 410f, 64f, 190f, 40f);

            var list = UiKit.ScrollList("List", dialog.Body, out _listContent);
            UiKit.Place((RectTransform)list.transform, 0f, 116f, 864f, 442f);
        }

        public override void Refresh()
        {
            var market = Game.Market;
            _lastStatus = market.Status;
            _status.text = market.Status;
            string pending = market.PendingCoins > 0 ? $"   ·   {market.PendingCoins} PROCEEDS READY" : string.Empty;
            _balance.text = $"{Game.Coins} COINS{pending}";

            _refresh.gameObject.SetActive(market.UsingOnline);
            _claim.gameObject.SetActive(market.UsingOnline);
            _retry.gameObject.SetActive(!market.UsingOnline);
            _refresh.Interactable = !market.Busy;
            _claim.Interactable = !market.Busy;
            _retry.Interactable = !market.Busy;

            UiKit.Clear(_listContent);
            var listings = market.Listings;
            for (int i = 0; i < listings.Count; i++) BuildRow(listings[i], i, market);
            UiKit.SetContentHeight(_listContent, listings.Count * (RowHeight + RowGap));
        }

        void BuildRow(MarketListing listing, int index, MarketController market)
        {
            var artifact = listing.artifact;
            var rarity = UiPalette.Rarity(artifact.rarity);

            var row = UiKit.Fill($"Row {index}", _listContent, UiPalette.RowFill);
            UiKit.Place(row.rectTransform, 0f, index * (RowHeight + RowGap), 832f, RowHeight);

            var edge = UiKit.Fill("Edge", row.transform, rarity);
            UiKit.Place(edge.rectTransform, 0f, 0f, 5f, RowHeight);

            var icon = UiKit.Icon("Icon", row.transform, Game.Catalog.Weapon(artifact.weaponId));
            UiKit.Place(icon.rectTransform, 16f, 10f, 52f, 52f);

            var name = UiKit.Label("Name", row.transform,
                $"{artifact.rarity.ToUpperInvariant()}  {artifact.displayName}", 18, rarity);
            UiKit.Place(name.rectTransform, 80f, 10f, 470f, 24f);
            var stats = UiKit.Label("Stats", row.transform,
                $"{artifact.Stats}   ·   Q{artifact.quality}", 16, UiPalette.Muted);
            UiKit.Place(stats.rectTransform, 80f, 36f, 470f, 22f);

            var price = UiKit.Label("Price", row.transform, $"{listing.price} COINS", 18, UiPalette.Gold,
                TextAnchor.MiddleRight);
            UiKit.Place(price.rectTransform, 556f, 24f, 140f, 24f);

            bool own = market.IsOwnListing(listing);
            var captured = listing;
            var action = UiKit.PushButton("Action", row.transform, own ? "CANCEL" : "BUY",
                own ? ButtonTone.Danger : ButtonTone.Primary,
                () =>
                {
                    if (own) Game.CancelListing(captured);
                    else Game.BuyListing(captured);
                }, 14);
            UiKit.Place(action.Rect, 712f, 16f, 104f, 40f);
            action.Interactable = !market.Busy && (own || Game.Coins >= listing.price);
        }

        protected override void Tick()
        {
            if (Time.unscaledTime < _nextStatusCheck) return;
            _nextStatusCheck = Time.unscaledTime + StatusInterval;
            if (Game.Market.Status == _lastStatus) return;
            Refresh();
        }
    }
}
