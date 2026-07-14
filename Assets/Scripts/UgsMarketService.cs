using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

namespace DungeonDash
{
    [Serializable]
    public sealed class OnlineMarketResponse
    {
        public bool ok;
        public string message;
        public List<MarketListing> listings = new();
        public int balance;
        public int pendingCoins;
        public Artifact artifact;
    }

    public interface IOnlineMarketGateway
    {
        string PlayerId { get; }
        Task InitializeAsync();
        Task<OnlineMarketResponse> CallAsync(Dictionary<string, object> arguments);
    }

    sealed class UgsMarketGateway : IOnlineMarketGateway
    {
        public string PlayerId => AuthenticationService.Instance.PlayerId;

        public async Task InitializeAsync()
        {
            var options = new InitializationOptions();
            const string environmentArgument = "--ugs-environment=";
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(environmentArgument, StringComparison.Ordinal)) continue;
                options.SetEnvironmentName(argument.Substring(environmentArgument.Length));
                break;
            }
            await UnityServices.InitializeAsync(options);
            if (Array.Exists(Environment.GetCommandLineArgs(), x => x == "--qa-fresh-auth"))
                AuthenticationService.Instance.SignOut(true);
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public Task<OnlineMarketResponse> CallAsync(Dictionary<string, object> arguments) =>
            CloudCodeService.Instance.CallEndpointAsync<OnlineMarketResponse>("ArtifactMarket", arguments);
    }

    public sealed class UgsMarketService
    {
        readonly IOnlineMarketGateway _gateway;
        readonly List<MarketListing> _listings = new();

        public UgsMarketService(IOnlineMarketGateway gateway = null) =>
            _gateway = gateway ?? new UgsMarketGateway();

        public IReadOnlyList<MarketListing> Listings => _listings;
        public bool IsOnline { get; private set; }
        public bool Busy { get; private set; }
        public int Balance { get; private set; }
        public int PendingCoins { get; private set; }
        public string Status { get; private set; } = "Online market not connected";
        public string PlayerId => IsOnline ? _gateway.PlayerId : string.Empty;

        public async Task<bool> ConnectAsync(int initialBalance, int pendingCoinDelta)
        {
            if (IsOnline) return true;
            Busy = true;
            Status = "Connecting to Unity Gaming Services...";
            try
            {
                await _gateway.InitializeAsync();
                Apply(await CallMutationAsync("connect", new Dictionary<string, object>
                {
                    ["initialBalance"] = initialBalance
                }));
                if (pendingCoinDelta != 0)
                    Apply(await CallMutationAsync("syncCoins", new Dictionary<string, object>
                    {
                        ["amount"] = pendingCoinDelta
                    }));
                IsOnline = true;
                Status = "Online · shared UGS market";
                return true;
            }
            catch (Exception exception)
            {
                IsOnline = false;
                Status = $"Offline local market · {ShortMessage(exception)}";
                return false;
            }
            finally
            {
                Busy = false;
            }
        }

        public Task<OnlineMarketResponse> RefreshAsync() => RunAsync("refresh");

        public Task<OnlineMarketResponse> ListAsync(Artifact artifact, int price) =>
            RunAsync("list", new Dictionary<string, object>
            {
                ["artifact"] = ArtifactArguments(artifact),
                ["price"] = price
            });

        public Task<OnlineMarketResponse> BuyAsync(string listingId) =>
            RunAsync("buy", new Dictionary<string, object> { ["listingId"] = listingId });

        public Task<OnlineMarketResponse> CancelAsync(string listingId) =>
            RunAsync("cancel", new Dictionary<string, object> { ["listingId"] = listingId });

        public Task<OnlineMarketResponse> ClaimAsync() => RunAsync("claim");

        public Task<OnlineMarketResponse> SyncCoinsAsync(int amount) =>
            RunAsync("syncCoins", new Dictionary<string, object> { ["amount"] = amount });

        async Task<OnlineMarketResponse> RunAsync(string action, Dictionary<string, object> arguments = null)
        {
            if (!IsOnline) throw new InvalidOperationException("Online market is not connected.");
            Busy = true;
            Status = $"Online · {action}...";
            try
            {
                OnlineMarketResponse response;
                if (action == "refresh")
                {
                    arguments ??= new Dictionary<string, object>();
                    arguments["action"] = action;
                    response = await _gateway.CallAsync(arguments);
                }
                else response = await CallMutationAsync(action, arguments);
                Apply(response);
                Status = "Online · shared UGS market";
                return response;
            }
            catch (Exception exception)
            {
                Status = $"Online request failed · {ShortMessage(exception)}";
                throw;
            }
            finally
            {
                Busy = false;
            }
        }

        async Task<OnlineMarketResponse> CallMutationAsync(string action, Dictionary<string, object> arguments)
        {
            arguments ??= new Dictionary<string, object>();
            arguments["action"] = action;
            arguments["requestId"] = Guid.NewGuid().ToString("N");
            try
            {
                return await _gateway.CallAsync(arguments);
            }
            catch
            {
                // The request may have committed before the response was lost. Retrying once
                // with the same ID is safe because Cloud Code records completed mutations.
                return await _gateway.CallAsync(arguments);
            }
        }

        void Apply(OnlineMarketResponse response)
        {
            if (response == null || !response.ok) throw new InvalidOperationException("The market returned an invalid response.");
            _listings.Clear();
            if (response.listings != null) _listings.AddRange(response.listings);
            Balance = response.balance;
            PendingCoins = response.pendingCoins;
        }

        static Dictionary<string, object> ArtifactArguments(Artifact artifact) => new()
        {
            ["id"] = artifact.id,
            ["weaponId"] = artifact.weaponId,
            ["displayName"] = artifact.displayName,
            ["damage"] = artifact.damage,
            ["attacksPerSecond"] = artifact.attacksPerSecond,
            ["criticalChance"] = artifact.criticalChance,
            ["quality"] = artifact.quality,
            ["rarity"] = artifact.rarity
        };

        static string ShortMessage(Exception exception)
        {
            string message = exception.GetBaseException().Message;
            return message.Length <= 90 ? message : message.Substring(0, 90) + "...";
        }
    }
}
