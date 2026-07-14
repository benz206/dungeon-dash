using System;
using System.Linq;
using UnityEngine;

namespace DungeonDash
{
    // Standalone-only smoke hook for proving that separate authenticated players
    // see and mutate the same deployed market. It is inert in normal game runs.
    public sealed class MarketQaRunner : MonoBehaviour
    {
        const string BuyArgument = "--qa-market-buy=";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!arguments.Contains("--qa-market-list") &&
                !arguments.Any(x => x.StartsWith(BuyArgument, StringComparison.Ordinal))) return;
            new GameObject("Market QA Runner").AddComponent<MarketQaRunner>();
        }

        async void Start()
        {
            try
            {
                var market = new UgsMarketService();
                if (!await market.ConnectAsync(100, 0)) throw new Exception(market.Status);

                string buyArgument = Environment.GetCommandLineArgs()
                    .FirstOrDefault(x => x.StartsWith(BuyArgument, StringComparison.Ordinal));
                if (buyArgument == null)
                {
                    var artifact = ArtifactGenerator.Roll("weapon_axe", new System.Random(2026));
                    await market.ListAsync(artifact, 10);
                    var listing = market.Listings.Single(x => x.artifact.id == artifact.id);
                    Debug.Log($"[DungeonDash][MarketQA] LISTED player={market.PlayerId} listing={listing.id} artifact={artifact.id}");
                }
                else
                {
                    string listingId = buyArgument.Substring(BuyArgument.Length);
                    var response = await market.BuyAsync(listingId);
                    if (response.artifact == null) throw new Exception("Purchase returned no artifact.");
                    Debug.Log($"[DungeonDash][MarketQA] BOUGHT player={market.PlayerId} listing={listingId} artifact={response.artifact.id}");
                }
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[DungeonDash][MarketQA] FAILED " + exception.GetBaseException().Message);
                Application.Quit(2);
            }
        }
    }
}
