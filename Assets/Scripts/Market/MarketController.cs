using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DungeonDash
{
    public interface IMarketHost
    {
        int Coins { get; set; }
        void AddArtifact(Artifact artifact);
        bool RemoveArtifact(Artifact artifact);
        void Notify(string message);
        void PersistSave();
    }

    public sealed class MarketController
    {
        readonly IMarketHost _host;
        readonly SaveData _save;
        readonly LocalMarketService _local;
        readonly UgsMarketService _online;

        public MarketController(IMarketHost host, SaveData save, LocalMarketService local, UgsMarketService online)
        {
            _host = host;
            _save = save;
            _local = local;
            _online = online;
        }

        public bool UsingOnline { get; private set; }
        public bool Busy => _online.Busy;
        public string Status => _online.Status;
        public int PendingCoins => UsingOnline ? _online.PendingCoins : 0;

        public IReadOnlyList<MarketListing> Listings => UsingOnline ? _online.Listings : _local.Listings;

        public bool IsOwnListing(MarketListing listing) => UsingOnline
            ? listing.sellerId == _online.PlayerId
            : listing.sellerId == LocalMarketService.PlayerSeller;

        public void Seed(CatalogIndex catalog, System.Random random)
        {
            if (_local.Listings.Count > 0) return;
            for (int i = 0; i < Mathf.Min(8, catalog.ArtifactWeaponIds.Length); i++)
            {
                var artifact = ArtifactGenerator.Roll(catalog.ArtifactWeaponIds[i], random);
                _local.AddNpcListing(artifact, artifact.Price);
            }
            _host.PersistSave();
        }

        public string Serialize() => _local.Serialize();

        public int SimulateSales(System.Random random) => _local.SimulateSales(random);

        public void TrackCoinDelta(int amount)
        {
            if (_save.marketAccountInitialized) _save.marketPendingCoinDelta += amount;
        }

        public async void Open()
        {
            if (await EnsureOnline())
            {
                try
                {
                    var response = await _online.ClaimAsync();
                    SyncBalance();
                    if (response.message.StartsWith("Claimed", StringComparison.Ordinal)) _host.Notify(response.message);
                }
                catch (Exception exception)
                {
                    _host.Notify("Online market: " + exception.GetBaseException().Message);
                }
                return;
            }

            if (_online.IsOnline) return;
            UsingOnline = false;
            ClaimLocalProceeds();
        }

        public void ClaimLocalProceeds()
        {
            int coins = _local.ClaimProceeds();
            if (coins <= 0) return;
            _host.Coins += coins;
            TrackCoinDelta(coins);
            _host.Notify($"Collected {coins} market coins");
            _host.PersistSave();
        }

        public async void List(Artifact artifact)
        {
            if (Busy) return;
            if (!await EnsureOnline())
            {
                if (_online.IsOnline) return;
                if (!_host.RemoveArtifact(artifact)) return;
                _local.List(artifact, artifact.Price);
                _host.Notify($"Online unavailable — listed {artifact.displayName} locally");
                _host.PersistSave();
                return;
            }

            try
            {
                await _online.ListAsync(artifact, artifact.Price);
                _host.RemoveArtifact(artifact);
                SyncBalance();
                _host.Notify($"Listed {artifact.displayName} globally for {artifact.Price} coins");
            }
            catch (Exception exception)
            {
                _host.Notify("Listing failed: " + exception.GetBaseException().Message);
            }
        }

        public async void Buy(MarketListing listing)
        {
            if (Busy) return;
            if (!UsingOnline)
            {
                int coins = _host.Coins;
                var bought = _local.Buy(listing.id, ref coins);
                if (bought == null) return;
                TrackCoinDelta(coins - _host.Coins);
                _host.Coins = coins;
                _host.AddArtifact(bought);
                _host.Notify($"Bought {bought.displayName} locally");
                _host.PersistSave();
                return;
            }

            try
            {
                var response = await _online.BuyAsync(listing.id);
                if (response.artifact != null) _host.AddArtifact(response.artifact);
                SyncBalance();
                _host.Notify(response.message);
            }
            catch (Exception exception)
            {
                _host.Notify("Purchase failed: " + exception.GetBaseException().Message);
            }
        }

        public async void Cancel(MarketListing listing)
        {
            if (Busy) return;
            if (!UsingOnline)
            {
                var returned = _local.Cancel(listing.id);
                if (returned == null) return;
                _host.AddArtifact(returned);
                _host.Notify($"Returned {returned.displayName}");
                _host.PersistSave();
                return;
            }

            try
            {
                var response = await _online.CancelAsync(listing.id);
                if (response.artifact != null) _host.AddArtifact(response.artifact);
                SyncBalance();
                _host.Notify(response.message);
            }
            catch (Exception exception)
            {
                _host.Notify("Cancel failed: " + exception.GetBaseException().Message);
            }
        }

        public async void Refresh()
        {
            try
            {
                await _online.RefreshAsync();
                SyncBalance();
                _host.Notify("Market refreshed");
            }
            catch (Exception exception)
            {
                _host.Notify("Refresh failed: " + exception.GetBaseException().Message);
            }
        }

        public async void Claim()
        {
            try
            {
                var response = await _online.ClaimAsync();
                SyncBalance();
                _host.Notify(response.message);
            }
            catch (Exception exception)
            {
                _host.Notify("Claim failed: " + exception.GetBaseException().Message);
            }
        }

        async Task<bool> EnsureOnline()
        {
            try
            {
                bool connected;
                if (_online.IsOnline)
                {
                    if (_save.marketPendingCoinDelta != 0)
                        await _online.SyncCoinsAsync(_save.marketPendingCoinDelta);
                    connected = true;
                }
                else
                {
                    int initialBalance = Mathf.Max(0, _host.Coins - _save.marketPendingCoinDelta);
                    connected = await _online.ConnectAsync(initialBalance, _save.marketPendingCoinDelta);
                }

                if (!connected) return false;
                UsingOnline = true;
                _save.marketAccountInitialized = true;
                _save.marketPendingCoinDelta = 0;
                SyncBalance();
                return true;
            }
            catch (Exception exception)
            {
                _host.Notify("Online sync failed: " + exception.GetBaseException().Message);
                return false;
            }
        }

        void SyncBalance()
        {
            _host.Coins = _online.Balance;
            _host.PersistSave();
        }
    }
}
