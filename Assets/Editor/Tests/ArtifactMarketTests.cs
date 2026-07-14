using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDash;
using NUnit.Framework;

namespace DungeonDashTests
{
    public sealed class ArtifactMarketTests
    {
        [Test]
        public void Roll_StaysWithinDocumentedStatBounds()
        {
            var random = new Random(31);
            for (int i = 0; i < 10000; i++)
            {
                var artifact = ArtifactGenerator.Roll("weapon_sword", random);
                Assert.That(artifact.quality, Is.InRange(1, 100));
                Assert.That(artifact.damage, Is.InRange(5, 30));
                Assert.That(artifact.attacksPerSecond, Is.InRange(1.2f, 3.5f));
                Assert.That(artifact.criticalChance, Is.InRange(0.03f, 0.30f));
                Assert.That(artifact.Price, Is.GreaterThan(0));
            }
        }

        [Test]
        public void Roll_HigherRaritiesAreProgressivelyHarderToFind()
        {
            var random = new Random(82);
            var counts = new Dictionary<string, int>
            {
                ["Common"] = 0, ["Rare"] = 0, ["Epic"] = 0, ["Mythic"] = 0
            };
            for (int i = 0; i < 100000; i++)
                counts[ArtifactGenerator.Roll("weapon_sword", random).rarity]++;

            Assert.That(counts["Common"], Is.GreaterThan(counts["Rare"]));
            Assert.That(counts["Rare"], Is.GreaterThan(counts["Epic"]));
            Assert.That(counts["Epic"], Is.GreaterThan(counts["Mythic"]));
            Assert.That(counts["Mythic"], Is.LessThan(6000));
        }

        [Test]
        public void Roll_AssignsUniqueArtifactIds()
        {
            var random = new Random(7);
            var ids = Enumerable.Range(0, 1000)
                .Select(_ => ArtifactGenerator.Roll("weapon_axe", random).id)
                .ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }

        [Test]
        public void Market_BuyMovesArtifactAndChargesExactPrice()
        {
            var market = new LocalMarketService();
            var artifact = ArtifactGenerator.Roll("weapon_bow", new Random(4));
            market.AddNpcListing(artifact, 17);
            int coins = 30;

            var bought = market.Buy(market.Listings[0].id, ref coins);

            Assert.That(bought, Is.SameAs(artifact));
            Assert.That(coins, Is.EqualTo(13));
            Assert.That(market.Listings, Is.Empty);
        }

        [Test]
        public void Market_RejectsUnaffordableAndOwnListings()
        {
            var market = new LocalMarketService();
            var artifact = ArtifactGenerator.Roll("weapon_bow", new Random(4));
            market.AddNpcListing(artifact, 17);
            int coins = 5;
            Assert.That(market.Buy(market.Listings[0].id, ref coins), Is.Null);
            Assert.That(coins, Is.EqualTo(5));

            market.List(ArtifactGenerator.Roll("weapon_axe", new Random(5)), 10);
            coins = 100;
            Assert.That(market.Buy(market.Listings[1].id, ref coins), Is.Null);
            Assert.That(coins, Is.EqualTo(100));
        }

        [Test]
        public void Market_RoundTripPreservesListings()
        {
            var market = new LocalMarketService();
            var artifact = ArtifactGenerator.Roll("weapon_staff", new Random(8));
            market.List(artifact, 42);

            var restored = new LocalMarketService(market.Serialize());

            Assert.That(restored.Listings, Has.Count.EqualTo(1));
            Assert.That(restored.Listings[0].artifact.id, Is.EqualTo(artifact.id));
            Assert.That(restored.Listings[0].price, Is.EqualTo(42));
            Assert.That(restored.Cancel(restored.Listings[0].id).id, Is.EqualTo(artifact.id));
        }
    }
}
