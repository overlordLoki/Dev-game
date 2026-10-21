# Save System — Plan

## Summary

Close the game, reopen it, and the world is as you left it — most importantly, NPCs still
remember you. Saving converts the live game into **plain save classes** (like the level
DTOs `MapData` / `PoiData`, but the other direction), writes them as JSON, and rebuilds the
real objects on load. It's built **after** the world-units refactor (so positions don't
depend on window size) and reuses the file approach from the settings plan.

---

## Where things are now

- Levels load from authored JSON via `MapLoader` (DTOs → domain objects). Nothing is ever
  written back.
- `World` holds the live state: the clock (`CurrentDate`, `_elapsedSeconds`), the player,
  `_npcsByLevel` (each visited level's NPCs), `_populated`, and id counters
  (`_nextNpcId`, `_nextTranscriptId`).
- Each NPC's brain holds a `Journal`: memories (hot) and transcripts (cold) — plain data
  already, by design.
- NPCs have a **runtime** `Id` (1, 2, 3… by spawn order) — not stable across runs.
- `World` is created once in `LoadContent` and started with `Init()`. There's no "load"
  path.

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
| Id counters, visited levels (`_populated`) | Window size, cell size, textures | Rebuilt at startup |
| — | Task queues (first version) | Back to default behaviour on load; see below |

---

## Decisions

### Agreed / recommended

1. **Stable NPC keys.** Each NPC gets a permanent key (`"medows_knight"`) assigned where
   it's spawned (`SpawnLevelNpcs` now, the level JSON later). Saves refer to NPCs by key;
   the runtime `Id` stays for in-session lookups only.
2. **Positions in cells** — guaranteed by the world-units refactor, which comes first.
3. **NPC factory.** A lookup from saved type name to constructor:
   `"Knight" → (pos, id, name) => new Knight(..., new KnightBrain())`. One line per NPC type.
4. **`version` field** from the first save. New fields just come back empty in old saves;
   the version is for the rare breaking change.
5. **Safe write**: `slot.json.tmp`, then replace.
6. **Location**: `%LOCALAPPDATA%\Ashenveil\saves\` (same folder approach as settings).
7. **Mapping is testable.** Game ↔ save-class conversion is plain methods that don't need
   the GPU or a `World` (same approach as `Conversation.ToTranscript`). The key test:
   **save → load → everything matches.**
8. **Load path.** A way to build `World` from a `SaveData`, next to the existing `Init()`
   (new game). The main menu gets **Continue** next to **New Game**. This is the biggest
   structural change.

### Open — to decide before building

1. **JSON or SQLite?** Recommendation: **JSON** now — already used, readable, simple. SQLite
   later if transcripts get large or memory search needs it (embeddings recall). Save
   classes don't care which, so switching is contained.
2. **Slots:** one or several? (Recommendation: one; slots later.)
3. **When to save:** pause-menu "Save" + save on quit first; autosave on level change / day
   end later.
4. **Task queues:** skip in the first version (recommended). Saving them needs tasks
   described by POI name rather than pixel target — doable after world units.
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
5. **World load path**: build a `World` from `SaveData` (levels, NPC buckets, `_populated`,
   counters, clock, player position).
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
- **Settings plan** (helpful): proves the folder, safe write and versioning approach first.
- **Debug console** (helpful) ✅ done: `journal`, `where [npc]`, `npcs` to check what was restored.
  See [Completed/debug-console-plan.md](Completed/debug-console-plan.md).
