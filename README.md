# Dungeon Dash

A compact Unity 6 PC arena roguelite inspired by the immediate, clean combat of
*Soul Knight*. Choose a hero, survive escalating waves, collect uniquely rolled
weapon artifacts, and trade them through a persistent market simulation.

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

## Market boundary

`IMarketService` separates gameplay from the current `LocalMarketService`. The
local implementation persists listings, purchases, cancellations, and simulated
sales without requiring a network connection. A future authoritative service can
implement the same boundary, but a production global economy will also require
accounts, a hosted database, idempotent transactions, and server-side validation.

## Tests

Unity edit-mode tests cover artifact bounds, rarity distribution, unique IDs,
market purchasing rules, and persistence round trips.
