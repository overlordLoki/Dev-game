# The Ashenveil Murders

A top-down 2D game built with **MonoGame** (.NET) — a learning project exploring
the fundamentals: the game loop, sprites and animation, tile maps, a screen/menu
system, entity and object collision, and depth sorting.

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/) (net10.0)
- MonoGame is pulled in via NuGet (`MonoGame.Framework.DesktopGL` +
  `MonoGame.Content.Builder.Task`) — no separate install needed.

## Running

The desktop build is the main target:

```bash
cd Ashenveil.DesktopGL
dotnet run
```

Or open `Ashenveil.sln` in Visual Studio / VS Code, set **Ashenveil.DesktopGL** as the
startup project, and press **F5** (with debugger) or **Ctrl+F5** (without).

### Controls

| Input | Action |
|-------|--------|
| Arrow keys | Move the player |
| P | Pause / resume |
| Esc | Quit |
| Mouse | Menu / settings buttons |

## Project layout

This is a standard MonoGame cross-platform solution — shared game code lives in
`Ashenveil.Core`, and each platform has a thin launcher.

```
Ashenveil.sln
├── Ashenveil.Core/            # all shared game code (edit here)
│   ├── AshenveilGame.cs       # main Game class: the loop, screen manager wiring
│   ├── Settings.cs         # height-based UI scale
│   ├── Layout.cs           # single source of truth for tile-cell size
│   ├── Textures.cs         # shared accessor over ContentManager
│   ├── Entities/           # IEntity, Player, NPC (movement, collision)
│   ├── Objects/            # IObject, Tree (world props)
│   ├── Tiles/              # TileType, TileMap (the ground grid)
│   ├── Levels/             # MapData + MapLoader (JSON level loading)
│   ├── Screens/            # ScreenManager, Menu, Settings, Pause, World, Gamebox
│   │   └── Locations/      # Location (a playable area, built from a level file)
│   ├── Utility/            # IDrawable, Animation, Debug helpers
│   ├── Localization/       # culture/string resources
│   └── Content/            # assets + the MGCB pipeline (Ashenveil.mgcb)
│       ├── Sprites/        # player sheets, tiles, object art
│       └── levels/         # *.json level files
├── Ashenveil.DesktopGL/       # Windows/Mac/Linux launcher (main run target)
├── Ashenveil.Android/         # Android launcher
└── tools/                  # the level editor (see below)
```

## Systems

- **Screen manager** — a stack of `IScreen`s (menu → game → pause overlay). The
  top screen updates; screens draw bottom-to-top so overlays show the game behind.
- **Responsive layout** — tiles stretch to fill the window; the player, NPCs, and
  objects all size off `Layout.CellSize`, so everything scales together on resize.
- **Animation** — sprite sheets sliced into frames by `Animation`, advanced on a timer.
- **Tile maps** — a `TileType[,]` grid drawn from the current window size, with
  optional per-tile rotation.
- **Objects** — grid-placed props (trees) with tight collision boxes.
- **Collision** — AABB push-out; entities collide with walls, each other, and
  objects. Collision boxes are tight (feet/trunk), separate from sprite size.
- **Depth sorting** — player, NPCs, and objects share `IDrawable.SortY` and draw
  in base-Y order, so the player passes behind or in front of trees correctly.

## Levels

Levels are plain JSON in `Ashenveil.Core/Content/levels/`, loaded by
`Levels/MapLoader.cs` (`System.Text.Json`). Format:

```json
{
  "cols": 8,
  "rows": 5,
  "tiles":     [ ["grass", "dirt", ...], ... ],
  "rotations": [ [0, 90, ...], ... ],
  "objects":   [ { "type": "tree", "variety": 3, "col": 6, "row": 3, "rotation": 0 } ]
}
```

Tile ids (`grass`, `dirt`, `dirt_road`, `dirt_cross`) must match
`MapLoader.ParseTile`. `rotations` and object `rotation` are optional.

## Tools

### Level editor — `tools/editor/`

A native desktop app (Photino + .NET) that reads and writes levels **directly**
in `Ashenveil.Core/Content/levels/` — no export step, no copying files around.

```bash
tools/editor/start-editor.cmd
```

```bash
tools/editor/start-editor.sh
```

Either script builds and launches it, from any working directory. There is
nothing to configure: the editor lives in this repo, so it finds the game by
walking up from itself.

Paint tiles, place and rotate objects, save straight into the levels folder.
The palette is **not** hardcoded — it's derived from the game's own source
(`Assets.cs`, `MapLoader.ParseTile`, `TileMap._assetNames`), so adding a tile
to the game makes it appear in the editor, with its real art, with no editor
change. Full details in [`tools/editor/README.md`](tools/editor/README.md).

### Retired — `tools/old/`

Kept for reference only; nothing uses these.

- `level-editor.html` — the previous browser-based editor
- `build-sprites.py` / `sprites.js` — its embedded art and the script that generated them
