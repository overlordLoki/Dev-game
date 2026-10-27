# Save Slots — Plan

> **Status: steps 1–5 built; step 6 (the live check) is with the user.** See "What was built" below.

## Summary

The save system has one slot (`saves/slot1.db`). This plan turns that into **any number of
saves**, picked on a new **save slots screen**:
- **Save** in the pause menu opens it to make a new save or overwrite one;
- **Load** in the pause menu, and **Continue** on the main menu, open it to choose which save to play.

Each row shows who, where and when, and has a Delete button.

The save system was built for this: "more slots later are just more files; nothing in the
schema changes" ([save-system-plan.md](save-system-plan.md), decision 2). Each save is still
one SQLite file, written and read exactly as now. What's new is a folder of them, a cheap way
to describe a file without loading it, and the screen.

---

## What was built (2026-10-27)

Steps 1 to 5. Step 6, the live check in the game, is with the user.

- **`Saving/`:**
  - **`SaveStore`** takes a file's full path. `ReadSummary()` reads a `SaveSummary` without loading
    the save: read-only, never migrates, never throws (a file it can't read gives `Problem`).
  - **`SaveSummary`:** `FilePath`, `PlayerName`, `Level`, `TotalDays`, `SavedAt` (UTC), `Problem`,
    `IsReadable`; `Unreadable(path, problem)`.
  - **`SaveLibrary`** (replaced `SaveSlot`):
    - `List()` returns readable saves newest first, then damaged ones. Only `.db` files count,
      checked by hand as well, since on Windows `*.db` also matches `.dbx`.
    - `HasSaves` and `NextNewFilePath()`: the next file is `save-N.db`; gaps aren't refilled and
      other names don't count.
    - `CanSave` holds the no-saving-mid-conversation rule.
    - `TrySaveNew` and `TrySaveOver` both clear unsaved play. Overwriting an unreadable file
      deletes it first.
    - `TryLoad` and `TryDelete`: deleting a missing file is a message, not silence.
- **`GameDate`** has `SeasonName` and `Display` ("Summer 11, Year 1"); `GameClock` uses them.
- **`Screens/SaveSlotsScreen`:**
  - Two purposes, `Save` ("+ New save" first) and `Load`.
  - Rows show a headline and a detail, with Delete beside each row (not on it).
  - Scrolling with the mouse wheel (one row a notch) or the ^ and v buttons; a message line;
    Back or Escape leaves.
  - It only reports choices: `Open`, `Show`, `ShowMessage` and `OpenFor` for the caller.
    `Headline` and `Detail` are the row text, as static functions.
- **`AshenveilGame`:**
  - **Continue** and the pause menu's **Load** open load mode; the pause menu's **Save** checks
    `CanSave` first, then opens save mode.
  - **Confirms:** overwrite, delete, and load with unsaved play, all through one `Confirm(...)`
    helper, which Quit uses too.
  - `LoadSave(summary)` loads first and only then closes screens down to the main menu.
- **Console:** `saves` lists saves numbered from 1, newest first; `save` makes a new save;
  `save <n>` overwrites save n; `load <n>` loads save n. No questions asked, and failures are
  error lines.
- **Tests:** `SaveStoreTests` (summaries), `SaveLibraryTests`, `SaveSlotsScreenTests` and
  `SaveCommandsTests`. 507 tests in all, passing.

---

## Where things were before

- **`SaveStore`** reads and writes one fixed file: `FileName = "slot1.db"` in the folder it's
  given (`SaveStore.DefaultFolder` = `%LOCALAPPDATA%\Ashenveil\saves`). `HasSave`, `Save`,
  `TryLoad`. Schema version 1 in `PRAGMA user_version`.
- **`SaveSlot`** is what the menus call: `TrySave(world)` (refused mid-conversation, clears
  `World.HasUnsavedPlay`) and `TryContinue(out world)` (load + `World.FromSave`, with a sentence on failure).
- **Menus** (`AshenveilGame`):
  - main menu: Continue (only if `slot1.db` exists) loads it at once;
  - pause menu: Save writes `slot1.db`, and Load loads it, asking first if there's unsaved play;
  - Quit asks "Quit without saving?" when there's unsaved play.
- **Console:** `save` and `load`, both on `slot1.db`.
- **Widgets:** Button, Label, Panel, TextField, ChatLog... There's no list or scrolling
  widget yet, and `IInput` already passes the mouse through (which includes the scroll wheel).

---

## Decided (2026-10-27)

1. **Unlimited saves.** A save is any `.db` file in the saves folder. There's no fixed number of slots.
2. **Continue opens the slot screen** (to load). It's shown when there's at least one save.
3. **Each row shows details:** the player's name, their level, the in-game date and when it
   was saved. These are read from the file without loading the whole save.
4. **Overwriting asks first:** "Overwrite this save?"
5. **Saves can be deleted**, from the slot screen, after a confirm.

---

## Design

### Files

- **One file per save**, as now, in the same `saves` folder.
- **File names are generated and never shown:** `save-1.db`, `save-2.db`... (the next free
  number). What the player sees comes from *inside* the file, so a name only has to be unique.
- **`slot1.db` from the single-slot version** is just another `.db` in the folder, so it shows
  up in the list with nothing to migrate.
- **Anything in the folder that isn't a readable save** (a broken file, one from a newer game)
  still gets a row, marked as damaged. It can't be loaded, but it can be deleted or overwritten.
  Hiding it would leave files the player can't see or remove.

### `SaveStore`: one file, any name

- **The constructor takes the file's full path** instead of a folder: `new SaveStore(path)`.
  Everything else stays the same: `Save`, `TryLoad`, the schema, migrations and the
  "newer file" refusal.
- **New: `ReadSummary()`.** It reads only the `game` row and the first player, after the same
  version checks as `TryLoad`. That's cheap enough to run for every file each time the screen
  opens. It never throws: a file it can't read gives a summary with `Problem` set, so it still
  gets a row. It opens the file read-only and never migrates it, because listing saves
  shouldn't write to them.

### `SaveSummary`: what a row shows

Plain data, like the save classes:

| Field | From | Shown as |
|---|---|---|
| `FilePath` | the file | (not shown; it's what loading, overwriting or deleting needs) |
| `PlayerName` | `players.name` | "Loki" |
| `Level` | `players.level` | "Medows" |
| `TotalDays` | `game.total_days` | "Summer 11, Year 1" (via `GameDate.OnDay`) |
| `SavedAt` | `game.saved_at` (UTC) | "Saved 27 Oct 2026, 14:32", in local time |
| `Problem` | why it can't be read | "Damaged save: ..." instead of the details |

### `SaveLibrary` replaces `SaveSlot`

One class for "the saves folder", with the rules that are about saving rather than about one file:

- **`List()`:** a summary of every `.db` in the folder, newest first, damaged ones last. Never
  throws; a missing folder means no saves.
- **`HasSaves`:** for whether the main menu shows Continue.
- **`TrySaveNew(world, out message)`:** writes a new file (the next free `save-N.db`).
- **`TrySaveOver(world, summary, out message)`:** overwrites that save. The screen asks "Overwrite?" before calling it.
- **`TryLoad(summary, out world, out problem)`:** today's `TryContinue`, for one file.
- **`TryDelete(summary, out problem)`:** deletes the file. Deleting the save you're playing from is
  fine, because the game is in memory: it just has nowhere to "Load" back to.
- **The existing rules carry over:**
  - no saving mid-conversation, checked *before* the screen opens, so you're never shown a
    list you can't use;
  - a successful save clears `HasUnsavedPlay`;
  - every failure is a sentence for the player, never an exception.

### The slot screen (`SaveSlotsScreen`, in `Screens/`)

One screen with two modes, pushed over the main menu or the pause menu:

```
            ┌──────────────── Save Game ───────────────┐
            │  [ + New save ]                          │   <- save mode only
            │  Loki - Medows, Summer 11, Year 1        │
            │  Saved 27 Oct 2026, 14:32       [Delete] │
            │  Loki - CastleInterior, Spring 3, Year 1 │
            │  Saved 26 Oct 2026, 20:05       [Delete] │
            │  Damaged save (file is not a database)   │
            │                                 [Delete] │
            │                     [^] [v]     [ Back ] │
            └──────────────────────────────────────────┘
```

- **Click a row to use it:**
  - **save mode:** New save writes a new file; an existing row asks "Overwrite this save?";
  - **load mode:** the row is loaded. From the pause menu with unsaved play, it first asks "Load and lose what you've played since you last saved?"
- **Delete** on each row asks "Delete this save? This can't be undone."
- **Scrolling:** the mouse wheel, plus ^ and v buttons, when there are more rows than fit. The
  list is rebuilt from `List()` each time the screen opens and after every save or delete.
- **Messages:** a line at the bottom says what happened ("Game saved.", or why something failed).
- **Back or Escape** returns to the screen underneath.
- **Laid out in `Open(w, h)` each time it's shown**, as `SettingsScreen` is, so it fits after a resize.
- **It only reports choices.** `AshenveilGame` does the saving, loading and confirming, the same
  split as `ConfirmScreen`. So the screen needs no `World` or files, and the row text can be
  tested as plain functions.

### Menus and console

- **Main menu:** Continue (shown when `HasSaves`) opens the slot screen in load mode.
- **Pause menu:**
  - **Save:** checks for a conversation first, then opens save mode. After a save it goes
    back to the pause menu showing "Game saved."
  - **Load:** opens load mode.
  - **Quit's "Quit without saving?" is unchanged.**
- **Console:**
  - `saves` lists the saves, numbered as the screen shows them;
  - `save` makes a new save, and `save <n>` overwrites save n;
  - `load <n>` loads save n.

---

## Answered (2026-10-27): not now

1. **Player-typed save names** ("Before the wolves"): **later.** They'd need a `name` column,
   which means **schema version 2: the first real migration** (`ALTER TABLE game ADD COLUMN
   name TEXT`), a good way to prove the migration machinery works. Generated details are
   enough to tell saves apart for now.
2. **Quick save** to the save you loaded from (one click, no screen): **later.** It needs the
   game to remember which file it came from.
3. **A cap on how many saves are kept:** **no.** They're small files, and Delete is there.

---

## Steps

1. **`SaveStore` by file path** + `ReadSummary` + `SaveSummary`. The existing store tests
   switch to a path, and summary tests are added.
2. **`SaveLibrary`** (list, new name, save new or over, load, delete, the rules), replacing
   `SaveSlot`. Its tests replace `SaveSlotTests`.
3. **`SaveSlotsScreen`** (rows, scrolling, both modes, messages), plus tests for the row text.
4. **Menus:** Continue, pause Save and Load go through the screen, along with the confirms
   (overwrite, delete, unsaved load).
5. **Console:** `saves`, `save [n]`, `load <n>`.
6. **Live check:**
   - make three saves at different places, and check the list order and details;
   - overwrite one and delete one;
   - Continue into each;
   - drop a junk `.db` into the folder and check it shows as damaged and can be deleted.

---

## Testing

- **Summary:** a saved file gives its player's name, level, date and saved time without a full
  load. A broken file, one that isn't a save, or one that's too new gives a problem instead of a summary.
- **Listing:** newest first, with damaged files last. An empty or missing folder gives no saves.
  Non-`.db` files are ignored. The old `slot1.db` is listed.
- **New names:** `save-1.db`, then `save-2.db`. A gap (`save-2.db` deleted) is never reused while
  a higher number exists, so "newest" stays obvious when looking at the folder.
- **Overwrite:** saving over a summary changes that file only, and the others are untouched.
- **Delete:** the file is gone and the list shrinks. Deleting a file that's already gone is a message, not a crash.
- **Rules:** saving mid-conversation is refused (new or over), and a successful save clears unsaved play.
- **Row text:** "Loki - Medows, Summer 11, Year 1" and "Saved 27 Oct 2026, 14:32" from a
  summary, and the damaged-row wording.

---

## Depends on

- **Save system** ✅ built (steps 1–6; the live check is with the user). See [save-system-plan.md](save-system-plan.md).
