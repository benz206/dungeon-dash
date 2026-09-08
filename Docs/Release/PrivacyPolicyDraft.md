# Dungeon Dash privacy policy — unpublished working draft

**Do not publish this draft yet.** The publisher/contact fields, diagnostic SDK
behavior, server retention, backend deletion verification, and deletion support route below are unresolved. The
engineering evidence is in `PrivacyAudit.md`. Replace internal completion notes
with verified operating practices before publication.

## Who operates the game

Dungeon Dash is operated by **[publisher's legal name]**. Contact us about privacy
or support at **[public contact address or support URL]**. This policy takes effect
on **[publication date]** and covers the Dungeon Dash iPhone game.

## Progress stored on your device

The game stores your character slots, inventory, coins, best clears, defeated-enemy
totals, saved expeditions, tutorial progress, volume, and cosmetic preferences on
your device. The game also remembers whether the Guild Collection is unlocked.
These gameplay saves do not have an implemented cloud-backup or cross-device
sync feature. Removing the app can remove them.

## The online artifact market

Choosing Connect Online in the Online Data screen connects to Unity Gaming
Services. The game creates or signs into an anonymous account and uses its player identifier to
associate your market activity with that account. It sends artifact details,
listings, gameplay-coin balances and changes, and identifiers used to process
transactions reliably. Other market users receive the listings and their seller
identifiers. The game does not ask you for an email, real name, or social login.

Unity processes information needed to deliver its online services, including
player identifiers and network information. Its supplied Cloud Code manifest
declares coarse location and user IDs for app functionality. Market records are
stored using Unity Cloud Save through the game's backend.

**Complete before publication:** verify the deployed account-deletion process and
network data, and state the actual retention schedule. The implemented backend
removes market records but retains a two-hour replay barrier until a later
successful market write. Local character deletion does not delete the separate
server account or its market data.

## Optional purchases

Apple processes the optional Guild Collection purchase. The game uses Unity
In-App Purchasing to communicate with the store, check ownership, and restore
eligible purchases. The game does not receive your payment-card details.
Restoring a purchase restores the eligible cosmetic entitlement, not deleted
gameplay saves. Apple also maintains its purchase records under its own policies.

**Complete before publication:** reconcile the final StoreKit/Unity SDK data
flows with `PrivacyAudit.md`, the archive privacy report, and current vendor
documentation. Do not claim that purchases transmit no data.

## Diagnostics and support

The game has no installed advertising service or custom player-behavior analytics.
**Complete before publication:** verify the effective Unity diagnostics and SDK
operational telemetry settings. Describe what information the chosen support
provider receives, the reason for keeping it, and the actual retention period.

## Your choices and requests

You can play dungeon runs offline and choose whether to buy cosmetic styles.
You can delete an individual character from the character-slot screen after
confirming the deletion. That is distinct from deleting online-market data.

**Complete before publication:** verify the Online Data > Delete Account flow against the
deployed backend and provide its instructions, the contact route for access/deletion requests, and applicable
player rights for the countries where the game will be available. Describe any
data retained for a defined reason and duration. Do not promise an unimplemented
deletion deadline or advertise the game as child-directed without reviewing the
actual audience and service requirements.

## Service-provider information

The game uses Apple and Unity services. Their information is available in
[Apple's privacy policy](https://www.apple.com/legal/privacy/) and
[Unity's privacy policy](https://unity.com/legal/privacy-policy).
Those policies supplement this game's policy; they do not replace a description
of the publisher's own practices. Add the support/website provider once chosen.

## Changes

We will update this policy when the game's data practices change and show the
effective date above. **Complete before publication:** determine how material
changes will be communicated to existing players.
