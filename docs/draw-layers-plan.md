# Draw Layers — Plan

## Summary

Things that should cover the player — tree canopies, roofs, archways, bridges — need more
than sorting by y. The fix is **three draw layers**: Ground (flat, drawn first), World
(sorted by the base's y — what the game does now), and Overhead (always on top, fading when
the player is underneath). It's a rendering feature, not a position coordinate: positions
stay x/y (see [world-units-plan.md](Completed/world-units-plan.md)).

Step 1, the sort-key fix (entities sorted by the bottom of their sprite instead of their
feet), is ✅ done. What's left is the layers themselves.

---

## Where things are now

*(Refreshed after the World split and world units.)*

- **Drawing lives in `GameplayScreen.Draw`** (since the World split, `World` doesn't draw). It
  already does two of the three layers:
  1. tiles first (`location.tileMap.Draw`), effectively the **Ground** layer;
  2. the player, NPCs and objects collected as `IWorldDrawable`, sorted by `SortY`, drawn in
     order: the **World** layer.

  After those, `DrawNav` draws the pathfinding overlay when `Debug.SHOWNAV` is on (the `nav`
  console command). It's a debug overlay, so it stays on top of everything, Overhead included.
- **`IWorldDrawable`** = `float SortY` + `Draw`. Nothing says which layer a thing belongs in.
- **Sort keys are in cells now (world units):**
  - **`GameObject.SortY = Row + 1`:** the bottom of the object's cell (a tree's base). Good.
  - **`Entity.SortY`** is the bottom of the collision box, the feet (`Feet.Y`). It used to be the
    bottom of the **sprite**, which made padded sprites like the knight sort as if standing
    lower than they are. **Fixed (step 1).**
- **Entities stand near the bottom of their cell** (`PointOfInterest.StandingSpot()`, feet at
  0.9), so a sprite's head reaches into the cell above. A tree in that upper cell sorts behind
  them (its base is higher up), which is right.
- A tree is one sprite (trunk + canopy) in the World layer. Its collision boxes
  (`bounds.json`) cover the trunk only.

---

## Decisions

1. **Three layers, drawn in order:**

   | Layer | Contents | Drawn |
   |---|---|---|
   | **Ground** | Tiles, paths, flat decals (puddles, shadows) | First, no sorting |
   | **World** | Entities, trunks, wells, bushes | Sorted by `SortY` |
   | **Overhead** | Canopies, roofs, archways, bridge tops | Last, always on top |

2. **A `Layer` on `IWorldDrawable`**, defaulting to World, so existing objects don't change.
3. **Sort by feet, not sprite** ✅: `Entity.SortY` uses the collision box's bottom, the same
   "feet" (`Entity.Feet`) that spawning, walking, doors and the map edge use.
4. **Big trees split into two sprites only when needed:** trunk (World, collides, sorts) +
   canopy (Overhead). Small trees and bushes stay one sprite in World. Don't split
   pre-emptively — y-sorting with the feet fix may be enough for most.
5. **Overhead fades when the player is under it** (e.g. 40% opacity when the player's feet are
   inside the canopy's area), so you never lose the player behind a roof or canopy.

---

## Steps

1. ✅ **Fix the sort key** — done. `Entity.SortY` is now `Bounds.Bottom` (the feet), falling
   back to the sprite bottom when an entity has no boxes in `bounds.json`. Tested in
   `SortYTests` (padded sprite, no boxes, full-frame box unchanged).
2. **`Layer` on `IWorldDrawable`** (default World) and a three-pass draw in
   `GameplayScreen.Draw`: Ground items, sorted World items, Overhead items (then the nav overlay).
3. **Fade** for Overhead items when the player's feet are inside their area.
4. **Split one tree** (the biggest canopy) into trunk + canopy as the first real Overhead
   object; check it looks right before doing more.
5. **Editor support** — let the level editor mark an object's layer, or place a canopy as
   its own Overhead object.

---

## Testing

- `SortY` fix: an entity's sort key equals its collision box bottom, regardless of sprite
  padding (unit test with `TestNpc` and known boxes).
- Layer ordering: given drawables in all three layers, the draw order is
  Ground → World (sorted) → Overhead (testable by recording draw calls in a fake drawable).
- Fade: player inside / outside an Overhead area gives the expected opacity.
- The look: by hand.

---

## Open questions

- **Fade or hide?** Fading is friendlier; hiding is simpler.
- **Do NPCs also trigger the fade, or only the player?** (Probably only the player.)
- ~~Sort fix before or after world units?~~ Settled: both are done, and `SortY` is in cells.

## Depends on / feeds into

- Independent. World units (the only thing it had to avoid clashing with) is done.
- **Collision is unaffected:** a canopy split off as an Overhead object has no collision boxes;
  the trunk keeps them. The nav grid only reads collision boxes, so pathfinding is unaffected too.
- Step 5 needs the level editor (`tools/editor`) to store a layer per object.
