using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonDash;
using NUnit.Framework;
using UnityEngine;

namespace DungeonDashTests
{
    public sealed class MarketDeletionTests
    {
        sealed class Gateway : IOnlineMarketGateway
        {
            public string PlayerId { get; set; } = "test-player";
            public bool HasAccount = true;
            public bool FailDelete;
            public bool LostDeleteResponse;
            public bool ConfirmMarketDeletion = true;
            public int Creates, Deletes, Calls;
            public TaskCompletionSource<bool> InitializeGate;
            public TaskCompletionSource<bool> ResumeGate;
            public readonly List<string> Actions = new();

            public async Task InitializeAsync()
            {
                Creates++;
                if (InitializeGate != null) await InitializeGate.Task;
            }

            public async Task<bool> ResumeAccountAsync()
            {
                if (ResumeGate != null) await ResumeGate.Task;
                return HasAccount;
            }

            public Task DeleteAccountAsync()
            {
                Deletes++;
                if (LostDeleteResponse) HasAccount = false;
                return FailDelete || LostDeleteResponse
                    ? Task.FromException(new Exception("response unavailable")) : Task.CompletedTask;
            }

            public Task<OnlineMarketResponse> CallAsync(Dictionary<string, object> arguments)
            {
                Calls++;
                string action = (string)arguments["action"];
                Actions.Add(action);
                return Task.FromResult(new OnlineMarketResponse
                {
                    ok = true, accountDataDeleted = ConfirmMarketDeletion && action == "deleteAccountData",
                    balance = 25, message = string.Empty
                });
            }
        }

        sealed class Host : IMarketHost
        {
            readonly SaveData _save;
            public Host(SaveData save) => _save = save;
            public int Coins { get => _save.slots[0].coins; set => _save.slots[0].coins = value; }
            public void AddArtifact(Artifact artifact) => _save.slots[0].inventory.Add(artifact);
            public bool RemoveArtifact(Artifact artifact) => _save.slots[0].inventory.Remove(artifact);
            public void Notify(string message) { }
            public void PersistSave() => _save.Save();
        }

        static SaveData KnownAccount()
        {
            var save = new SaveData { marketAccountInitialized = true, marketPlayerId = "test-player",
                guildCosmeticsOwned = true, cosmeticStyle = 2, marketPendingCoinDelta = 7 };
            save.CreateSlot("knight").coins = 43;
            save.slots[0].inventory.Add(ArtifactGenerator.Roll("weapon_axe", new System.Random(1)));
            return save;
        }

        [Test]
        public async Task UnconfirmedMarketDeletion_NeverDeletesAuthenticationAccount()
        {
            var save = KnownAccount();
            var gateway = new Gateway { ConfirmMarketDeletion = false };
            Assert.That(await new UgsMarketService(gateway).DeleteAccountAsync(save), Is.False);
            Assert.That(gateway.Deletes, Is.Zero);
            Assert.That(SaveData.Load().marketDeletionPending, Is.True);
            Assert.That(save.marketDataDeleted, Is.False);
        }

        [Test]
        public async Task InterruptedDeletion_ResumesAfterReloadAndPreservesLocalProgressAndPurchase()
        {
            var save = KnownAccount();
            string character = JsonUtility.ToJson(save.slots[0]);
            var failed = new Gateway { FailDelete = true };
            Assert.That(await new UgsMarketService(failed).DeleteAccountAsync(save), Is.False);
            var reloaded = SaveData.Load();
            Assert.That(reloaded.marketDeletionPending && reloaded.marketDataDeleted, Is.True);
            var retry = new Gateway();
            Assert.That(await new UgsMarketService(retry).DeleteAccountAsync(reloaded), Is.True);
            var completed = SaveData.Load();
            Assert.That(completed.marketDeletionPending || completed.marketAccountInitialized || completed.marketOnlineEnabled, Is.False);
            Assert.That(completed.marketPlayerId, Is.Empty);
            Assert.That(completed.marketPendingCoinDelta, Is.Zero);
            Assert.That(completed.guildCosmeticsOwned, Is.True);
            Assert.That(completed.cosmeticStyle, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(completed.slots[0]), Is.EqualTo(character));
            Assert.That(retry.Actions, Is.EqualTo(new[] { "deleteAccountData" }));
            Assert.That(retry.Deletes, Is.EqualTo(1));
            Assert.That(retry.Creates, Is.Zero);
        }

        [Test]
        public async Task MissingCredentialsForKnownAccount_RemainUnconfirmedWithoutCreatingAccount()
        {
            var save = KnownAccount();
            var gateway = new Gateway { HasAccount = false };
            Assert.That(await new UgsMarketService(gateway).DeleteAccountAsync(save), Is.False);
            Assert.That(save.marketDeletionPending, Is.True);
            Assert.That(gateway.Creates + gateway.Calls + gateway.Deletes, Is.Zero);
            Assert.That(save.marketPlayerId, Is.EqualTo("test-player"));
        }

        [Test]
        public async Task LostAuthenticationDeletionResponse_DoesNotMistakeMissingSessionForProof()
        {
            var save = KnownAccount();
            var gateway = new Gateway { LostDeleteResponse = true };
            Assert.That(await new UgsMarketService(gateway).DeleteAccountAsync(save), Is.False);
            var loaded = SaveData.Load();
            Assert.That(await new UgsMarketService(gateway).DeleteAccountAsync(loaded), Is.False);
            Assert.That(loaded.marketDeletionPending && loaded.marketDataDeleted, Is.True);
            Assert.That(loaded.marketPlayerId, Is.EqualTo("test-player"));
            Assert.That(gateway.Deletes, Is.EqualTo(1));
        }

        [Test]
        public async Task NoSavedAccount_DoesNotCreateOneToDeleteIt()
        {
            var gateway = new Gateway { HasAccount = false };
            var save = new SaveData();
            Assert.That(await new UgsMarketService(gateway).DeleteAccountAsync(save), Is.True);
            Assert.That(gateway.Creates + gateway.Calls + gateway.Deletes, Is.Zero);
            Assert.That(save.marketDeletionPending, Is.False);
        }

        [Test]
        public async Task DifferentSignedInAccount_IsNotDeleted()
        {
            var gateway = new Gateway { PlayerId = "other-player" };
            var save = KnownAccount();
            Assert.That(await new UgsMarketService(gateway).DeleteAccountAsync(save), Is.False);
            Assert.That(gateway.Calls + gateway.Deletes, Is.Zero);
            Assert.That(save.marketPlayerId, Is.EqualTo("test-player"));
        }

        [Test]
        public async Task PendingState_IsSavedBeforeNetworkAndSerializesDeletion()
        {
            var gateway = new Gateway { ResumeGate = new TaskCompletionSource<bool>() };
            var service = new UgsMarketService(gateway);
            var save = KnownAccount();
            var operation = service.DeleteAccountAsync(save);
            Assert.That(SaveData.Load().marketDeletionPending, Is.True);
            Assert.That(service.Busy, Is.True);
            Assert.That(await service.DeleteAccountAsync(save), Is.False);
            Assert.That(await service.ConnectAsync(25, 0), Is.False);
            gateway.ResumeGate.SetResult(true);
            Assert.That(await operation, Is.True);
            Assert.That(gateway.Creates, Is.Zero);
        }

        [Test]
        public void FreshLocalMarket_DoesNotInitializeOnlineServices()
        {
            var save = new SaveData();
            save.CreateSlot("knight");
            var artifact = ArtifactGenerator.Roll("weapon_axe", new System.Random(1));
            save.slots[0].inventory.Add(artifact);
            var gateway = new Gateway();
            var local = new LocalMarketService(null);
            var controller = new MarketController(new Host(save), save, local, new UgsMarketService(gateway));
            controller.Open();
            controller.List(artifact);
            Assert.That(gateway.Creates + gateway.Calls, Is.Zero);
            Assert.That(local.Listings.Count, Is.EqualTo(1));
            Assert.That(save.slots[0].inventory, Is.Empty);
            Assert.That(controller.Status, Does.Contain("simulated"));
        }

        [Test]
        public void PendingDeletion_BlocksTradingAndReconnectAfterReload()
        {
            var save = KnownAccount();
            save.marketDeletionPending = true;
            save.Save();
            save = SaveData.Load();
            var gateway = new Gateway();
            var controller = new MarketController(new Host(save), save, new LocalMarketService(null), new UgsMarketService(gateway));
            var item = save.slots[0].inventory[0];
            controller.Open(); controller.EnableOnline(); controller.List(item);
            controller.Buy(new MarketListing()); controller.Cancel(new MarketListing());
            controller.Refresh(); controller.Claim(); controller.TrackCoinDelta(10);
            Assert.That(gateway.Creates + gateway.Calls, Is.Zero);
            Assert.That(save.slots[0].inventory.Count, Is.EqualTo(1));
            Assert.That(save.slots[0].coins, Is.EqualTo(43));
            Assert.That(save.marketPendingCoinDelta, Is.EqualTo(7));
        }

        [Test]
        public async Task ActiveMarketConnection_PreventsDeletionUntilLocalUpdatesFinish()
        {
            var save = KnownAccount();
            var gateway = new Gateway { InitializeGate = new TaskCompletionSource<bool>() };
            var controller = new MarketController(new Host(save), save, new LocalMarketService(null), new UgsMarketService(gateway));
            controller.Open();
            Assert.That(controller.Busy, Is.True);
            Assert.That(await controller.DeleteOnlineAccountAsync(), Is.False);
            Assert.That(save.marketDeletionPending, Is.False);
            gateway.InitializeGate.SetResult(true);
            for (int i = 0; i < 10 && controller.Busy; i++) await Task.Yield();
            Assert.That(controller.Busy, Is.False);
            Assert.That(gateway.Deletes, Is.Zero);
        }
    }
}
