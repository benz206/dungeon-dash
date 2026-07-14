using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonDash
{
    [Serializable]
    public sealed class Artifact
    {
        public string id;
        public string weaponId;
        public string displayName;
        public int damage;
        public float attacksPerSecond;
        public float criticalChance;
        public int quality;
        public string rarity;

        public int Price => Mathf.Max(8, Mathf.RoundToInt(damage * attacksPerSecond * (1f + criticalChance * 3f) * 3f));
        public string Stats => $"{damage} dmg  {attacksPerSecond:0.00}/s  {criticalChance * 100f:0}% crit";
    }

    public static class ArtifactGenerator
    {
        public static Artifact Roll(string weaponId, System.Random random)
        {
            // Four independent successes are required for a near-perfect roll.
            // This makes ordinary items common while preserving an exciting long tail.
            double quality = Math.Pow(random.NextDouble(), 4d);
            int score = Mathf.Clamp(1 + (int)Math.Round(quality * 99d), 1, 100);
            string cleanName = weaponId.Replace("weapon_", "").Replace('_', ' ');
            return new Artifact
            {
                id = Guid.NewGuid().ToString("N"),
                weaponId = weaponId,
                displayName = char.ToUpperInvariant(cleanName[0]) + cleanName.Substring(1),
                damage = 5 + Mathf.RoundToInt((float)quality * 25f),
                attacksPerSecond = 1.2f + (float)quality * 2.3f,
                criticalChance = 0.03f + (float)quality * 0.27f,
                quality = score,
                rarity = score >= 85 ? "Mythic" : score >= 60 ? "Epic" : score >= 35 ? "Rare" : "Common"
            };
        }
    }

    [Serializable]
    public sealed class MarketListing
    {
        public string id;
        public string sellerId;
        public int price;
        public Artifact artifact;
    }

    [Serializable]
    public sealed class MarketSnapshot
    {
        public List<MarketListing> listings = new();
        public int pendingCoins;
    }

    public interface IMarketService
    {
        IReadOnlyList<MarketListing> Listings { get; }
        int PendingCoins { get; }
        void AddNpcListing(Artifact artifact, int price);
        void List(Artifact artifact, int price);
        Artifact Buy(string listingId, ref int coins);
        Artifact Cancel(string listingId);
        int SimulateSales(System.Random random);
        int ClaimProceeds();
        string Serialize();
    }

    public sealed class LocalMarketService : IMarketService
    {
        public const string PlayerSeller = "player";
        readonly MarketSnapshot _snapshot;

        public LocalMarketService(string json = null)
        {
            _snapshot = string.IsNullOrEmpty(json) ? new MarketSnapshot() : JsonUtility.FromJson<MarketSnapshot>(json);
            _snapshot.listings ??= new List<MarketListing>();
        }

        public IReadOnlyList<MarketListing> Listings => _snapshot.listings;
        public int PendingCoins => _snapshot.pendingCoins;

        public void AddNpcListing(Artifact artifact, int price) => Add(artifact, price, "wanderer");
        public void List(Artifact artifact, int price) => Add(artifact, price, PlayerSeller);

        void Add(Artifact artifact, int price, string sellerId)
        {
            _snapshot.listings.Add(new MarketListing
            {
                id = Guid.NewGuid().ToString("N"), sellerId = sellerId,
                artifact = artifact, price = Mathf.Max(1, price)
            });
        }

        public Artifact Buy(string listingId, ref int coins)
        {
            var listing = _snapshot.listings.FirstOrDefault(x => x.id == listingId && x.sellerId != PlayerSeller);
            if (listing == null || coins < listing.price) return null;
            coins -= listing.price;
            _snapshot.listings.Remove(listing);
            return listing.artifact;
        }

        public Artifact Cancel(string listingId)
        {
            var listing = _snapshot.listings.FirstOrDefault(x => x.id == listingId && x.sellerId == PlayerSeller);
            if (listing == null) return null;
            _snapshot.listings.Remove(listing);
            return listing.artifact;
        }

        public int SimulateSales(System.Random random)
        {
            int sold = 0;
            foreach (var listing in _snapshot.listings.Where(x => x.sellerId == PlayerSeller).ToArray())
            {
                if (random.NextDouble() >= 0.45d) continue;
                _snapshot.pendingCoins += listing.price;
                _snapshot.listings.Remove(listing);
                sold++;
            }
            return sold;
        }

        public int ClaimProceeds()
        {
            int result = _snapshot.pendingCoins;
            _snapshot.pendingCoins = 0;
            return result;
        }

        public string Serialize() => JsonUtility.ToJson(_snapshot);
    }
}
