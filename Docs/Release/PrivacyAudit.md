# Privacy implementation audit

September 8, 2026. This is an engineering audit of the current source and exported
simulator project, not a published privacy policy or completed App Privacy form.
No production network capture or signed-archive privacy report has been obtained.

## Observed behavior

| Area | Evidence | Implication |
| --- | --- | --- |
| Local progress | `SaveData`, `RunCheckpoint`, and `DungeonGame.SaveCurrentSlot` write PlayerPrefs. | Character saves, inventory, records, checkpoints, volume, guide state, and cosmetic selection/ownership are local. No game cloud-save implementation was found. |
| Market connection | `MarketController.EnsureOnline` requires a saved online choice or an existing online account; Online Data explains the service before Connect Online. | Fresh installs use local simulated trading until the player connects. Existing accounts keep their connection. Offline fallback does not undo a successful account creation. |
| Market requests | `UgsMarketService` sends balance changes, artifact fields, listing IDs, and operation IDs. | Gameplay/market data leaves the device, associated with a Unity player ID. No email, player-name, external login, or contact fields are requested by game code. |
| Market storage | `Assets/CloudCode/ArtifactMarket.js` stores `DUNGEON_DASH_MARKET/state` as a private Cloud Save custom item. | Player balances, proceeds, listings, owner IDs, and a bounded request history persist on the server. The account/ownership maps have no general expiry. Deletion removes the targeted records and retains a replay barrier, described in `OnlineAccountDeletion.md`. Listings returned to market clients include seller IDs. |
| Purchases | `CosmeticStore` uses Unity IAP and Apple's native entitlement check; game save stores ownership and chosen style. | Apple handles payment. The game does not collect card details. Transaction and entitlement information still flows through the store SDK. Inspect final runtime behavior before completing purchase-data disclosures. |
| Analytics and ads | No UGS Analytics or Ads package in the resolved package set. Their legacy project toggles are off. IAP analytics adapters are conditional on the absent Analytics package. | No custom player-retention or advertising analytics has been installed. This does not establish that every SDK sends no operational telemetry. |
| Diagnostics | `UnityConnectSettings.asset`: `InsightsSettings.m_EngineDiagnosticsEnabled = 1`, `m_Enabled = 0`; legacy crash and performance reporting are off. | The engine capability flag alone does not prove that uploads are active. Verify effective player build settings and a device network trace before asserting diagnostics are absent. |
| Support | No game support form, publisher contact, or hosted privacy URL supplied. | Actual support-provider data handling and retention cannot yet be described. |

## SDK manifests and disclosures

`PackageInventory.json` records 61 resolved packages, available notice hashes,
and the contents/hashes of their privacy manifests. The exported
`UnityFramework/PrivacyInfo.xcprivacy` includes UGS Authentication, Cloud Code,
Services Core, and IAP declarations. The Unity runtime also has a separate
manifest. Keep those vendor files; do not remove declarations merely to obtain
a more attractive privacy label.

The bundled Authentication manifest is broad. Unity documents that email,
other contact information, support content, and usage data depend on features
such as Unity Player Accounts, player names, or an OpenID provider. This game
uses anonymous sign-in. The actual flow still needs player-ID and network-derived
location review. [Unity Authentication privacy documentation](https://docs.unity.com/authentication/privacy-and-consent/apple-privacy-survey).

Cloud Code declares linked user IDs and coarse location for app functionality.
Its separate server-side Cloud Save use must also be covered. Services Core
declares UserDefaults access (`CA92.1`). IAP's supplied manifest declares file
timestamp/disk-space API reasons and no collected data entries; an empty list
is not proof that every optional SDK path or the app itself collects no data.
[Unity Cloud Code privacy documentation](https://docs.unity.com/en-us/cloud-code/apple-privacy-survey),
[Unity service data-safety index](https://docs.unity.com/services/data-safety-requirements).

Apple requires the developer's disclosures to account for integrated partners.
Generate the privacy report from the final Xcode archive and reconcile it with
the implemented features and observed network behavior. A manifest and the
App Store privacy answers serve different purposes; neither replaces the
developer's policy. [Apple privacy details](https://developer.apple.com/app-store/app-privacy-details/),
[Apple SDK requirements](https://developer.apple.com/support/third-party-SDK-requirements/).

## Release work still required

1. Add an easily accessible hosted privacy-policy link in the app and metadata.
   Supply the publisher identity, contact route, service providers, retention,
   deletion practices, and applicable player choices from actual operations.
   [App Review Guidelines, privacy](https://developer.apple.com/app-store/review/guidelines/).
2. Deploy and verify the implemented anonymous-account deletion flow against a
   test backend before shipping. The Online Data screen is accessible from title,
   pause, and market. It requires confirmation and preserves pending requests
   across restart. Local character deletion remains a separate action. Apple's
   requirement also covers automatically created accounts.
   [Apple account-deletion guidance](https://developer.apple.com/support/offering-account-deletion-in-your-app/).
3. Complete the support path for ambiguous Authentication deletion after a lost
   response and invalidated credentials. The client correctly keeps it unconfirmed
   and retains a support ID; it cannot infer account removal from a missing token.
   The server uses a two-hour replay barrier, pruned on a later successful market
   write. Review that actual retention behavior before publishing the policy.
   See `OnlineAccountDeletion.md` for evidence and remaining deployment gates.
4. Decide and implement actual server retention and support-request handling.
   Do not publish promises such as “deleted within 30 days” without an operational
   mechanism. Authentication deletion alone does not prove this custom Cloud
   Save item was cleaned up.
5. Capture clean-install, market-entry, purchase, restore, offline, and deletion
   network behavior on an iPhone. Verify operational telemetry and current Unity
   IAP 5.4.3 data use, then complete the App Privacy form. Do not choose “Data Not
   Collected” based on the lack of an analytics package.

One disposable live Authentication account was created and deleted to verify
error responses. No existing player or market data was touched. The changed
market backend has not been deployed; no privacy policy or App Store privacy
answer has been published or submitted.
