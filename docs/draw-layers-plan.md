# Draw Layers — Plan

## Summary

Things that should cover the player — tree canopies, roofs, archways, bridges — need more
than sorting by y. The fix is **three draw layers**: Ground (flat, drawn first), World
(sorted by the base's y — what the game does now), and Overhead (always on top, fading when
the player is underneath). It's a rendering feature, not a position coordinate: positions
stay x/y (see [world-units-plan.md](Completed/world-units-plan.md)).

Part of this is a **one-line bug fix** that's worth doing first, on its own: entities sort by
the bottom of their sprite instead of their feet.

---

## Where things are now

- `World.Draw` already does two of the three layers:
  1. tiles first (`location.tileMap.Draw`) — effectively the **Ground** layer;
  2. the player, NPCs and objects collected as `IWorldDrawable`, sorted by `SortY`, drawn in
     order — the **World** layer.
- `IWorldDrawable` = `SortY` + `Draw`. Nothing says which layer a thing belongs in.
- **Sort keys:**
  - `GameObject.SortY = (Row + 1) * Layout.CellHeight` — the bottom of the object's cell
    (a tree's base). Good.
  - `Entity.SortY` — was the bottom of the **sprite**, which made padded sprites like the
    knight sort as if standing lower than they are. **Fixed (step 1):** now the bottom of the
    collision box, the feet.
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
3. **Sort by feet, not sprite.** `Entity.SortY` uses the collision box's bottom
   (`Bounds.Bottom`) — the same "feet" the game already uses for `PlayerCell()`.
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
2. **`Layer` on `IWorldDrawable`** (default World) and a three-pass draw in `World.Draw`: Ground
   items, sorted World items, Overhead items.
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
- **Once world units land,** `SortY` should be in cells too — do the sort fix before or after
  that refactor? (Before is fine: it's a one-liner either way.)

## Depends on / feeds into

- Independent; the `SortY` fix can be done any time.
- Touches `World.Draw` and `IWorldDrawable`, which the world-units refactor also touches — easier
  to do the layer split after world units, or before it, but not in the middle.
