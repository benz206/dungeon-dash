# App Store listing draft — English

Prepared September 8, 2026. Copy is based on implemented features; it has not
been submitted. Confirm name availability in App Store Connect. The registered
Bundle ID, publisher name, support URL, and privacy URL remain owner inputs.

## Listing fields

**Name:** Dungeon Dash

**Subtitle:** Dodge. Loot. Descend.

**Promotional text:** Read the warning, dodge the strike, claim the treasure. Choose your delver and push deeper into a pixel-art dungeon. Optional styles add flair to every hero.

**Keywords:** roguelite,dungeon,pixel,action,adventure,loot,knight,magic,arcade,treasure

**Suggested categories:** Games — Action; Role Playing.

**Description:**

One more chamber. One better artifact. One narrow escape.

Choose your delver and fight through changing dungeon chambers in Dungeon Dash.
Move, aim, and dash with touch controls built for landscape play. Watch for
warning rings, step away from a caster's aim line, and use the walls to stop
incoming shots.

MAKE EACH EXPEDITION COUNT
Collect weapons and rare artifacts, build your loadout, and return to the hub
for another attempt. Each character remembers their best cleared chamber and
total enemies defeated. Reach new depths to earn Scout, Warden, and Champion
ranks.

FIND YOUR DELVER
Choose from six hero families with alternate appearances. Keep separate
character saves and continue a saved expedition from its latest checkpoint.

PLAY FREE. ADD YOUR STYLE.
The optional Guild Collection permanently unlocks three animated cosmetic
auras: Ember Crown, Astral Wisp, and Verdant Soul. Preview them before buying.
Every aura works with every hero and changes appearance only.

Dungeon runs work offline. Internet access is needed for the online market,
purchases, and restoring purchases. Progress is stored on your device; deleting
the app can remove your saved expeditions. Restore Purchases restores an eligible
Guild Collection purchase, not your lost gameplay progress.

## Guild Collection product

| Field | Draft value |
| --- | --- |
| Reference name | Guild Collection |
| Product ID | `dungeon_dash.guild_cosmetics` |
| Type | Non-Consumable |
| English display name | Guild Collection |
| English description | Three cosmetic auras for every hero. |
| Price | Configure in App Store Connect; US$2.99 is an untested initial hypothesis. |

The product metadata must match the permanent entitlement the game implements.
Apple limits the localized IAP description to 45 characters and display name to
30. [Apple IAP field reference](https://developer.apple.com/help/app-store-connect/reference/in-app-purchases-and-subscriptions/in-app-purchase-information/).
The app displays Apple's localized price. Do not include a fixed price in the
screenshots or promise family sharing until it is configured and tested.

## Screenshot sequence

Capture the actual iPhone build at an accepted device resolution. Current Mac
QA captures demonstrate layout but are not final iPhone submission screenshots.

1. **DODGE THE WARNING** — A visible melee ring and caster aim line, with enough
   space to see the player and touch controls.
2. **FIND YOUR DELVER** — Hero selection showing the six hero families.
3. **BUILD YOUR LOADOUT** — Inventory with representative earned artifacts.
4. **GO ONE CHAMBER DEEPER** — Run summary showing a legitimately earned record.
5. **MAKE IT YOURS** — The three Guild Collection previews, labeled
   “Optional cosmetic purchase.”

Use the actual build's content. Do not show fabricated player counts, review
scores, rewards, prices, or features. Keep gameplay visible around any headline.

## App Review notes draft

Dungeon Dash is a landscape action game. Core dungeon play does not require a
login. Choose Enter the Dungeon, create a character, and follow the first-run
guide. Move with the left control, aim/attack with the right control, and use
Dash to evade enemy warnings. Clear a chamber and approach its exit to continue.

The Styles button on the title screen opens the optional Guild Collection.
It contains three permanent appearance-only auras. Classic remains free.
Restore Purchases is on the same screen. There are no subscriptions or paid
randomized rewards. Gameplay coins cannot be purchased for money.

Players choose Connect Online before using the anonymous Unity Gaming Services
market. Online Data is accessible from title, pause, and market screens and
contains Delete Account with a separate confirmation. Before submission, verify
that flow against the deployed backend and establish the lost-confirmation support
route. Do not submit while those checks or the privacy-policy link are incomplete.

## Submission checks

The name/subtitle, promotional text, keywords, description, and IAP fields are
within their usual character limits. Confirm against the current form before
upload. Apple documents the fields in its [platform version reference](https://developer.apple.com/help/app-store-connect/reference/app-information/platform-version-information/).

Complete the age-rating questionnaire from the actual content: repeated fantasy
combat, melee weapons, magic, pixel effects, and an optional cosmetic purchase.
Do not guess a numeric age rating, declare Kids Category suitability, or claim
VoiceOver/controller support without testing those features. Complete the
current screenshot, accessibility, regional availability, and privacy forms in
the owner's App Store Connect account.
