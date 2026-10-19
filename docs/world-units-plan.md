# World Units (Positions in Cells) — Plan

## Summary

Entity positions are stored in **pixels**, but the size of a pixel-cell depends on the
window: `Layout.CellWidth = window height / visible rows`. So a position only means
something at the window size it was recorded at. That already causes drift when the window
is resized, and it makes saving unreliable (save at 720p, load at 1080p, everyone is in the
wrong place).

The fix: **the game works in cells; only drawing works in pixels.** `Position` becomes a
position in cells (`5.5, 3.25`), speeds become cells per second, and there is exactly one
place that converts to pixels — when drawing. This must land **before the save system**.

---

## Where things are now

- `Entity.Position` is a pixel `Vector2`. Only **13 places** touch `.Position` directly.
- **Speeds** are pixels per second: player `200f`, knight `100f`.
- **Collision**: `Bounds` and `Boxes` are pixel rectangles, derived from the sprite size
  in cells (`SizeInCells`) and the tight boxes in `bounds.json`.
- **Spawning**: `PointOfInterest.ToPixel()` gives the cell centre in pixels; `World.Spawn`
  and `UseExit` turn cells into pixels.
- **Tasks**: `GoToTask` stores its target in pixels (from `ToPixel()` at enqueue time).
- **Camera** follows the player's pixel position; `WorldBounds` clamps in pixels.
- **Draw order** uses `SortY` (the base's y, in pixels).

---

## Decisions

1. **Unit = one cell.** `Position (5.5, 3.25)` = 5½ cells right, 3¼ cells down from the
   map's top-left. Cells are already the game's natural unit (tiles, POIs, exits), so most
   conversions simply disappear.
2. **One conversion point.** A small `WorldToScreen` / `ScreenToWorld` pair (using
   `Layout.CellSize`) used only by drawing, the camera, and mouse input. Everything else —
   movement, collision, tasks, spawning, saving — stays in cells.
3. **Speeds in cells per second.** At 720p a cell is 144px (`720 / VisibleRows`, with 5
   rows), so today's player (200px/s) is ≈ 1.4 cells/s and the knight (100px/s) ≈ 0.7.
   Note that today's speeds *change with window size* — the player crosses the screen
   faster in a small window. Cells per second fixes that too.
4. **Collision in cells.** `Bounds`/`Boxes` become float rectangles in cells. MonoGame's
   `Rectangle` is integer-only, so this needs a small float rectangle type (or
   `RectangleF`-style struct) — the main new piece of code.
5. **POIs return cells.** `PointOfInterest.ToWorld()` = cell centre in cells
   (`x + 0.5, y + 0.5`). `GoToTask` targets become cell positions, so tasks become
   resolution-independent too (and saveable later).
6. **Resize stops moving things.** Positions don't change on resize; only the draw scale
   does. That's the visible proof the refactor worked.

---

## The z question (open)

"x, y, z" could mean three very different things:

| Meaning | What it is | Status |
|---|---|---|
| **Draw order** | What's drawn in front of what | Already handled by `SortY`. |
| **Height above ground** | Jumping, flying, raised platforms | Knight has a jump animation. Would be a `Z` (or `Height`) on entities, drawn as a y-offset and shadow; collision mostly ignores it. |
| **Floor / layer** | Upstairs/downstairs, bridges | Each level would have layers; entities carry a layer index. Much bigger. |

**Recommendation:** keep positions x/y for this refactor, but use a `Vector2`-based
`WorldPos` type (or just `Vector2` with a clear "in cells" rule) that could gain a height
later without touching movement code. Decide which z you mean before building it.

---

## Steps

Do it in slices, keeping the game playable after each one:

1. **Conversion helpers** — `WorldToScreen` / `ScreenToWorld`, plus tests. Nothing uses them
   yet.
2. **Drawing + camera** — draw entities and objects at `WorldToScreen(Position)`; camera
   follows in cells. At this point positions are still pixels and everything looks the same
   (temporarily convert pixels→cells at the boundary).
3. **Switch `Position` to cells** — entity movement (`Speed` in cells/s), spawning
   (`ToWorld()`), exits, `WorldBounds`.
4. **Collision in cells** — float rectangle type, `Bounds`/`Boxes`, push-out maths. The
   collision tests already exist and are the safety net here; update them to cells.
5. **Tasks** — `GoToTask` targets and arrive radius in cells (radius 4px → ~0.03 cells at 720p).
6. **`SortY` and anything else** still in pixels — grep for leftovers.
7. **Resize check** — move around, resize the window; nothing should shift. Use the debug
   console's `where` to compare positions before/after.

---

## Testing

- Conversion round-trip: `ScreenToWorld(WorldToScreen(p)) == p` at several cell sizes.
- Movement: 1 second at 1.4 cells/s moves 1.4 cells **at any window size** (the existing
  `PlayerTests` become cell-based and gain a "same result at two cell sizes" case).
- Collision tests switched to cells — same scenarios, same expectations.
- `GoToTask` tests in cells.
- By hand: resize mid-game, nothing drifts.

---

## Risks

- **Collision is the fiddly part** — float rectangles and rounding at boundaries. Do it as
  its own slice with tests green before and after.
- **Mouse picking** (if anything uses mouse→world) must go through `ScreenToWorld`.
- **The editor tool** writes cell-based data (tiles, POIs, exits), so it shouldn't need
  changes — worth confirming nothing there stores pixel positions.

## Depends on / feeds into

- Easier with: the debug console (`where`, `tp`).
- Required by: the save system (positions and tasks must be resolution-independent).
