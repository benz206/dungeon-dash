using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class MarketAccountPanel : MonoBehaviour
    {
        DungeonGame _game;
        Text _description, _status, _supportId;
        UiButton _primary, _delete, _copy;
        bool _confirming;

        public void Initialize(DungeonGame game)
        {
            _game = game;
            var root = (RectTransform)transform;
            UiKit.Stretch(root, 0f, 0f, 0f, 0f);
            UiKit.Shade("Shade", root, UiPalette.Ink.Alpha(0.96f));
            var dialog = UiKit.Dialog(root, "ONLINE DATA", "YOUR MARKET ACCOUNT", UiPalette.Gold, 760f, 560f);
            var close = UiKit.PushButton("Close", dialog.HeaderActions, "CLOSE", ButtonTone.Ghost,
                () => Destroy(gameObject), 14);
            UiKit.Corner(close.Rect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(120f, 42f));
            _description = UiKit.Wrapped("Description", dialog.Body, string.Empty, 23, UiPalette.Cream);
            UiKit.Place(_description.rectTransform, 12f, 8f, 700f, 186f);
            _status = UiKit.Wrapped("Status", dialog.Body, string.Empty, 22, UiPalette.Gold);
            UiKit.Place(_status.rectTransform, 12f, 202f, 700f, 64f);
            _supportId = UiKit.Wrapped("Support ID", dialog.Body, string.Empty, 20, UiPalette.Muted);
            UiKit.Place(_supportId.rectTransform, 12f, 270f, 700f, 48f);
            _copy = UiKit.PushButton("Copy ID", dialog.Body, "COPY SUPPORT ID", ButtonTone.Ghost,
                () => GUIUtility.systemCopyBuffer = _game.Market.SupportId, 14);
            UiKit.Place(_copy.Rect, 12f, 326f, 700f, 42f);
            _primary = UiKit.PushButton("Primary", dialog.Body, "CONNECT ONLINE", ButtonTone.Primary,
                () =>
                {
                    if (_confirming) _confirming = false;
                    else _game.Market.EnableOnline();
                    Refresh();
                }, 14);
            UiKit.Place(_primary.Rect, 12f, 390f, 338f, 54f);
            _delete = UiKit.PushButton("Delete", dialog.Body, "DELETE ACCOUNT", ButtonTone.Danger,
                DeleteOrConfirm, 14);
            UiKit.Place(_delete.Rect, 370f, 390f, 342f, 54f);
            Refresh();
        }

        async void DeleteOrConfirm()
        {
            if (_game.Market.Busy) return;
            if (!_confirming && !_game.Market.DeletionPending)
            {
                _confirming = true;
                Refresh();
                return;
            }
            _confirming = false;
            await _game.Market.DeleteOnlineAccountAsync();
            if (this != null) Refresh();
        }

        void Update() => Refresh();

        void Refresh()
        {
            var market = _game.Market;
            _description.text = _confirming
                ? "DELETE YOUR ONLINE ACCOUNT?\n\nThis permanently removes your online market balance, unclaimed proceeds, and active listings. Listed artifacts will not return to your inventory.\n\nYour local characters, carried items, coins, and purchased cosmetic styles stay on this device."
                : market.DeletionPending
                    ? "Deletion is not yet confirmed. Market trades and new online connections are paused.\n\nReconnect and retry to finish. If your old account can no longer be accessed, keep the support ID below so its deletion can be checked. Your local dungeon progress and cosmetics are kept."
                    : "Online trading is optional. Connecting creates or restores an anonymous Unity account and sends market activity and gameplay-coin balances to Unity's services. Other traders receive your listings and seller ID.\n\nDungeon runs and the local simulated market work without an online account.";
            _status.text = !market.HasOnlineAccount && _game.Mode != GameMode.Market
                ? "Open the artifact market from the hub to choose online trading." : market.Status;
            _supportId.text = string.IsNullOrEmpty(market.SupportId) ? string.Empty : "SUPPORT ID  ·  " + market.SupportId;
            _copy.gameObject.SetActive(!string.IsNullOrEmpty(market.SupportId));
            _primary.gameObject.SetActive(_confirming || (!market.OnlineEnabled && !market.DeletionPending && _game.Mode == GameMode.Market));
            UiKit.ButtonLabel(_primary).text = _confirming ? "KEEP ACCOUNT" : "CONNECT ONLINE";
            _primary.Interactable = !market.Busy;
            _delete.gameObject.SetActive(market.HasOnlineAccount);
            UiKit.ButtonLabel(_delete).text = market.DeletionPending ? "RETRY DELETION" : _confirming ? "DELETE FOREVER" : "DELETE ACCOUNT";
            _delete.Interactable = !market.Busy;
        }
    }
}
