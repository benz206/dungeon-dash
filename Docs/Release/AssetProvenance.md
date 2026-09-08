# Asset and dependency provenance

Audit date: September 8, 2026. `AssetInventory.json` records a SHA-256 hash and
source evidence for every PNG, WAV, and TTF in `Assets`: 398 files total.

| Content | Count | Verification |
| --- | ---: | --- |
| Imported pixel sprites | 370 | Exact byte matches against the original `DungeonDashGodot/assets` source used by `Tools/import_godot_assets.py`. |
| Generated grass/path tiles | 8 | Every RGBA pixel matches the output of `Tools/generate_ground_tiles.py`. |
| Generated app icon | 1 | Source prompt and generation record in `../AppIcon.md`; hash in the inventory. |
| Generated audio clips | 17 | Every file exactly matches `Tools/generate_sfx.py` regenerated into a temporary directory. |
| Font binaries | 2 | VT323 and Press Start 2P; accompanying OFL notices are bundled and shown in Credits. |

The [0x72 author page](https://0x72.itch.io/dungeontileset-ii) lists Dungeon Tileset
II as CC0 and also lists the Doc and Pumpkin character downloads. That page's
links to other creators' extension packs do not grant those creators' rights.
This audit establishes the Unity-to-Godot import chain; it is not a fresh
byte-for-byte comparison with the author's downloadable archive. Keep a copy of
the original author download/license evidence with release records before final
rights sign-off.

Unity-generated animation clips, controllers, tile assets, and sprite atlases
derive from these sprites. The in-game glow, attack rings, projectiles, and UI
chrome are drawn by project code. No additional downloaded combat art was added.

`PackageInventory.json` records all 61 entries in `Packages/packages-lock.json`,
including editor/test dependencies and built-in engine modules. Package presence
does not establish inclusion in the final player. It records exact versions,
notice hashes, and available privacy manifests so the final archive can be
reconciled with the source snapshot.

Available package license/third-party notice texts and both font OFL notices are
preserved in `Assets/StreamingAssets/ThirdPartyNotices.txt`, which Unity copies
into builds. The credits panel is accessible from the title and pause screens
and displays the art credits and full font notices. Engine-provided modules
without separate package notice files remain covered by the applicable Unity
distribution terms; confirm the owner's Unity license eligibility and the final
archive's engine notices before publishing. No claim of ownership over third-party
assets or exclusive rights to the generated icon is made.
