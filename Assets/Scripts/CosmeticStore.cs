using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace DungeonDash
{
    public sealed class CosmeticStore : MonoBehaviour
    {
        DungeonGame _game;
        StoreController _store;
        Product _product;
        PendingOrder _pending;
        bool _checking;
        bool _checkAgain;

        public bool Supported => Application.platform == RuntimePlatform.IPhonePlayer;
        public bool Busy { get; private set; }
        public bool Ready { get; private set; }
        public bool Owned => _game.Save.guildCosmeticsOwned;
        public bool CanBuy => Ready && !Busy && !Owned && _pending == null;
        public string Price => _product?.metadata.localizedPriceString ?? string.Empty;
        public string Status { get; private set; } = "Cosmetic previews. Purchases are available on iPhone.";

        public void Initialize(DungeonGame game) => _game = game;

        public async void Connect()
        {
            if (!Supported || Busy || Ready) return;
            Busy = true;
            Status = "Connecting to the App Store...";
            if (_store == null)
            {
                _store = UnityIAPServices.StoreController();
                _store.OnStoreDisconnected += Disconnected;
                _store.OnProductsFetched += ProductsFetched;
                _store.OnProductsFetchFailed += ProductsFailed;
                _store.OnPurchasePending += PurchasePending;
                _store.OnPurchaseConfirmed += PurchaseConfirmed;
                _store.OnPurchaseFailed += PurchaseFailed;
                _store.OnPurchaseDeferred += PurchaseDeferred;
                _store.OnPurchasesFetched += PurchasesFetched;
                _store.OnPurchasesFetchFailed += PurchasesFailed;
                _store.OnCheckEntitlement += EntitlementChecked;
                _store.AppleStoreExtendedPurchaseService.OnEntitlementRevoked += EntitlementRevoked;
            }
            try
            {
                await _store.Connect();
                if (this == null) return;
                _store.FetchProductsWithNoRetries(new List<ProductDefinition>
                    { new(GuildCosmetics.ProductId, ProductType.NonConsumable) });
            }
            catch (Exception)
            {
                if (this != null) Unavailable();
            }
        }

        void ProductsFetched(List<Product> products)
        {
            _product = products.Find(product => product.definition.id == GuildCosmetics.ProductId);
            Ready = _product != null && _product.availableToPurchase && !string.IsNullOrEmpty(Price);
            if (!Ready) { Unavailable(); return; }
            _store.FetchPurchases();
        }

        public void Buy()
        {
            if (!CanBuy) return;
            Busy = true;
            Status = "Complete your purchase in the App Store.";
            _store.PurchaseProduct(_product);
        }

        public void Restore()
        {
            if (!Ready || Busy) return;
            Busy = true;
            Status = "Restoring purchases...";
            _store.RestoreTransactions((success, error) =>
            {
                if (this == null) return;
                if (success) _store.FetchPurchases();
                else { Busy = false; Status = "Restore could not finish. Please try again."; }
            });
        }

        public void RefreshPurchases()
        {
            if (!Ready || Busy) return;
            Busy = true;
            _store.FetchPurchases();
        }

        void PurchasePending(PendingOrder order)
        {
            if (!ContainsPack(order)) return;
            _pending = order;
            VerifyEntitlement();
        }

        void PurchasesFetched(Orders orders) => VerifyEntitlement();

        void VerifyEntitlement()
        {
            if (_product == null) return;
            if (_checking) { _checkAgain = true; return; }
            _checking = true;
            Busy = true;
            Status = "Checking your cosmetic collection...";
            // StoreKit's entitlement check verifies the signed transaction and checks revocation.
            // A purchase callback alone is not sufficient to grant this pack.
            _store.CheckEntitlement(_product);
        }

        void EntitlementChecked(Entitlement entitlement)
        {
            _checking = false;
            if (entitlement.Product?.definition.id != GuildCosmetics.ProductId)
            {
                Busy = false;
                Status = "Could not verify purchases. Please try Restore.";
                return;
            }
            bool entitled = entitlement.Status is EntitlementStatus.FullyEntitled or EntitlementStatus.EntitledButNotFinished;
            if (entitled)
            {
                SetOwned(true);
                if (_pending != null)
                {
                    var order = _pending;
                    _pending = null;
                    // Persist the permanent unlock before acknowledging delivery to Apple.
                    _store.ConfirmPurchase(order);
                }
                Status = "Guild Collection unlocked. Thank you for supporting Dungeon Dash.";
            }
            else if (entitlement.Status == EntitlementStatus.NotEntitled)
            {
                SetOwned(false);
                Status = _pending == null ? "One purchase. Three auras. Every hero." : "Purchase verification failed. Please try Restore.";
            }
            else Status = "Could not verify purchases. Please try Restore.";
            Busy = false;
            if (_checkAgain)
            {
                _checkAgain = false;
                VerifyEntitlement();
            }
        }

        void SetOwned(bool owned)
        {
            _game.Save.guildCosmeticsOwned = owned;
            if (!owned) _game.Save.cosmeticStyle = 0;
            _game.PersistSave();
        }

        void EntitlementRevoked(string productId)
        {
            if (productId != GuildCosmetics.ProductId) return;
            SetOwned(false);
            Status = "This purchase is no longer active. Classic style is equipped.";
        }

        public void Equip(int style)
        {
            if (!GuildCosmetics.CanEquip(_game.Save, style)) return;
            _game.Save.cosmeticStyle = style;
            _game.PersistSave();
        }

        static bool ContainsPack(Order order)
        {
            foreach (var item in order.CartOrdered.Items())
                if (item.Product.definition.id == GuildCosmetics.ProductId) return true;
            return false;
        }

        void PurchaseConfirmed(Order order)
        {
            if (order is FailedOrder) Status = "Unlocked. Apple confirmation will retry when purchases are restored.";
        }

        void PurchaseFailed(FailedOrder order)
        {
            Busy = false;
            Status = order.FailureReason == PurchaseFailureReason.UserCancelled
                ? "Purchase cancelled. You can keep playing for free."
                : "Purchase could not finish. Please try again.";
        }

        void PurchaseDeferred(DeferredOrder order)
        {
            Busy = false;
            Status = "Awaiting approval. Your pack unlocks after Apple confirms it.";
        }

        void Unavailable()
        {
            Ready = false;
            Busy = false;
            _checking = false;
            _checkAgain = false;
            Status = "The App Store is unavailable. You can keep playing for free.";
        }

        void Disconnected(StoreConnectionFailureDescription failure) => Unavailable();
        void ProductsFailed(ProductFetchFailed failure) => Unavailable();
        void PurchasesFailed(PurchasesFetchFailureDescription failure)
        {
            Busy = false;
            Status = "Could not load purchases. Please try Restore.";
        }

        void OnDestroy()
        {
            if (_store == null) return;
            _store.OnStoreDisconnected -= Disconnected;
            _store.OnProductsFetched -= ProductsFetched;
            _store.OnProductsFetchFailed -= ProductsFailed;
            _store.OnPurchasePending -= PurchasePending;
            _store.OnPurchaseConfirmed -= PurchaseConfirmed;
            _store.OnPurchaseFailed -= PurchaseFailed;
            _store.OnPurchaseDeferred -= PurchaseDeferred;
            _store.OnPurchasesFetched -= PurchasesFetched;
            _store.OnPurchasesFetchFailed -= PurchasesFailed;
            _store.OnCheckEntitlement -= EntitlementChecked;
            _store.AppleStoreExtendedPurchaseService.OnEntitlementRevoked -= EntitlementRevoked;
        }
    }
}
