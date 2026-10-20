# ItemMarkers

**1.0.0** - client-side ground item and command essence markers for Risk of Rain 2.

![ItemMarkers gameplay showing a Command essence diamond and an item icon with distance labels](https://raw.githubusercontent.com/Morslord/RiskOfRain2-ItemMarkers/main/img/itemmarkers-gameplay.png)

Ground item icons and Command essence markers with centered distance labels.

## Features

- Ground item icons with a centered distance label below them, without item names.
- Markers remain visible through walls and move to the screen edge when outside the camera view.
- Command essences use a filled diamond (◆) in their tier color, with distance below.
- Lunar Equipment essences use the Equipment color with a thin Lunar-colored outline.
- Catalog colors are used for tier colors, including Hallowed, Hex and Pages.
- One shared limit for item and Command markers: **0–60**, default **60**. **0** hides all markers; the nearest eligible pickups are preferred.
- Separate filters for Temporary and Ethereal items, and for White, Green, Red, Boss, Lunar, Void, Hallowed, Hex, Pages and Equipment. All filters default to enabled.
- Marker Size: **50–200%**, in **5%** steps, default **100%**.
- Hide Within Distance: **0–50 m**, in **1 m** steps, default **5 m**. Item and Command markers closer than this distance are hidden; **0 m** disables proximity hiding. Pickups exactly at the threshold remain visible. Hidden nearby pickups do not consume marker slots.
- Optional Risk of Options menu; all settings also work through the BepInEx configuration file.

The mod only reads pickups and adds local HUD elements. Other players do not need ItemMarkers for its local HUD feature; joining mixed-mod multiplayer sessions still needs in-game validation.

## Installation

Install with r2modman or another Thunderstore-compatible mod manager. **BepInExPack must be installed.**

For manual installation, place `ItemMarkers.dll` in your profile's `BepInEx/plugins/ItemMarkers/` directory.

The configuration file is created on the first launch with the mod enabled:
`BepInEx/config/local.itemmarkers.cfg`.
If this file does not exist yet, settings from the previous `com.morslord.itemmarkers.cfg` are copied automatically when available. The old file is retained, and an existing new file takes precedence.

Optionally install **Rune580's Risk Of Options** to configure all 15 settings in-game. Without it, edit the configuration file. Settings are shared between the menu and the file. The menu API was checked against Risk Of Options **2.8.6**.

## Filters and limitations

Category and variant filters are cumulative. For example, an Ethereal White item requires both White and Ethereal Items to be enabled. Lunar Equipment uses the Equipment filter.

- Pickups are rediscovered and nearest candidates selected every **0.2 seconds**. Visible marker positions update each frame.
- Proximity uses the same player-body distance as the label, with the camera as fallback when no body is available. Hiding is checked each frame; nearest eligible replacements and reappearing markers are selected during the next discovery pass.
- The limit applies per local HUD. There is no fixed discovery cap of 60 pickups.
- Unknown/untiered helper items, Lunar Coins, key/progression objects and Meal items are excluded.
- Only pickers matching the game's Command prefab network asset ID and recognized selection events are marked. Scrappers, printers, other choice interactables, Rebirth/Delusion choices and unsupported replacement Command prefabs are excluded. Ambiguous categories among eligible options are skipped.
- Markers can overlap, especially at screen edges; this first version does not cluster them.
- Sharing mods can leave a pickup present after you collected your personal share. Such an object may remain marked; no personal sharing-state adapter is included.
- Item icons use direct sprites where present and load addressable sprites asynchronously otherwise. Loads are shared across markers and local HUDs, then released on run changes or plugin shutdown. No synchronous asset-loading wait is used.
- HUD mods, split-screen, spectator mode, stage transitions, DLC combinations and large drop counts require further in-game testing.

## Credits

- **Akilorp2D and ItemShareFix contributors** — [ItemShareFix](https://github.com/Akilorp2D/ItemShareFix), the marker reference implementation. The item-tier classification is adapted from its MIT-licensed source.
- **Rune580 and Risk Of Options contributors** — [Risk Of Options](https://github.com/Rune580/RiskOfOptions), the optional in-game settings framework.
- **BepInEx contributors and Risk of Thunder** — the mod framework and game-specific tooling.

See `THIRD_PARTY_NOTICES.md` for the pinned reference and scope of code reuse. ItemMarkers is an independent project and is not an official ItemShareFix release.
