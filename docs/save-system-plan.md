# Save System — Plan

## Summary

Close the game, reopen it, and the world is as you left it — most importantly, NPCs still
remember you. Saving converts the live game into **plain save classes** (like the level
DTOs `MapData` / `PoiData`, but the other direction), writes them as JSON, and rebuilds the
real objects on load. Its main prerequisite, world units (positions that don't depend on
window size), is done. It reuses the file approach the [settings plan](Completed/settings-plan.md)
built and tested.

---

## Where things are now

*(Refreshed after the World split, world units and pathfinding.)*

- **Levels** load from authored JSON via `MapLoader` (DTOs → domain objects). Nothing is ever
  written back.
  - `World` keeps each visited `Location` in `_visited`, so a revisit is the same object.
  - Each `Location` builds its `Nav` grid (pathfinding) in its constructor.
  - None of this needs saving: it all comes from the level file.
- **The live state is spread over `World`'s parts** (the old single `World` was split up):
  - **`GameClock`:** `CurrentDate` (a `GameDate`) plus private `_elapsedSeconds` into the day.
  - **`World`:** `player` and `location` (the level being played).
  - **`NpcRoster`:** `_byLevel` (each level's NPCs), `_populated` (levels whose starting NPCs
    have spawned) and `_nextId`.
  - **`ConversationSystem`:** `_nextTranscriptId`, plus conversations and decisions in flight.
- **Everything is in cells**, not pixels: `Position`, `Feet`, `GoToTask` targets. So saved
  positions mean the same thing at any window size.
- **Each NPC** has `Name`, `Persona`, `Location` (set by `NpcRoster.Add`) and a brain holding:
  - a `Journal`: memories (hot) and transcripts (cold), plain data by design;
  - a task queue (`GoToTask` targets in cells, `WaitTask` seconds).
- **NPCs have a runtime `Id`** (1, 2, 3… by spawn order), which isn't stable across runs.
  `Journal` and `Transcript` say who someone is by **name** ("Traveller", "Sir Lancelot").
- **Starting NPCs are hard-coded** in `NpcRoster.SpawnStartingNpcs` (Medows: the knight).
- **`World` is created once in `AshenveilGame.LoadContent` and started with `Init()`.** There's
  no "load" path.
- **The console can check what was restored:** `npcs`, `journal <npc>`, `where [npc]`.

---

## Core idea: save data, not objects

- **Save:** game objects → save classes (`SaveData`, `PlayerSave`, `NpcSave`,
  `TranscriptSave`…) → JSON.
- **Load:** JSON → save classes → rebuild the real `Knight`, `Journal`, etc.

Never serialise live objects: a `Knight` holds textures, a brain, a back-reference and
animation state. Save classes hold only what's needed to rebuild it.

---

## What's saved

| Saved | Not saved | Why not |
|---|---|---|
| Clock (`CurrentDate` + time into the day) | Level tiles, objects, POIs | Already in the level JSON; save only what changes |
| Player: level, position | Conversations in progress | Saving is blocked while a chat is open |
| Each visited level's NPCs: type, stable key, name, position, journal | Decisions waiting on the LLM | Lost; the NPC just misses learning from one chat |
| Id counters (`NpcRoster._nextId`, `ConversationSystem._nextTranscriptId`), populated levels (`NpcRoster._populated`) | Window size, cell size, textures, nav grids, the `_visited` level cache | Rebuilt at startup / from the level files |
| — | Task queues (first version) | Back to default behaviour on load; see below |

---

## Decisions

### Agreed / recommended

1. **Stable NPC keys.** Each NPC gets a permanent key (`"medows_knight"`) assigned where
   it's spawned (`NpcRoster.SpawnStartingNpcs` now, the level JSON later). Saves refer to NPCs by key;
   the runtime `Id` stays for in-session lookups only.
2. **Positions in cells** ✅: guaranteed by the world-units refactor, which is done.
3. **NPC factory.** A lookup from saved type name to constructor:
   `"Knight" → (pos, id, name) => new Knight(..., new KnightBrain())`. One line per NPC type.
4. **`version` field** from the first save. New fields just come back empty in old saves;
   the version is for the rare breaking change.
5. **Safe write**: `slot.json.tmp`, then replace. This is the same as `Config/SettingsStore.Save()`,
   so copy its pattern (or pull the write-temp-then-rename into a small shared helper, now that
   there are two users).
6. **Location**: `%LOCALAPPDATA%\Ashenveil\saves\`, i.e. a `saves` folder inside
   `SettingsStore.DefaultFolder`. Take the folder as a parameter, as `SettingsStore` does, so
   tests use a temp folder.
7. **Loading never throws**, like `SettingsStore.Load()`: a missing save means "no Continue"; a
   broken one means a clear message, and the game still starts.
8. **Mapping is testable.** Game ↔ save-class conversion is plain methods that don't need
   the GPU or a `World` (same approach as `Conversation.ToTranscript`). The key test:
   **save → load → everything matches.**
9. **Load path.** A way to build `World` from a `SaveData`, next to the existing `Init()`
   (new game). The main menu gets **Continue** next to **New Game**. This is the biggest
   structural change.

### Open — to decide before building

1. **JSON or SQLite?** Recommendation: **JSON** now — already used, readable, simple. SQLite
   later if transcripts get large or memory search needs it (embeddings recall). Save
   classes don't care which, so switching is contained.
2. **Slots:** one or several? (Recommendation: one; slots later.)
3. **When to save:** pause-menu "Save" + save on quit first; autosave on level change / day
   end later.
4. **Task queues:** skip in the first version (recommended): on load, NPCs fall back to
   their default behaviour. Saving them later is now straightforward:
   - `GoToTask` targets are in cells, so saving the target (or the POI name it came from)
     is enough;
   - the path is replanned on the first tick anyway;
   - a `WaitTask` saves its seconds left.

   Each task type needs a small save class (`{ "type": "go_to", "x": 3.5, "y": 7.9 }`).
5. **Player identity:** the player is "Traveller", and transcripts/memories store names.
   Keep that for now, or introduce a stable player id + chosen name now? (Renaming the
   player later would otherwise break every memory of them.)

---

## Save file shape (first version)

```json
{
  "version": 1,
  "savedAt": "2026-10-20T14:03:00Z",
  "clock": { "totalDays": 4, "secondsIntoDay": 213.5 },
  "player": { "level": "Medows", "x": 5.5, "y": 3.25 },
  "counters": { "nextNpcId": 3, "nextTranscriptId": 12 },
  "levels": {
    "Medows": {
      "npcs": [
        {
          "key": "medows_knight", "type": "Knight", "name": "Sir Lancelot",
          "x": 7.0, "y": 2.5,
          "memories": [ { "text": "The traveller's name is Loki", "kind": "Conversation", "sourceId": 4 } ],
          "transcripts": [ { "id": 4, "day": 3, "location": "Medows", "participants": ["Traveller", "Sir Lancelot"],
                             "lines": ["Traveller: my name is Loki", "..."], "summary": "..." } ]
        }
      ]
    }
  }
}
```

Shared transcripts (a group conversation) appear once per NPC in this shape. Fine for v1;
if it matters, store transcripts once at the top level and have journals list ids.

---

## Steps

1. **Stable NPC keys** in spawning.
2. **Save classes** + **`SaveMapper`**: game → save classes and back (memories, transcripts,
   NPCs, player, clock). Round-trip tests.
3. **NPC factory** (type name → constructor + brain).
4. **`SaveStore`**: write (temp then replace) / read / "is there a save?", `version`.
5. **World load path**: build a `World` from `SaveData`. That's the levels, `NpcRoster`'s
   buckets and `_populated`, both id counters, the `GameClock` date and time into the day, and
   the player's level and feet position. `NpcRoster` and `GameClock` will each need a way to
   be set from saved values (today their counters and clock are private, with no setters).
6. **Menu**: Continue (only if a save exists) + New Game; pause-menu Save; save on quit.
   Block saving during a conversation.
7. **Live check**: tell the knight your name, save, quit, relaunch, Continue, ask him.

---

## Testing

- Round-trip: build a world state → save → load → compare (memories, transcripts with
  summary and `SourceId`, NPC positions, clock, counters).
- Missing save → no Continue; corrupt save → clear error, game still starts.
- Older `version` → loads with defaults for new fields.
- Ids after load: new transcripts/NPCs don't reuse saved ids (counters restored).

---

## Depends on

- **World units** (required) ✅ done: positions, sizes, speeds and task targets are in cells,
  so they don't depend on window size. See [Completed/world-units-plan.md](Completed/world-units-plan.md).
- **Settings** ✅ done: proved the folder, safe write and versioning approach on simple data.
  Reuse from `Config/`:
  - `SettingsStore.DefaultFolder` for where saves go;
  - `Save()`'s write-`.tmp`-then-rename;
  - `Load()`'s "never throws; anything wrong means defaults";
  - the `version` field.

  See [Completed/settings-plan.md](Completed/settings-plan.md).
- **Debug console** (helpful) ✅ done: `journal`, `where [npc]`, `npcs` to check what was restored.
  See [Completed/debug-console-plan.md](Completed/debug-console-plan.md).
