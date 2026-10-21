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

> **Status: done (steps 1–6).** The game works in cells, and only drawing and the camera use
> pixels. Step 7, the by-hand resize check, is still to do. See "What was built" at the end.

---

## Where things were before the refactor

*(Re-checked after the World split, the exit refactor and the debug console.)*

- `Entity.Position` is a pixel `Vector2` (the sprite's top-left). Outside `Entity` itself,
  about a dozen places touch `.Position` or convert with `Layout.CellWidth/Height/Size`:
  - **Spawning:** `Location.PlaceAt`, `PointOfInterest.ToPixel()`.
  - **Exits:** `Location.ExitTouching`.
  - **Talk range:** `World.Interact` (1.5 cells, in pixels).
  - **Edges:** `WorldBounds.Area` and `Clamp`.
  - **Tasks:** `GoToTask`, plus its targets from `NpcBrain.MakeGoTo` and `NpcRoster`.
  - **Camera:** `GameplayScreen` follows the player.
  - **Console:** `WorldCommands`' `tp` and `where`.
  - **Objects:** `GameObject.Boxes`, `SortY` and `Draw`. These are already cell-based (`Col`/`Row`)
    and only turn into pixels at the end.
  - `Conversation.GetAveragePosition`.
- **Sizes**: `Entity.Height = CellSize * SizeInCells`, so sizes are already "cells × scale"
  underneath. Only the stored position isn't.
- **Speeds** are pixels per second: player `200f`, knight `100f`.
- **Collision**: `Bounds` and `Boxes` are integer pixel `Rectangle`s, from the sprite size and
  the fraction boxes in `bounds.json` (already resolution-independent fractions).
- **Tasks**: `GoToTask` stores its target in pixels (from `ToPixel()` at enqueue time), so a
  queued walk aims at the wrong place after a resize.
- **Draw order** uses `SortY`, the feet's y in pixels.
- **Tests** mostly use `Layout.Update(500)`, which gives 100px cells. So every pixel number in
  the tests is just "cells × 100", and converting them is mechanical.

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

## The z question — settled: positions stay x/y

"z" meant **layers** — things like tree canopies drawing over the player. That's a
**draw-order** question, not a third coordinate, so this refactor keeps positions as x/y
(`Vector2` in cells). Layers are their own small rendering feature: see
[draw-layers-plan.md](../draw-layers-plan.md).

For the record, the other meanings of z, neither needed now:

| Meaning | What it would be |
|---|---|
| **Height above ground** | Jumping/flying: a `Height` drawn as a y-offset plus shadow. |
| **Floors** | Upstairs/downstairs, bridges you walk on: a layer index per entity. Much bigger. |

Keeping position in one clearly-named place ("in cells") means a height could be added later
without touching movement code.

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

## What was built

It was done as one change rather than the seven playable slices: once `Position` changed
type, everything touching it had to change in the same build anyway. The tests were the
safety net. They were converted from pixels to cells (÷100, since they used 100px cells)
with the same scenarios.

- **`Collision/RectF`**: a float rectangle, as a `readonly record struct` so tests can
  compare it.
  - `Left/Right/Top/Bottom/Center`, `IsEmpty`, `Intersects` (touching edges don't count),
    `Intersect`, `Union`, `Inflate`.
  - `Geometry.Union` and `Box.ToRect` use it.
- **`Graphics/Layout`** is the one conversion point:
  - `ToScreen(Vector2)` and `ToScreen(RectF)` → `Rectangle`. Each edge is rounded on its own,
    so rectangles that touch in the world still touch on screen.
  - `ToWorld(Vector2)`.
  - `CellSize` is now only read by drawing code: `Entity.Draw`, `GameObject.Draw`,
    `TileMap.Draw` and the camera follow in `GameplayScreen`.
- **`Entity`:**
  - `Position` is the sprite's top-left in cells.
  - `Width`/`Height` are floats in cells (`Height = SizeInCells`), so the editor's regex on
    `SizeInCells` still works.
  - `Boxes`/`Bounds` are `RectF`; `SortY` is a float in cells.
  - `IEntity`, `IObject` and `IWorldDrawable` changed to match.
- **Speeds:** player 1.4 cells/s, knight 0.7.
- **Objects:** `GameObject.Boxes` sit on the 1x1 square at (`Col`, `Row`), and `SortY = Row + 1`.
- **World edges and spawning:**
  - `WorldBounds.Area` is `(0, 0, Cols, Rows)`.
  - `PointOfInterest.ToPixel()` became `ToWorld()` (the cell centre).
  - `PlaceAt`, `GoToTask` targets and `NpcRoster` all use it.
- **Distances in cells:**
  - `ExitTouching` takes a `RectF`, with a leeway of 0.03 cells (~4px at 720p).
  - The `GoToTask` arrive radius is 0.03.
  - The talk range is `World.TalkRange = 1.5`.
- **World start-up no longer depends on the window:** `World.Init` and spawning don't need
  `Layout`, so the old "Layout must be set first" comments are gone.
- **Console:** `tp` stands the player on `(col + 0.5, row + 0.5)`; `where` prints the feet
  in cells (`feet at (2.50, 2.90)`).
- **Follow-up: points mean "stand your feet here".** `Entity.Feet` is the bottom centre of the
  collision box, and `Entity.StandAt(spot)` moves an entity so its feet are on a spot.
  - `PlaceAt`, `tp` and `GoToTask` all use them.
  - Before this, spawning centred the *sprite* on a point. A padded frame like the knight's
    (1.2 cells, with the body in the bottom 40%) then landed with its feet in the cell below.
  - Walking (`GoToTask`) and spawning now aim at the same point.
  - That point is `PointOfInterest.StandingSpot()`: `(x + 0.5, y + FeetDown)`, with
    `FeetDown = 0.9`. It's near the bottom of the cell, so the body fills its own cell
    instead of poking into the one above.
  - Not exactly 1.0: there, the 0.03 door leeway would reach a door in the row below.
  - `ToWorld()` stays the exact cell centre, for things like path waypoints.
  - `tp` uses `StandingSpot()` too; `tpxy` is exact.
- **Tests:** `Near.Equal` (in `TestSupport`) compares with a tolerance, since 0.9 - 1 isn't
  exactly -0.1 in floats. New tests cover:
  - `RectF` itself;
  - `Layout` round trips, edge rounding, and touching rectangles;
  - the player walking the same number of cells at two window sizes;
  - POIs not moving on resize;
  - an overlap smaller than a pixel;
  - the arrive radius.

**Still by hand (step 7):** walk around, resize the window, and check nothing shifts. `where`
before and after should print the same thing.

## Depends on / feeds into

- Easier with: the debug console (`where`, `tp`).
- Required by: the save system (positions and tasks must be resolution-independent).
