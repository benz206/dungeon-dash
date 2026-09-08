# Dungeon Dash

Dungeon Dash is an arena roguelite built with Unity 6, being prepared for a free
iPhone release with optional cosmetic purchases. Desktop play remains supported. It takes some
inspiration from *Soul Knight*: pick a hero, fight through increasingly busy
waves, and collect weapons with randomly rolled stats. Artifacts can also be
listed on a shared market for other players to buy.

## Running the game

Open the project in Unity `6000.5.3f1`. If
`Assets/Resources/GameCatalog.asset` or `Assets/Resources/LevelLibrary.asset` is
missing, run `Tools > Dungeon Dash > Generate Everything` before starting. That
also packs the sprite atlases, without which every tile costs its own draw call.
Then open `Assets/Scenes/SampleScene.unity` and press Play.

### Mobile play and iPhone exports

On iPhone, use the left pad to move, the right pad to aim and fire, and the Dash
button to evade. An interaction button appears beside usable doors and hub
destinations. Vault, Market, and Pause are available from the HUD. UI respects
screen safe areas and the game runs in landscape orientation.

To preview touch controls in a desktop player, launch it with `--touch-controls`.
The existing QA screenshot flags can be combined with this flag. Use
`--qa-view=cosmetics` to inspect the collection screen.
Use `--qa-tutorial` to show the first-run guide, or `--qa-resume` without
`--qa-character` to restore the last QA checkpoint in a new player process.
Editor play mode, batch tests, and players launched with `--qa-*` flags use a separate save key so
future automated runs do not change normal player progress.

Install iOS Build Support for Unity 6000.5.3f1, then use **Tools > Dungeon Dash >
Export iPhone Player** or **Export iPhone Simulator**. Set the registered iOS
Bundle ID before a device export. The simulator exporter targets Apple Silicon.

App suspension saves a run checkpoint and keeps gameplay paused until the player
resumes. Continue restores the chamber, hero health, remaining enemies, uncollected
loot, and unlocked exit alongside inventory and coins. Combat also checkpoints every
five seconds and after rewards, damage, and room changes. A hard termination resumes
the last checkpoint; transient attacks and cooldowns restart. Defeat or abandoning a
run clears the checkpoint. Checkpoints are local to the device.

New delvers receive a guide that advances through movement, attacking, dashing,
clearing a chamber, and using its exit. HIDE TIPS dismisses it permanently for that
save slot. The included level library enables four themes and nine room templates.

Melee enemies warn with an orange ring before striking. From chamber two, casters
mark an aim line and fire a slower, dodgeable bolt at the hero's earlier position.
Walls block both enemy bolts and player projectiles. Pausing freezes attacks, and
defeating or interrupting an enemy cancels its pending wind-up.

Each delver keeps a best cleared chamber and lifetime defeat count. Clear chambers
3, 6, and 10 to earn SCOUT, WARDEN, and CHAMPION ranks. The hub and run summary show
the next target. Ranks are earned through play and saved with the character.
`--qa-view=encounter` stages the second chamber's attack warnings for visual QA.

The Styles screen, accessible from the title and pause menus, previews the optional
Guild Collection. Purchases use Unity IAP and Apple product
`dungeon_dash.guild_cosmetics`; desktop previews cannot grant purchases. See
[COMMERCIAL_RELEASE.md](COMMERCIAL_RELEASE.md) for configuration, launch gates,
and remaining product work.

### Desktop controls

- `WASD` — move
- Mouse — aim and fire
- `Space` — fire toward the cursor
- `I` — open the artifact inventory
- `M` — open the artifact market
- Right mouse button — dash
- `E` — interact with a nearby destination
- `Esc` — pause or close the current menu

There are six playable hero classes and eleven appearance variants. Weapons
drop from a round-robin pool, so every imported weapon gets a turn instead of
being left entirely to chance. Their stats are still rolled independently, and
high-quality rolls are intentionally rare. Enemy families rotate between waves
in a similar way.

## Chambers and the level builder

Each chamber is assembled at runtime by `ChamberBuilder` from data in
`Assets/Resources/LevelLibrary.asset`:

- **Room templates** give a role (entry, combat, hall, treasure), a shape
  (rectangle, cross, ellipse, pillared, notched), a size range, a minimum depth
  and prop/enemy weights.
- **Chamber themes** rotate every two chambers and set floor wear, grass biomes,
  wall and floor tint, an accent colour the HUD picks up, and a weighted prop
  table.
- **Props** are placed by rule — open floor, against the north wall, in a corner
  or at the room's centre — and solid ones are cut out of the navigation grid.

Rooms are linked with two-wide corridors and the room farthest from the entry
gets the exit doorway, so every chamber is walkable end to end.

`Tools > Dungeon Dash > Level Builder` edits all three asset types in place,
previews the chamber a given depth and seed produces, and sweeps 200 seeds
looking for disconnected floor, a sealed doorway, wall/floor overlap or a
chamber with nowhere to spawn enemies. `Rebuild Default Level Library` discards
hand-authored edits and regenerates the defaults.

## Artifact market

Opening the market signs the player into Unity Authentication anonymously and
connects to the `ArtifactMarket` Cloud Code endpoint. Listings are kept in Cloud
Save, and the server handles listing, buying, cancelling, claiming, and coin
syncing. It also validates artifacts and makes repeated requests safe, so a
retry cannot charge someone twice.

If Unity Gaming Services cannot be reached during the initial connection, the
game switches to a persistent local market and says so in the UI. It does not
switch to local data after an online transaction has already started, since the
server may have completed a request even if the response never reached the
client.

Player builds use the UGS production environment by default. To use another
environment, launch the game with:

```text
--ugs-environment=<environment-name>
```

The market is authoritative for transactions and for artifacts that have
entered it. Drops and gameplay coin rewards still originate on the client. That
keeps the single-player game usable offline; making those rewards fully
authoritative would require moving the combat simulation to a server as well.

### Market smoke test

A standalone build includes a few command-line flags for checking a deployed
market. They do nothing unless passed explicitly.

1. Start a seller with `--qa-fresh-auth --qa-market-list` and copy the listing
   ID from the log.
2. Start a buyer with `--qa-fresh-auth --qa-market-buy=<listing-id>`.
3. Start another fresh buyer with the same listing ID. That purchase should
   fail, confirming that the listing can only be bought once.

Add `--ugs-environment=<environment-name>` to each command when testing outside
production.

## Tests

The Unity edit-mode tests cover hero startup, combat, sprite catalog coverage,
chamber generation (connectivity, doorways, prop and spawn placement,
determinism), enemy navigation, artifact bounds and rarity, local market
persistence, UGS fallback, and client retry behavior. The Node tests cover the Cloud Code side, including validation,
balances, ownership, purchase races, write-lock retries, and idempotency.

Run the Cloud Code tests with:

```bash
node --test CloudCodeTests/ArtifactMarket.test.js
```

Run the Unity edit-mode tests with:

```bash
/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$(pwd)" -runTests \
  -testPlatform EditMode -testResults /tmp/dungeon-dash-tests.xml
```
