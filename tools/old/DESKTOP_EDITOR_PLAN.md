# Level Editor — Desktop App Plan

Parked for later. Goal: turn the current browser-based level editor into a
proper **desktop app** that reads/writes the game's files directly.

## Why move off the browser

The browser version works but has three friction points, all caused by the same
thing — **a web page can't touch the filesystem**:

1. Sprites must be pre-encoded into `sprites.js` via `build-sprites.py` every time
   art changes.
2. Saving a level is Download-then-move-into-`Content/levels/` by hand.
3. No concept of a "project" — it can't remember the game folder.

A desktop app with filesystem access removes all three.


## Chosen direction: native desktop app (not a browser tab)

Preference is a real app window, not a browser page. Best fit that still reuses
the HTML UI:

- **Photino.NET** — a lightweight .NET native web-view wrapper. Runs the existing
  HTML in its own window and exposes filesystem access to the JS. Feels like a
  desktop app; reuses all the HTML/JS. (Electron is the heavier equivalent if
  Photino proves limiting.)

Alternative if the native shell gets fiddly:

- **Thin .NET local server** (ASP.NET minimal API) that serves the HTML and
  answers `fetch()` calls. Opens in the browser at `localhost`, but still reads/
  writes the real game folder. Less "app-like" but the filesystem bridge is
  simpler (plain HTTP endpoints).

Either way the frontend is the same; only the bridge differs.

## Features the app should have

- **Pick the game folder once** ("open project"), remembered in a small config
  file (e.g. `editor-config.json` in the app's own folder). This is the "project".
- **Auto-load sprites on start** — scan `merder.Core/Content/Sprites/**` and hand
  the UI the images live. Kills `build-sprites.py` entirely; add a PNG, it shows up.
- **Save / load levels in place** — write JSON straight into
  `merder.Core/Content/levels/`, and list existing levels in a dropdown to reopen.
- **New level / duplicate level** helpers.


## Current state

- `tools/level-editor.html` — working editor: tile painting (drag), object
  placement, object rotation (R key / button), sprite rendering, JSON export/
  import, grid resize.
- `tools/sprites.js` — generated base64 sprites (will be replaced by live
  `getSprites()`).
- `tools/build-sprites.py` — regenerates `sprites.js` (obsolete once the app reads
  sprites live).
- JSON schema (the contract with the game loader):
  ```json
  {
    "cols": 8, "rows": 5,
    "tiles": [ ["grass", "dirt", ...], ... ],
    "objects": [ { "type": "tree", "variety": 3, "col": 6, "row": 3, "rotation": 90 } ]
  }
  ```
- Game side already loads this via `merder.Core/Levels/MapLoader.cs`
  (`System.Text.Json` -> `TileType[,]` + objects). Tile ids must match
  `MapLoader.ParseTile`.

## Open items / future

- **Tile rotation** — straight dirt road is a *tile*, and tiles are plain strings,
  so roads can't rotate yet. Adding it means changing the tile format from a string
  to `{ id, rotation }` on **both** editor and game (`MapData` + `MapLoader` +
  `TileMap.Draw`). Bigger, coordinated change.
- **Rotation in-game** — the editor writes `rotation`, but `Tree.Draw` doesn't use
  it yet. Needs the rotation/origin overload of `spriteBatch.Draw`.
- **Real sprites in editor for tiles** — already done via data URIs; live loading
  will make this automatic.
- **Multiple object layers / stacking** — currently one object per cell.