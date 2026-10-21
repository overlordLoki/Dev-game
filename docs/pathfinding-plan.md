# NPC Pathfinding — Plan

## Summary

Today `GoToTask` walks an NPC in a straight line at its target. The first tree, well or
bush in the way stops it: collision pushes it back every frame, it slides along the box
or sticks, and the task never finishes. This plan replaces "head straight at it" with
**A\* on the level's tile grid**: work out which cells are blocked, find a route of cells
around them, and walk that route one waypoint at a time.

A\* (the stub in `GoToTask`) is the right tool here: the levels are small grids already
(Medows is 12x10), objects already sit on cells, and A\* on a grid that size is
microseconds. It lands in three pieces that can each be tested on their own: a **NavGrid**
(where you can walk), a **Pathfinder** (the A\* search), and **path following** in
`GoToTask`. Everything else (smoothing, getting unstuck, debug drawing) is a step after.

---

## Where things are now

- **`GoToTask`** sets `npc.Direction` straight at the target every tick and completes
  within 4px. It aims the **sprite's centre** (`Position + size/2`), the same point
  `Location.PlaceAt` uses. It has an empty private `AStar()` stub.
- **Targets** are POIs: `NpcBrain.MakeGoTo` turns a decision's `go_to` into
  `new GoToTask(poi.ToWorld())`, the POI cell's centre in cells (world units, since the
  world-units refactor).
- **Obstacles** are only **objects** (trees, bushes, the well, castle pieces). Each sits on
  one cell (`Col`, `Row`) and has collision `Boxes` from `bounds.json`, usually covering
  only part of its cell (a trunk, not the canopy). **Tiles never block**: there's no
  walkable flag in the catalogue yet.
- **Collision** (`World.Update`) pushes entities out of object boxes and each other, then
  `WorldBounds` keeps them on the map. Bodies are measured by the **feet** (`Bounds`).
- **`TaskContext`** only has `Owner`, `Blackboard` and `DeltaTime`. A task can't see the
  level, so it can't plan around anything.
- **NPCs don't know which level they're in.** `NpcRoster` keeps them per level, but the
  NPC itself has no `Location`.
- **`TaskState`** is `Inactive / Active / Completed`. There's no way for a task to fail.

---

## Why A\* on the tile grid (and not something else)

| Option | Fits? |
|---|---|
| **Steering only** (walk straight, sidestep when blocked) | Cheap, but gets stuck in any U-shaped gap: it only sees what's directly in front. Good *on top of* a path, not instead of one. |
| **A\* on the tile grid** | ✅ The grid exists, objects are per cell, and maps are tiny. Easy to test and to draw. |
| **Flow field** (one map of "which way to X" from every cell) | Great for many agents heading to one place (a horde). Overkill for one knight walking to the well. Possible later for crowds. |
| **Navmesh** | What big 3D games use. Far more machinery than a 12x10 grid needs. |

The search goes in its **own class**, not in `GoToTask`. The private stub gets deleted.
Other tasks will want routes too (`patrol`, `talk_to`, `follow`, wandering somewhere
sensible), and a pure class can be tested without an NPC.

---

## Step 1 — NavGrid: which cells can you walk on

A plain grid of `bool`: `walkable[row, col]`. It's built once from a `Location`:
- **Start with every cell walkable.**
- **Block a cell** if an object's collision box **covers its middle**: test the box against
  a smaller rectangle in the centre of the cell (say the middle 50%), not the whole cell.
  A tree trunk that only clips a cell's corner then doesn't block the cell next to it.
- **Later, tiles:** add `Walkable` to the tile catalogue (water, walls) and block those
  cells too. Nothing needs it today.

Cells are **resolution-independent**: a box covers the same fraction of a cell at any
window size. So the grid is built once per level and doesn't need rebuilding on resize.
It can live on `Location` as `location.Nav`, built in its constructor or lazily on first
use. Since the world-units refactor, boxes are `RectF`s in cells, so building it needs no
`Layout` at all: in the constructor is fine.

Helpers it should offer, all in cell units:
```csharp
bool IsWalkable(Point cell);               // false off the map
Point CellAt(Vector2 world);               // which cell a point is in: just floor x and y
Vector2 CentreOf(Point cell);              // cell centre: (col + 0.5, row + 0.5)
```

**Tests:** an empty level is all walkable; a level with a tree blocks the tree's cell and
only that cell; cells off the map aren't walkable; `CellAt`/`CentreOf` round-trip.

---

## Step 2 — Pathfinder: the A\* search

A static, pure function, with no NPC, no drawing and no `Layout`:
```csharp
// The cells from start to goal, both included; null if there's no way through.
public static List<Point> FindPath(NavGrid grid, Point start, Point goal)
```

How it works, to learn from:
- **Open set**: a `PriorityQueue<Point, int>` (built into .NET) ordered by
  `f = g + h`. Here `g` is the cost so far and `h` is the guess of the cost left.
- **`cameFrom`**: a dictionary of "I reached this cell from that one". Walk it backwards from
  the goal to get the path, then reverse it.
- **`g` scores**: a dictionary of the best known cost to each cell. Skip a neighbour if
  the new route isn't cheaper.
- **Neighbours: 8 directions.** Straight steps cost 10, diagonal steps cost 14 (√2 x 10,
  so whole numbers). **No corner cutting**: a diagonal step is only allowed if both
  straight cells beside it are walkable, or an NPC would clip the corner of a tree.
- **Heuristic: octile distance**, the exact cost on an empty 8-direction grid. It never
  overestimates, so the path A\* finds is the shortest.
  ```
  dx = |x1-x2|, dy = |y1-y2|  →  h = 10*(dx+dy) - 6*min(dx,dy)
  ```

**Edge cases:**
- **Start equals goal:** the path is just `[start]`.
- **The goal cell is blocked** (a POI placed on a tree): go to the **nearest walkable cell
  next to it** instead of failing. That's what a designer would expect.
- **The start cell is blocked** (the NPC has been pushed half into an object's cell): search
  from it anyway. Only its neighbours need to be walkable.
- **No path:** return `null`. The task decides what that means (see step 4).

**Tests** (the biggest win of this plan; build small grids in code):
- an open grid gives a straight or diagonal line of the right length;
- a wall with one gap: the path goes through the gap;
- a U-shaped pocket: the path goes round, not into it (straight-line steering fails this);
- diagonals don't cut blocked corners;
- the goal is walled off completely → `null`;
- a blocked goal → the path ends next to it;
- start equals goal → one cell.

---

## Step 3 — GoToTask follows the path

- **Plan on the first `Execute`, not in the constructor.** A task can wait in the queue
  behind others, so the NPC's position when it's *created* isn't where it'll start from.
  Keep the path as a list of waypoints in cells: the centre of each cell, except the last,
  which is the exact target.
- **Each tick:** steer at the current waypoint. Within the arrive radius, move on to
  the next. After the last one, `Completed`, as now.
- **Use the feet ✅ done.** `Entity.Feet` (the bottom centre of the collision box) is what
  `GoToTask` steers and what `Location.PlaceAt` / `StandAt` put on a POI. Path following
  should ask "which cell are my feet in" and steer `Feet` to each waypoint. Then the feet,
  which are what collides, go round the trees the path went round.
- **The task needs the grid.** Add `NavGrid Nav` to `TaskContext`, filled in by the brain.
  The brain gets it from the NPC, so **NPCs need to know their level**: add `Location` to
  `NPC`, set by `NpcRoster.Add`. It's needed for anything level-aware, and it gets updated
  when NPCs change level later.
- **Keep the straight-line fallback** when `Nav` is null (tests, or a level with no grid
  yet), so nothing breaks while this is half-built.

**Tests:** a `TestNpc` with a nav grid and a tree between it and the target. Tick the task
(as `TaskTests` already does), and check it ends at the target without its body ever
overlapping the tree box. Then the same setup with no grid, to keep the old straight-line
behaviour.

---

## Step 4 — Getting stuck and failing

The grid only knows about objects. **The player and other NPCs aren't in it**: they move,
and putting them in would mean replanning constantly. So the path can still be blocked
by someone standing on it. Collision pushes the NPC aside, which is often enough, but
not always.

- **Stuck check:** if the NPC hasn't got closer to its waypoint for ~1s (track the
  best distance so far), **replan** from where it is now.
- **Give up:** after ~3 failed replans, or when `FindPath` returns `null`, the task
  **fails** instead of trying forever. Add `Failed` to `TaskState`, and have `TickTasks`
  drop a failed task the same way it drops a completed one. Later, failures are worth
  remembering ("couldn't reach the well"), which ties into the Phase 8 event log.
- **`OnBlocked`** already reaches the brain when the NPC bumps something. It's a cheap
  trigger for "check if I'm stuck" without polling.

**Tests:** a path blocked by a `TestEntity` that never moves fails after the retries; a
target that can't be reached fails at once; `TickTasks` moves on to the next task after a
failure.

---

## Step 5 — Nicer movement (optional)

- **Path smoothing (string-pulling):** grid paths zig-zag (straight, diagonal, straight).
  Skip any waypoint where the NPC can see the one after it in a straight line (a
  line-of-sight check across the grid's cells). The NPC then walks in long straight runs.
  It's a small function and easy to test: a path across an empty grid smooths down to
  just start and end.
- **Arrive slowdown:** ease off speed in the last half-cell, so the NPC doesn't stop dead.
- **Debug drawing:** draw blocked cells and each NPC's current path (lines between
  waypoints) behind a `Debug` flag, like `SHOWBOUNDS`. It's the fastest way to see *why*
  an NPC walks where it does, and it's worth doing alongside step 3, not after.
- **A console command** (once the debug console exists): `goto <npc> <poi>` to test any
  route by hand, and `nav` to toggle the debug drawing.

---

## Later

- **Tile walkability:** a `Walkable` flag in `TileCatalog` for water, walls and cliffs.
  NavGrid reads it with no other changes.
- **Moving between levels:** a path to a POI in *another* level is a route to the exit
  cell, a level change, then a route inside the new level. That's a `go_to` with a level
  name, and it needs NPCs to own their `Location` (step 3).
- **Crowds:** if many NPCs head to the same place, a flow field replaces per-NPC A\*.
- **Bigger maps:** if levels grow past ~100x100, cache paths or use hierarchical A\*.
  It isn't needed at this size.

---

## Order and what each step proves

1. **NavGrid + tests:** the game knows where you can walk.
2. **Pathfinder + tests:** routes are correct, with no NPC involved.
3. **GoToTask follows paths, plus debug drawing:** the knight walks round the well.
4. **Stuck and fail:** nothing walks into a wall forever.
5. **Smoothing:** it looks natural.

Steps 1–2 are pure logic and good C# practice (`PriorityQueue`, dictionaries, `Point`).
Step 3 is where it meets the game.
