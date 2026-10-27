# Save System — Plan

## Summary

Close the game, reopen it, and the world is as you left it — most importantly, NPCs still
remember you. Saving converts the live game into **plain save classes** (like the level
DTOs `MapData` / `PoiData`, but the other direction), writes them to a **SQLite database,
one file per save** (`saves/slot1.db`), and rebuilds the real objects on load.

**Why SQLite from the start, not JSON:** the cold tier of NPC memory (transcripts, and later
the event log) is what grows without limit and what recall will want to *search* (by person,
by day, eventually by meaning). That ends up in a database either way, so starting there
avoids writing a JSON save format only to migrate off it. v1 still saves a **snapshot** (the
game keeps everything in memory while playing). The tables are laid out so the cold tier can
later be written incrementally and queried in place, without changing the format.

Its main prerequisite, world units (positions that don't depend on window size), is done.
The [settings plan](Completed/settings-plan.md) built the "where files go" and "loading never
throws" approach this reuses. Settings themselves stay JSON: they're machine config, small and
meant to be hand-editable.

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
- **NPCs have a runtime `Id`** (1, 2, 3… by spawn order; the player is 0), which isn't
  saved yet, so isn't stable across runs.
  `Journal` and `Transcript` say who someone is by **name** ("Traveller", "Sir Lancelot").
- **Starting NPCs are hard-coded** in `NpcRoster.SpawnStartingNpcs` (Medows: the knight).
- **`World` is created once in `AshenveilGame.LoadContent` and started with `Init()`.** There's
  no "load" path.
- **The console can check what was restored:** `npcs`, `journal <npc>`, `where [npc]`.

---

## Core idea: save data, not objects

- **Save:** game objects → save classes (`SaveData`, `PlayerSave`, `NpcSave`,
  `TranscriptSave`…) → SQLite rows.
- **Load:** SQLite rows → save classes → rebuild the real `Knight`, `Journal`, etc.

Never serialise live objects: a `Knight` holds textures, a brain, a back-reference and
animation state. Save classes hold only what's needed to rebuild it.

**Two layers, so each can be tested on its own:**
- **`SaveMapper`:** game ↔ save classes. Pure, no database, so it's fast to test.
- **`SaveStore`:** save classes ↔ SQLite. The only code that knows about tables and SQL.
  Tested against a real database file in a temp folder.

The save classes are the boundary between the two.

---

## What's saved

| Saved | Not saved | Why not |
|---|---|---|
| Clock (`CurrentDate` + time into the day) | Level tiles, objects, POIs | Already in the level JSON; save only what changes |
| Players: id, name, level, feet position | Conversations in progress | Saving is blocked while a chat is open |
| Each visited level's NPCs: type, id, name, position, journal | Decisions waiting on the LLM | Lost; the NPC just misses learning from one chat |
| Id counters (`NpcRoster._nextId`, which now numbers people, not just NPCs; `ConversationSystem._nextTranscriptId`), populated levels (`NpcRoster._populated`) | Window size, cell size, textures, nav grids, the `_visited` level cache | Rebuilt at startup / from the level files |
| Each NPC's task queue (`go_to` targets, `wait` time left), in order | A task's planned path and stuck timers | Replanned on the first tick after loading |

---

## Decisions

### Agreed / recommended

1. **Ids are saved, so they're stable.** Every person's `IEntity.Id` comes from one counter
   that only counts up and is itself saved, so an id is never reused within a save. The
   save's owner is player `0`; NPCs (and, later, other players) take the next number. Saves
   refer to everyone by that id, and loading rebuilds each one with it.
   *(Changed 2026-10-27: this replaces stable string keys like `"medows_knight"` and player
   GUIDs. Ids only need to be unique within one save, which the saved counter guarantees.)*
2. **Positions in cells** ✅: guaranteed by the world-units refactor, which is done.
3. **NPC factory.** A lookup from saved type name to constructor:
   `"Knight" → (pos, id, name) => new Knight(..., new KnightBrain())`. One line per NPC type.
4. **SQLite, one database file per save** ✅ decided: `saves/slot1.db`. Through
   `Microsoft.Data.Sqlite`, Microsoft's lightweight ADO.NET provider. It bundles the native
   SQLite library for Windows, Linux, macOS and Android, so nothing to install. No ORM:
   plain SQL with parameters is easy to read, and it teaches how the tables actually work.
5. **Schema version: `PRAGMA user_version`**, SQLite's built-in integer for exactly this.
   - A new file is created at the current version.
   - Opening an older one runs the **migrations** in order (version 1 → 2 → ...), each a few
     `ALTER TABLE` / `CREATE TABLE` statements kept in code.
   - A file *newer* than the game knows is refused with a clear message, never half-read.
6. **Safe write: one transaction.** A v1 save is a snapshot: in a single transaction, clear
   the tables and insert the current state. SQLite's journal makes a transaction all or
   nothing, so a crash mid-save leaves the previous save intact. The settings store's
   `.tmp`-then-rename trick isn't needed here.
7. **Location**: `%LOCALAPPDATA%\Ashenveil\saves\`, i.e. a `saves` folder inside
   `SettingsStore.DefaultFolder`. Take the folder as a parameter, as `SettingsStore` does, so
   tests use a temp folder.
8. **Loading never throws**, like `SettingsStore.Load()`: a missing save means "no Continue";
   a broken or too-new one means a clear message, and the game still starts.
9. **Transcripts are stored once**, with each NPC's journal listing the transcript ids it holds.
   That matches the game, where one `Transcript` object is shared by everyone in the
   conversation, and it's how the cold tier will be queried later.
10. **Mapping is testable.** Game ↔ save-class conversion is plain methods that don't need
   the GPU or a `World` (same approach as `Conversation.ToTranscript`). The key test:
   **save → load → everything matches.**
11. **Load path.** A way to build `World` from a `SaveData`, next to the existing `Init()`
   (new game). The main menu gets **Continue** next to **New Game**. This is the biggest
   structural change.

### Decided (2026-10-27)

1. **SQLite** from the start (see the summary and decision 4).
2. **One save slot** for now (`slot1.db`). More slots later are just more files; nothing in
   the schema changes.
3. **Save only from the Save button** (in the pause menu). No save on quit and no autosave:
   saving is always a deliberate choice. Worth adding with it: quitting asks "Quit without
   saving?" when there's been play since the last save, so a forgotten save isn't lost
   silently.
4. **Task queues are saved.** They're part of the game: an NPC sent to the well should still be
   on its way after a load. See decision 12 below.
5. **Players get a stable id and a name.** Multiplayer may come later, so the save holds a
   **list of players**, not one, and nothing about a person relies on their name being unique
   or unchanging. See decision 13 below.

### Following from those

12. **Saving tasks.** Each task in each NPC's queue becomes a row, in order: `go_to` saves its
    target (in cells); `wait` saves the seconds left. Only what's needed to carry on:
    - a `GoToTask`'s planned path, waypoint index and stuck timers aren't saved; it replans on
      its first tick, which it already does whenever it starts;
    - completed and failed tasks are never saved (they've already left the queue);
    - a task in the middle of running is saved like a waiting one, and picks up from there;
    - on load, an NPC with a non-empty queue is put back in the `Tasked` state, as `Enqueue` does.

    This needs a little game code first: `WaitTask` must expose its time left (today
    `_elapsed` is private), and `GoToTask` its target. The type names match the decision
    schema's (`"go_to"`, `"wait"`), so one new task type means one new branch in the
    decision code *and* one in the save mapping. New types (`patrol`, `follow`...) add their
    columns or rows when they're built.
13. **People are known by id, not by name.**
    - **Ids:** the saved `IEntity.Id` from decision 1, for players and NPCs alike. The save's
      owner is `0`.
    - **Players** get a `Name` ("Traveller" until there's a way to choose one).
    - **Keep multiplayer easy:** `0` only means "this save's owner", never "the player". Ask
      `is Player`, not `Id == 0`, and get *which* player from the conversation (its
      initiator / participants), not from `World.player`.
    - **Later, not now:**
      - recognising a *returning* multiplayer guest needs something stable from their side
        (an account or GUID) mapped to their id: one new column via a migration;
      - an NPC's id depends on spawn order in each new game, so when hand-written content
        (level JSON, quests) needs to name "the knight", add a key column then.
    - **This changes game code before saving.** Today a transcript's `Participants` are
      **names**, `Conversation.PlayerName` is the constant `"Traveller"`, and
      `NPC.PastConversationsWith(person)` matches by name. So renaming the player, or two
      players with the same name, would mix up memories.
    - **Transcripts keep both:** participant **ids** (who it was) and **names** (what they were
      called then, for the LLM prompt and the console).
    - **Recall matches by id.**
    - **Memory text stays text** ("The traveller's name is Loki"): it's what the NPC
      *believes*, written in words, and is fine to keep as words.
    - **Multiplayer itself (networking, several local players) is not part of this plan.**
      This just makes sure the save format and memories won't need redoing when it comes.

---

## Database shape (schema version 1)

Plain tables, one row per thing. Names are `snake_case`, the SQL convention. Every id is
a person's saved `IEntity.Id` (decision 1); players and NPCs share one numbering.

```sql
-- One row: everything there's only one of.
CREATE TABLE game (
    id                 INTEGER PRIMARY KEY CHECK (id = 1),
    saved_at           TEXT    NOT NULL,   -- ISO 8601, UTC
    total_days         INTEGER NOT NULL,   -- GameClock.CurrentDate
    seconds_into_day   REAL    NOT NULL,   -- GameClock._elapsedSeconds
    next_person_id     INTEGER NOT NULL,   -- NpcRoster._nextId: the next player or NPC id
    next_transcript_id INTEGER NOT NULL    -- ConversationSystem._nextTranscriptId
);

-- Every player (one for now; a list so multiplayer doesn't need a new format).
CREATE TABLE players (
    player_id INTEGER PRIMARY KEY,   -- IEntity.Id; 0 is the save's owner
    name      TEXT NOT NULL,         -- "Traveller" until names can be chosen
    level     TEXT NOT NULL,
    x         REAL NOT NULL,      -- feet, in cells
    y         REAL NOT NULL
);

-- Levels whose starting NPCs have already spawned (NpcRoster._populated).
CREATE TABLE populated_levels (level TEXT PRIMARY KEY);

CREATE TABLE npcs (
    npc_id  INTEGER PRIMARY KEY,   -- IEntity.Id
    type    TEXT NOT NULL,         -- "Knight", for the NPC factory
    name    TEXT NOT NULL,
    level   TEXT NOT NULL,
    x       REAL NOT NULL,      -- feet, in cells
    y       REAL NOT NULL
);

-- Hot tier: facts, in the order they were learned.
CREATE TABLE memories (
    npc_id        INTEGER NOT NULL REFERENCES npcs(npc_id),
    seq           INTEGER NOT NULL,
    text          TEXT    NOT NULL,
    kind          TEXT    NOT NULL,   -- MemoryKind name: "Conversation", "Event"
    transcript_id INTEGER,            -- Memory.SourceId; NULL if it didn't come from a chat
    PRIMARY KEY (npc_id, seq)
);

-- Cold tier: each conversation stored once...
CREATE TABLE transcripts (
    id       INTEGER PRIMARY KEY,
    day      INTEGER NOT NULL,
    location TEXT,
    summary  TEXT
);
CREATE TABLE transcript_participants (
    transcript_id INTEGER NOT NULL REFERENCES transcripts(id),
    seq           INTEGER NOT NULL,
    person_id     INTEGER NOT NULL,   -- a player_id or an npc_id: who it was
    name          TEXT    NOT NULL,   -- what they were called at the time
    PRIMARY KEY (transcript_id, seq)
);
CREATE TABLE transcript_lines (
    transcript_id INTEGER NOT NULL REFERENCES transcripts(id),
    seq           INTEGER NOT NULL,
    text          TEXT    NOT NULL,   -- "Traveller: my name is Loki"
    PRIMARY KEY (transcript_id, seq)
);
-- ...and each NPC's journal lists the ones it holds.
CREATE TABLE journal_transcripts (
    npc_id        INTEGER NOT NULL REFERENCES npcs(npc_id),
    transcript_id INTEGER NOT NULL REFERENCES transcripts(id),
    PRIMARY KEY (npc_id, transcript_id)
);

-- Each NPC's task queue, front first. Each type fills the columns it uses:
--   go_to: x, y (the target, in cells)      wait: seconds (time left)
CREATE TABLE tasks (
    npc_id  INTEGER NOT NULL REFERENCES npcs(npc_id),
    seq     INTEGER NOT NULL,       -- 0 = the front of the queue
    type    TEXT    NOT NULL,       -- "go_to", "wait": the decision schema's names
    x       REAL,
    y       REAL,
    seconds REAL,
    PRIMARY KEY (npc_id, seq)
);
```

On load, transcripts are built once and the **same** `Transcript` object is handed to every
journal that lists it, just as it was shared before saving.

**Later, without changing these tables:**
- **Cold tier written incrementally:** insert a transcript when a conversation ends, instead
  of only at save time.
- **The event log** becomes a new `events` table (Part B of the NPC plan).
- **Recall by query:** "conversations with Loki in the last 10 days" becomes a `WHERE`, not a
  loop over everything.

---

## Steps

1. **Identity** (game code, before any saving). Everyone is known by their `IEntity.Id`
   (decision 13), so no new id fields:
   - `Player.Name` ("Traveller" for now), replacing the `Conversation.PlayerName` constant.
     Its users: `ConversationSystem.BuildRequest` (the initiator's name for the prompt, its
     id for recall) and `Chat.cs` (the player's lines and their colour);
   - `Transcript.Participants` becomes `(Id, Name)` pairs: who it was, and what they were
     called then. Kept as pairs so the two can't drift apart (it's also the
     `transcript_participants` table's shape);
   - `NPC.PastConversationsWith(int personId, ...)` matches by id;
   - the `journal` console command can keep printing names.

   The existing tests show what changes: anything comparing with `"Traveller"` or a
   participant name, and every `new Transcript(...)`. Worth adding: recall still finds a
   conversation after the player is renamed; two people with the same name aren't mixed
   up; a transcript records each participant's id and name.
2. **Save classes** + **`SaveMapper`**: game → save classes and back (memories, transcripts,
   NPCs, player, clock). Round-trip tests.
3. **NPC factory** (type name → constructor + brain) and **task mapping** (`"go_to"` ↔
   `GoToTask`, `"wait"` ↔ `WaitTask`; `WaitTask` exposes its time left).
4. **`SaveStore`** (SQLite):
   - **create** the schema on a new file and set `user_version`;
   - **save** a snapshot in one transaction;
   - **load** into save classes;
   - **"is there a save?"**;
   - **migrations:** an empty list for now, with the "too new" check.

   Add the `Microsoft.Data.Sqlite` package to `Ashenveil.Core`, and check that the DesktopGL
   build still runs (the native library has to be copied alongside the game).
5. **World load path**: build a `World` from `SaveData`. That's the levels, `NpcRoster`'s
   buckets and `_populated`, both id counters, the `GameClock` date and time into the day, and
   the player's level and feet position. `NpcRoster` and `GameClock` will each need a way to
   be set from saved values (today their counters and clock are private, with no setters).
6. **Menu**: Continue (only if a save exists) + New Game; a **Save** button in the pause
   menu, the only way to save. Block saving during a conversation. "Quit without saving?" when
   there's unsaved play.
7. **Live check**:
   - tell the knight your name, save, quit, relaunch, Continue, and ask him;
   - send him to the well (or catch him mid-walk), save, reload: he carries on walking there.

---

## Testing

- **`SaveMapper` round trip** (no database): world → save classes → world, then compare
  memories, transcripts (with summary and `SourceId`), NPC positions, clock and counters.
- **`SaveStore` round trip** against a real `.db` in a temp folder: save classes → file →
  save classes, field for field.
- **A shared transcript stays shared:** two NPCs who were in the same conversation get the
  *same* `Transcript` object back after loading, not two copies.
- **Saving twice:** the second save fully replaces the first; no leftover rows.
- **Missing save** → no Continue. **A file that isn't a database, or is missing tables** → a
  clear error, and the game still starts. **`user_version` newer than the game** → refused.
- **Ids after load:** new transcripts and NPCs don't reuse saved ids (counters restored).
- **Tasks:** a queue of `go_to` + `wait` + `go_to` comes back in order, with the wait's time
  left; an NPC with tasks loads as `Tasked`, one without as its default state. A `go_to` whose
  target is now unreachable fails cleanly after loading (that's the existing `Failed` path).
- **Identity:** the player's id survives save and load. Recall still finds past conversations
  after the player's *name* changes (it matches the id). Two people with the same name aren't
  mixed up.
- **By hand:** open `slot1.db` in "DB Browser for SQLite" (free) to see what was written.
  It's the database equivalent of reading a JSON file.

---

## Depends on

- **World units** (required) ✅ done: positions, sizes, speeds and task targets are in cells,
  so they don't depend on window size. See [Completed/world-units-plan.md](Completed/world-units-plan.md).
- **Settings** ✅ done: proved the folder, versioning and "never throws" approach on simple
  data. Reuse from `Config/`:
  - `SettingsStore.DefaultFolder` for where saves go;
  - `Load()`'s "never throws; anything wrong means a clear fallback";
  - the idea of a version number. Here it's `PRAGMA user_version` rather than a JSON field,
    and SQLite's transactions replace the `.tmp`-then-rename write.

  See [Completed/settings-plan.md](Completed/settings-plan.md).
- **Debug console** (helpful) ✅ done: `journal`, `where [npc]`, `npcs` to check what was restored.
  See [Completed/debug-console-plan.md](Completed/debug-console-plan.md).
