# Dungeon Dash

Dungeon Dash is a small PC arena roguelite built with Unity 6. It takes some
inspiration from *Soul Knight*: pick a hero, fight through increasingly busy
waves, and collect weapons with randomly rolled stats. Artifacts can also be
listed on a shared market for other players to buy.

## Running the game

Open the project in Unity `6000.5.3f1`. If
`Assets/Resources/GameCatalog.asset` or `Assets/Resources/LevelLibrary.asset` is
missing, run `Tools > Dungeon Dash > Generate Everything` before starting. That
also packs the sprite atlases, without which every tile costs its own draw call.
Then open `Assets/Scenes/SampleScene.unity` and press Play.

### Controls

- `WASD` — move
- Mouse — aim and fire
- `Space` — fire toward the cursor
- `I` — open the artifact inventory
- `M` — open the artifact market
- `Esc` — close the current menu

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
