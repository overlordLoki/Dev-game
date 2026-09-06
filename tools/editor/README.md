# Ashenveil Level Editor (desktop)

A native window that edits levels straight in the game folder — no export step,
no `build-sprites.py`, no dragging files into `Content/levels/`.

Double-click the start script, or run it from a terminal:

```bash
tools/editor/start-editor.cmd
```

```bash
tools/editor/start-editor.sh
```

Both build first (incremental — a second or two), then open the window. They
work from any working directory, and take `release` to build a Release copy.
The `.sh` needs `chmod +x` once to be double-clickable, and on Linux the window
needs WebKitGTK (`libwebkit2gtk-4.1`) — it warns if that's missing.

Or without the scripts:

```bash
dotnet run --project tools/editor/Ashenveil.Editor.App
```

## Finding the game

Nothing to configure. The editor lives inside the game repo, so on startup it
walks up from wherever the exe is running until it finds the repo root, then
locates the core project inside it. It walks rather than counting levels, so
`bin/Debug/net10.0`, a Release build, a published folder and `dotnet run` all
land on the same answer. The sidebar tags the path `(auto)` when it was found
this way.

The core project's **name is never assumed** — it's whichever immediate
subfolder holds `Content/Sprites` (`Ashenveil.Core` today, `merder.Core`
before the rename). `Content/Sprites` is the marker rather than `Content`
alone because several projects in a MonoGame solution carry a `Content`
folder; only the shared core one holds the art. Renaming the game again costs
nothing here.

Use **Change folder…** to point it at a different checkout — picking anywhere
inside the repo works, it walks up to the root. That choice is saved to
`editor-config.json` next to the exe and wins over auto-detection from then on;
delete that file to go back to finding it automatically.

## Controls

| | |
|---|---|
| Click / drag | paint tiles |
| Click | place object (right-click removes) |
| **Mouse wheel** | rotate the brush |
| **R** / **Shift+R** | rotate the brush, either way |
| **Ctrl+S** | save |

The cursor carries a ghost of whatever is about to be placed, at the rotation
it will land with, so the brush and its rotation are visible without looking
away from the grid. The wheel is captured over the grid, so scroll off the
cells to scroll a grid taller than the window.

## The two parts

| Project | What it is | Knows about |
|---|---|---|
| `Ashenveil.Editor.Core` | The backend. Config, sprite scanning, level read/write, the palette parser, and the request router. | Files and the game's source. **Not** Photino, not HTML. |
| `Ashenveil.Editor.App` | The shell. Opens a Photino window, serves art, carries messages. ~100 lines. | Photino and `EditorApi`. No editor logic. |
| `Ashenveil.Editor.App/ui` | The frontend. Plain HTML/CSS/JS. | The route API only. No .NET. |

The UI never calls .NET directly — it sends a JSON message and gets one back:

```json
{ "id": "7", "method": "PUT", "path": "/levels/Medows", "body": { ... } }
```

`EditorApi.Handle` routes it. That shape is HTTP on purpose: swapping the native
window for an ASP.NET server means mapping real HTTP requests onto the same
method and changing nothing else. `ui/api.js` is the only file that touches
`window.external`.

### Routes

| Route | Does |
|---|---|
| `GET /project` | current game folder + whether it's valid |
| `POST /project/browse` | native folder picker (the one thing the shell must supply) |
| `GET /catalogue` | the tile/object palette, derived from the game's source |
| `GET /sprites` | every PNG under `Content/Sprites`, for browsing art not yet wired up |
| `GET /levels` | level names |
| `GET /levels/{name}` | one level's JSON |
| `PUT /levels/{name}` | save (validated, written atomically via temp-file + move) |
| `DELETE /levels/{name}` | delete |

## The palette builds itself

The editor holds **no list of what tiles or objects exist**. `GameCatalogue`
reads that out of the game, chaining three files:

```
MapLoader.ParseTile    "grass"          ->  TileType.Grass
TileMap._assetNames    TileType.Grass   ->  Assets.Grass
Assets.cs              Assets.Grass     ->  "Sprites/Tiles/grass"
```

Tile vs object comes from the `//Tiles` and `//Objects` comments in `Assets.cs`.
Object varieties come from the constant names — `Tree_Small` / `Tree_Medium` /
`Tree_Large` become one `tree` type with varieties 1, 2, 3, in declaration
order, which is the order `MapLoader`'s own variety switch uses when it picks
between `TreeSmall`, `TreeMedium` and `TreeLarge`. **Keep those two in the same
order** — nothing enforces it, and swapping one would silently place the wrong
tree.

So **adding a tile to the game makes it appear in the editor**, with its real
art, with no editor change at all.

The catch: this is regex over source, not a compiler. It's written to be
forgiving — anything it can't line up becomes a warning in the sidebar rather
than an exception — but if you reformat `Assets.cs` heavily (e.g. rename the
section comments, or move the tile map out of `TileMap`), check the warnings
panel. Everything it parses is in `GameCatalogue.cs`, at the top.

## Art

Sprites are served live off disk through a `sprite://` scheme handler, so the
2MB of PNGs never get base64'd into the page and changed art shows up on a
window reload. Requests that resolve outside `Content/Sprites` are refused.

The old browser editor and its build step live in `tools/old/`
(`level-editor.html`, `build-sprites.py`, `sprites.js`). Nothing here uses
them — they're kept only for reference.

## Level JSON

Unchanged; this is the contract with `MapLoader`:

```json
{
  "cols": 8,
  "rows": 5,
  "tiles":     [ ["grass", "dirt", "..."] ],
  "rotations": [ [0, 90, 0] ],
  "objects":   [ { "type": "tree", "variety": 3, "col": 6, "row": 3, "rotation": 90 } ]
}
```

Levels are saved as the exact JSON the UI produced, not re-serialised through a
C# DTO — so a field the UI adds can't be silently dropped by a backend that
hasn't learned about it yet. `LevelStore` only checks it's valid JSON with
`cols`, `rows` and `tiles`.

## Known gap: object rotation isn't loaded

The editor writes `rotation` on objects, and `Tree` already takes a `rotate`
argument and draws with it — as do `TreeSmall` / `TreeMedium` / `TreeLarge`,
which default it to `0`. The break is in the middle:

- `MapData.ObjectData` has no `rotation` field, so it's dropped at parse time.
- `MapLoader` calls `new TreeSmall(o.col, o.row)` without passing one.

So you can rotate a tree in the editor, save it, and see it upright in game.
Tile rotation goes end-to-end already; object rotation stops here.
