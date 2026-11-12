# Roof Color Selector for Kingdoms and Castles

A mod that lets you recolor building roofs and save those colors with your game. It has two parts:

- **Roof color swatches.** Select a building that has a roof, and a row of 8 preset colors appears. Click one to recolor that building's roof.
- **Regions.** Open the **Regions** window, switch on **Paint**, and drag a rectangle over the map. Every roof inside gets collected. Name the group, pick a color, and save it as a region. Saved regions can be reapplied or deleted later.

> **Status: experimental and not yet tested in the game.** The code compiles against the current game's assemblies (Unity 2019.4.41), but it has not been run in the game. See [Known issues](#known-issues).

## Requirements

- Kingdoms and Castles, current version (Unity 2019.4.41 build)
- The game's mod loader. This mod uses only C# source and the game's own APIs. It has no asset bundle and no custom shader.

## Installation

1. Copy the `RoofColorSelector` folder into the game's mods location (see the note below).
2. Start the game and load a save with buildings that have roofs.
3. Select a building. The swatch row appears above the bottom of the screen.
4. Press **Regions** in the top-left corner to open the region window.

> **Note on install location:** I haven't confirmed where the loader picks up local mods. The Steam Workshop loader lists subscribed items only. Check your `KingdomsAndCastles_Data\mods` folder and the mod loader's log at `KingdomsAndCastles_Data\mods\log.txt`. Look for `Compilation success` followed by `Found 1 SceneLoaded entry point(s)` for this mod.

## How it works

The mod is C# source compiled by the game at load time.

- `kcRoofColorMod` is the entry point. The loader calls its `Preload` and `SceneLoaded` methods.
- `kcRoofColorPanel` shows the swatch row when a building with a roof is selected. It checks the selection about four times per second, not every frame.
- `kcRegionPainter` builds the region window and handles the drag rectangle.
- Saves use the game's `LoadSave` API under the keys `RoofColorSelectorV1` and `RoofColorRegionsV1`.

Roof detection is by renderer name: a renderer whose name contains "roof" is treated as the roof. Public types and members use the `kc` prefix to avoid name clashes with other mods.

## Known issues

- **The color may not show.** Recoloring sets the roof material's `_Color`. Game buildings get their look from a shared livery texture, and `_Color` is probably white on those materials, so the tint may do nothing. This is the first thing to check in testing.
- **Colors can be overwritten.** The game can rewrite a building's materials when it refreshes them. The mod reapplies saved colors only when a building is selected or a save loads. A Harmony patch on `UpdateMaterialSelection` would fix this, and it isn't written yet.
- **Regions match on the building's origin point.** A building counts if its base sits inside the rectangle, so a rectangle that only clips a roof can miss it.
- **Clicks and typing may leak through.** Releasing the mouse after a drag may also select a building in the game. Typing a region name may trigger game hotkeys.
- **Houses are included only by name.** Any building whose roof renderer is named with "roof" is included. This has not been checked against the game's house models.

## Roadmap

- Confirm the tint is visible on game buildings. If it isn't, switch to recoloring the roof's part of the livery texture, using per-building copies so other buildings don't change.
- Add the `UpdateMaterialSelection` reapply patch.
- Test region selection, naming, saving, and reloading in a real save.

## Credits and license

Written from scratch for this project. It is not based on the code of the District mod by Slooth, and it does not include that mod's assets. The region and swatch ideas were inspired by District.

License: not chosen yet. Add a `LICENSE` file before publishing.
