# Dungeon Dash — iPhone release

Direction agreed September 8, 2026: a free iPhone game with optional purchases.
The owner has an Apple Developer account. This is a commercial alpha in development,
not a launched or revenue-validated product.

## Verification snapshot — September 8, 2026

- 83 Unity tests and 7 Node market tests pass. Unity tests ran in an isolated
  source copy because the working project was open in the editor.
- One focused editor run terminated in the native Burst compiler. A retry with
  unchanged compiler settings and the subsequent full test suite passed.
- A standalone Mac QA player survived a forced process termination test: a new
  process restored the full saved character and checkpoint, including remaining
  loot and defeated enemies, and opened paused. This does not replace physical
  iPhone lifecycle testing.
- A Mac player has been built and inspected at 1600×740 and 1024×768 with touch
  controls. Screenshots and a playable preview are under `Builds/MobilePreview`.
  Run `Preview.command` there to launch the Mac app with its touch layout.
- Unity exported an ARM64 iOS simulator project with the IAP native plugin.
  Xcode reached native compilation but failed to compile launch storyboards
  because its matching iOS 26.5 platform was not installed. That 8.52 GB runtime
  download was started; installation and a complete native build remain pending.
- The app has not been run on a physical iPhone, published to TestFlight, or
  tested against Apple sandbox purchases. The registered Bundle ID is still
  needed from the owner. The iOS export is a development artifact, not a
  submission-ready archive.

## The first paid offer

**Guild Collection** is a permanent, non-consumable pack containing three animated
cosmetic auras: Ember Crown, Astral Wisp, and Verdant Soul. Each works with every
hero. Classic is always free. The pack changes no combat stats, loot odds, or
market balances. Purchased ownership is shared across local character slots.

Product ID: `dungeon_dash.guild_cosmetics`.

The Styles screen previews each aura before purchase. Unity IAP 5.4.3 fetches the
localized price from Apple. It does not display a made-up price or simulate a
successful purchase on desktop. The purchase action is disabled until the store
has returned the product. Entitlement checks precede unlocks, unlocks are saved
before transaction confirmation, and Restore Purchases can recover a permanent
purchase after reinstalling. Cancellation and deferred approval do not unlock it.

Suggested initial price to test: US$2.99, configured in App Store Connect. This is
a pricing hypothesis, not a revenue forecast. Do not put a real-money price on
the existing artifact-market currency: gameplay coins and drops originate on
the client, and the offline market includes simulated listings.

Apple documents its purchase and restoration requirements in the
[App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
and [In-App Purchase documentation](https://developer.apple.com/in-app-purchase/).
The implementation uses Unity's
[IAP purchase lifecycle](https://docs.unity.com/en-us/iap/set-up-in-app-purchasing).

## Build and release sequence

1. Set the game's exact registered Bundle ID in Unity's iOS Player Settings.
   It must match the App Store Connect app. The device exporter rejects the
   default-company identifier. Keep signing credentials out of the repository.
2. Create `dungeon_dash.guild_cosmetics` as a **Non-Consumable** in that app.
   Configure its price, availability, localized name and description, and review
   screenshot. Finish Apple's paid-app agreements and business details in the
   developer account if they are not already complete.
3. Run **Tools > Dungeon Dash > Export iPhone Player**. Open the exported
   `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode, select the intended signing team,
   and run on a physical iPhone. The exporter uses IL2CPP, iOS 15+, and landscape
   orientation. The simulator menu exports a separate ARM64 simulator project.
4. Verify the scenarios below using Apple's sandbox and TestFlight. A successful
   Unity export or simulator build does not establish that purchases work.
5. Review the included generated app icon and prepare final screenshots, support page, privacy policy,
   age rating, and accurate App Privacy answers covering the shipped Unity SDKs.
   Confirm the rights and notices for all shipped assets and dependencies.
6. Upload an archive for internal TestFlight review. External TestFlight testing
   and App Store publication are separate steps; neither has been performed.

## Acceptance gates

| Area | Required evidence |
| --- | --- |
| First session | A new tester can choose a hero, move, attack, dash, clear a chamber and use its exit without verbal help. |
| Combat | Testers recognize the orange melee rings and caster aim lines, can dodge them, and understand that walls block shots. Confirm fairness when several enemies attack together. |
| Progression | Best clears, lifetime defeats, and ranks persist across restarts and slot changes. Check whether the next milestone motivates another session; automated persistence tests do not establish retention. |
| Touch | Two fingers can move and aim together; a third can dash; opening menus and interrupting the app never leave an input held. |
| Layout | Small iPhone, notched iPhone, and both landscape rotations have readable text and accessible controls; minimap and controls do not overlap. |
| Interruptions | Lock screen, notification interruption, background/foreground, and interruption during a room transition preserve earned inventory and coins and return safely paused. |
| Store | Test purchase success, cancellation, Ask to Buy/deferred approval, offline launch, disconnect, retry, duplicate delivery, kill before confirmation, reinstall/restore, and refund/revocation. No paid aura unlocks from a failed or deferred purchase. |
| Performance | Profile a 20-minute session on the oldest supported physical iPhone. Choose a stable 60 or 30 fps target from measured frame time, heat, and battery use. The current 60 fps request is not a measured guarantee. |
| Persistence | Run checkpoints now restore the chamber, health, remaining enemies and loot, and unlocked exit. Automated tests cover reconstruction, reward duplication, defeat, abandonment, and interruption between rooms. Verify background/termination recovery on a physical iPhone. |
| Purchase safety | Previewing and equipping styles must never alter health, damage, currencies, artifact ownership, or free hero availability. |

## Product work before broad release

The first-run guide now advances through actual movement, attacking, dashing,
clearing a chamber, and using the exit; players can hide it. The included default
level library enables four themes and nine room templates. These improvements
still need player validation. Melee enemies now telegraph their strikes; casters
introduced from chamber two aim before firing dodgeable bolts. Walls stop enemy
and player projectiles. Each character records their best cleared chamber and
lifetime defeats, earning SCOUT, WARDEN, and CHAMPION ranks at clears 3, 6, and 10.
The hub and run summary show the next milestone. Validate that these goals and
encounters remain engaging across repeated sessions before expanding content.
A purchase screen alone does not make the game commercially compelling.

Run checkpoints save every five seconds during combat and on damage, rewards,
room changes, suspension, and quit. Continuing reconstructs a checkpoint and
waits paused. Inventory and run state share the same save; consumed pickups are
excluded before reward persistence. Transient attacks and cooldowns restart.
An abrupt kill can lose actions after the most recent checkpoint. Saves remain
local to the device. Increment `RunCheckpoint.CurrentVersion` when changing
chamber generation or incompatible combat data; incompatible runs return to the
hub while retaining their inventory.

Start with a small TestFlight cohort and record, with consent, first-chamber
completion, sessions played, reasons for quitting, crashes, store views, purchase
attempts, and completed purchases. Compare results across versions. Do not buy
ads until returning players and purchase behavior justify an acquisition test.
No retention telemetry or paid acquisition campaign has been installed or started.

Simple planning equation: gross sales = active players × buyer fraction × price.
For illustration only, 1,000 players × 2% × US$2.99 = US$59.80 gross, before
store fees, taxes, refunds, and operating costs. These inputs are assumptions,
not expected results. Model net receipts using the actual agreement in the
owner's Apple account.

## Asset provenance

`Tools/import_godot_assets.py` identifies the source art as 0x72's Dungeon Tileset II.
The [author's asset page](https://0x72.itch.io/dungeontileset-ii) lists the asset
license as CC0. That supports use of the original pack; it does not establish the
provenance of every later addition or third-party extension.

VT323 and Press Start 2P license notices are included in `Assets/Resources/Fonts`.
Procedural audio and ground-tile generators live in `Tools`. The new cosmetic
auras reuse the project's code-generated glow sprite and introduce no downloaded
art. Complete the asset/dependency inventory before submission.

The new app icon is a generated pixel-art knight illustration. Its source and full
generation prompt are recorded in [Docs/AppIcon.md](Docs/AppIcon.md). The build
tools assign it to the platform icon slots and set the iPhone display name to Dungeon Dash.
