using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonDash;
using NUnit.Framework;

namespace DungeonDashTests
{
    public sealed class UgsMarketServiceTests
    {
        sealed class FakeGateway : IOnlineMarketGateway
        {
            public string PlayerId => "test-player";
            public bool FailInitialize;
            public bool FailNextCall;
            public int Calls;
            public readonly Queue<OnlineMarketResponse> Responses = new();
            public readonly List<string> RequestIds = new();

            public Task InitializeAsync() => FailInitialize
                ? Task.FromException(new Exception("no network"))
                : Task.CompletedTask;

            public Task<OnlineMarketResponse> CallAsync(Dictionary<string, object> arguments)
            {
                Calls++;
                if (arguments.TryGetValue("requestId", out var requestId)) RequestIds.Add((string)requestId);
                if (FailNextCall)
                {
                    FailNextCall = false;
                    return Task.FromException<OnlineMarketResponse>(new Exception("response lost"));
                }
                if (Responses.Count == 0) return Task.FromException<OnlineMarketResponse>(new Exception("response lost"));
                return Task.FromResult(Responses.Dequeue());
            }
        }

        static OnlineMarketResponse Response(int balance) => new()
        {
            ok = true,
            balance = balance,
            listings = new List<MarketListing>()
        };

        [Test]
        public async Task Connect_WhenUgsIsUnavailable_ExplicitlySelectsOfflineStatus()
        {
            var gateway = new FakeGateway { FailInitialize = true };
            var service = new UgsMarketService(gateway);

            Assert.That(await service.ConnectAsync(25, 0), Is.False);
            Assert.That(service.IsOnline, Is.False);
            Assert.That(service.Status, Does.StartWith("Offline local market"));
        }

        [Test]
        public async Task Connect_SynchronizesPendingGameplayCoins()
        {
            var gateway = new FakeGateway();
            gateway.Responses.Enqueue(Response(25));
            gateway.Responses.Enqueue(Response(32));
            var service = new UgsMarketService(gateway);

            Assert.That(await service.ConnectAsync(25, 7), Is.True);
            Assert.That(service.Balance, Is.EqualTo(32));
            Assert.That(gateway.Calls, Is.EqualTo(2));
        }

        [Test]
        public async Task Mutation_RetriesWithTheSameIdAfterLostResponse()
        {
            var gateway = new FakeGateway();
            gateway.Responses.Enqueue(Response(25));
            var service = new UgsMarketService(gateway);
            Assert.That(await service.ConnectAsync(25, 0), Is.True);

            gateway.FailNextCall = true;
            gateway.Responses.Enqueue(Response(12));
            await service.ClaimAsync();

            Assert.That(gateway.RequestIds[^1], Is.EqualTo(gateway.RequestIds[^2]));
            Assert.That(service.Balance, Is.EqualTo(12));
        }
    }
}
