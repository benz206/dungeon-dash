# Dungeon Dash

A compact Unity 6 PC arena roguelite inspired by the immediate, clean combat of
*Soul Knight*. Choose a hero, survive escalating waves, collect uniquely rolled
weapon artifacts, and trade them through a shared Unity Gaming Services market.

## Play

Open the project with Unity `6000.3.2f1`. Before the first run, choose
`Tools > Dungeon Dash > Generate Everything` if `Assets/Resources/GameCatalog.asset`
is not present, then open `Assets/Scenes/SampleScene.unity` and press Play.

- `WASD`: move
- Mouse: aim and fire
- `Space`: fire toward the cursor
- `I`: artifact inventory
- `M`: artifact market
- `Esc`: close a menu

All six hero classes and all eleven imported appearance variants are playable.
Every imported gameplay sprite is catalogued and verified by an edit-mode test.
All imported weapon sprites participate in a
round-robin drop pool so none can be starved by random selection, while each
artifact's stats are independently rolled. Quality uses `random^4`, making the
high-stat tail progressively less common. Enemy families likewise rotate through
successive waves.

## Global artifact market

Opening the market signs the player into Unity Authentication anonymously and
calls the `ArtifactMarket` Cloud Code endpoint. The server stores the shared
market in private Cloud Save Game Data and uses write locks for atomic list, buy,
cancel, claim, and coin-sync operations. Mutation IDs make retries idempotent, so
a lost response cannot charge a buyer twice. The server rejects duplicate
artifact IDs, unknown weapon sprites, impossible stat combinations, purchases of
one's own listing, and unaffordable purchases.

If UGS is unavailable during the initial connection, the UI explicitly switches
to the persistent local market. It never silently falls back after an in-flight
online mutation because that could duplicate an artifact whose server result is
not yet known.

The default player build connects to the UGS production environment. For a test
environment, launch it with `--ugs-environment=<environment-name>`.

The market is server-authoritative for transactions and ownership after an
artifact first enters the market. Artifact drops and gameplay coin awards remain
client-originated because the game itself is currently single-player and must
remain playable offline. Moving combat simulation to a multiplayer server is the
natural point to make drop issuance fully authoritative.

For deployed-market QA, a standalone build supports two inert-by-default smoke
flags. Run a seller with `--qa-fresh-auth --qa-market-list`, copy the logged
listing ID, then run a buyer with
`--qa-fresh-auth --qa-market-buy=<listing-id>`. A third fresh buyer must fail on
that same ID, proving the listing can only be purchased once. Add
`--ugs-environment=<environment-name>` to all three commands when testing outside
production.

## Tests

Unity edit-mode tests cover every hero starting a real run, combat, complete
sprite-path coverage, artifact bounds and rarity distribution, local market
persistence, UGS fallback, and idempotent client retries. The Node Cloud Code
suite covers server validation, exact catalog parity, balances, ownership,
insufficient funds, purchase races, write-lock retries, and idempotency.

Run them with:

```bash
node --test CloudCodeTests/ArtifactMarket.test.js
/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$(pwd)" -runTests \
  -testPlatform EditMode -testResults /tmp/dungeon-dash-tests.xml
```
