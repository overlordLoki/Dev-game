# In-Game Debug Console — Plan

## Summary

A dev console you open with the backtick key (`` ` ``), type commands into, and read output
from — without leaving the game. It's a tool for building everything else: move the player,
inspect an NPC's memories, give tasks, skip time, test the backend connection. It comes
first because the next features (settings, world units, saving, the Phase 8 event log) are
all easier to build and check with it.

Two design rules: commands live in a **registry** (name + help + handler, not one giant
`switch`), and the output is a **log of entries**, not just text on screen — so the Phase 8
event log can feed into it later.

> **Status: v1 built.** Steps 1–6 are done: press `` ` `` in the world to open it. The
> first six commands work (`help`, `where`, `tp`, `npcs`, `journal`, `clear`) and the rest
> of the table below is still to do. See "What was built" at the end.

---

## Where things were before it was built

- **`TextField` widget ✅ done** (`UI/Widgets/TextField.cs`). It's a one-line text box:
  - `Type(char)` handles `'\r'` (submit) and `'\b'` (Backspace), and ignores other control
    characters and anything past `maxLength`.
  - Enter calls `onSubmit` but doesn't clear the text: the owner decides.
  - Click to focus, plus a placeholder, a blinking cursor, and long text showing its end.
  - `TextFieldTests` cover it, and the chat box uses it.
- **Typed text is routed by the `ScreenManager` ✅ done.**
  - `AshenveilGame` subscribes to `Window.TextInput` once and passes each character to
    `ScreenManager.TextInput`.
  - That hands it to the **top screen's** `IScreen.OnTextInput(char)`. The default does
    nothing, so screens that don't type don't change.
  - No screen subscribes or unsubscribes itself any more. The console just overrides
    `OnTextInput` and passes the character to its field.
- **Unknown characters can't crash drawing**: the font's `DefaultCharacter = '?'` is set
  when it loads.
- **Keyboard input** goes through `AshenveilGame.Input` (`IInput`), so key presses like the
  toggle key can be scripted in tests. `Window.TextInput` does **not** go through `IInput`;
  tests call `Type(char)` / `OnTextInput(char)` directly instead.
- **Screens** are a stack (`ScreenManager`): only the top screen updates and gets typing;
  every screen draws. The chat box is a screen pushed on top of `World`.
- **The chat box's message log** (`ChatScreen.DrawLog`: word-wrap cache, newest at the
  bottom, coloured names, grey game notes) is still private to `Chat.cs`. The console needs
  the same thing for its output (see step 3).
- **Debug output today** = `Console.WriteLine` and the `[DEBUG]` prints we used in
  `main.py` — neither is visible in-game.

---

## Decisions

1. **The console is a screen pushed on the stack**, like the chat box and pause menu. Open:
   the world stops updating underneath (the stack only updates the top). That's what you
   want for a dev console — nothing moves while you type.
2. **A reusable `TextField` widget ✅ done.** The console, the settings screen (backend
   URL) and the chat box all need typed text, so it's built once. It takes characters
   through `Type(char)`, so it's testable without a window. The chat box uses it.
   - **A screen with one field keeps it focused.** `TextField.Update` unfocuses on any
     click outside it, so a click on the console's output would stop typing. Do what
     `Chat.cs` does: set `_field.Focused = true` after the widget updates.
3. **Command registry.** Each command: `Name`, `Help`, `Run(args) → output lines`.
   `help` lists them from the registry automatically. Adding a command = one registration.
4. **Output is a list of `ConsoleLine { Text, Kind (Input | Output | Error | Event) }`**,
   capped (say the last 200). Drawn newest at the bottom, scrollable later.
5. **Dev-only, behind a flag.** Off in a release build or when "dev mode" is off in settings
   (see the settings plan). The toggle key does nothing when it's off.
6. **Command history**: Up/Down cycles previous commands. Cheap, and it's the thing you
   miss first.

---

## First commands

| Command | Does | Why it's useful now | |
|---|---|---|---|
| `help` | Lists commands | Discoverable | ✅ |
| `where [npc]` | Prints the level, cell and x,y of the feet (in cells): the player's, or an NPC's by id/name | Essential for the world-units refactor; checking where NPCs stand | ✅ |
| `tp <col> <row>` | Stands the player in the middle of a cell | Testing levels, POIs, exits | ✅ |
| `tpxy <x> <y>` | Puts the player's feet on an exact x,y in cells (`tpxy 3.25 4.5`) | Lining up against box edges and doors | ✅ |
| `level <name>` | Loads a level | Skip walking | |
| `npcs` | Lists NPCs in all levels: id, name, level, state | Replaces reading debug prints | ✅ |
| `journal <npc>` | Dumps an NPC's memories and transcripts | Checking the memory feature | ✅ |
| `task <npc> go_to <poi>` / `wait <s>` | Gives an NPC a task | Testing the task system by hand | |
| `time` / `time +<n>d` | Shows / skips game time | Phase 8 day-end reflection | |
| `server` / `health` | Shows the backend URL / calls `/health` | Backend problems at a glance | |
| `nav` | Toggles drawing of blocked cells and NPC paths | Pathfinding plan, once it lands | |
| `clear` | Clears the output | | ✅ |

Commands that name an NPC should accept its id **or** its name, and say clearly when it's
not found.

---

## Steps

All six steps are ✅ done. What each turned into is listed under "What was built" below.

1. ~~**`TextField` widget**~~ ✅ done, along with typing routed through `IScreen.OnTextInput`.
2. **`CommandRegistry`** — register/lookup, argument splitting (respect quotes:
   `journal "Sir Lancelot"`), unknown-command and bad-argument errors as `Error` lines.
   Pure logic, so it can be built and tested before there's a screen.
3. **`ConsoleScreen`** — `TextField` at the bottom, output lines above, a dark translucent
   panel over the world. Toggle with `` ` `` (through `IInput`, edge-triggered). Escape
   also closes. Typing arrives through `OnTextInput(char)`; there's nothing to subscribe.
   - **The toggle key leaks into the field.** It's the same problem `Chat.cs` has with E: the
     `` ` `` that opened the console also arrives as a typed character. Either reuse the
     `_waitForRelease` idea, or just ignore `` ` `` in `OnTextInput`, since it's never part of
     a command. Once two screens need this, consider moving it into the `ScreenManager`
     (drop typing for the first frame after a push).
   - **Share the log drawing with the chat box.** Pull `ChatScreen.DrawLog` out into a
     `ChatLog` widget (lines in, newest at the bottom, word-wrapped and coloured), and use it
     here for `ConsoleLine`s. Decide its shape now that there are two users, not before.
4. **Wire it in** — `AshenveilGame` pushes/pops it; only when dev mode is on.
5. **First commands** — start with `help`, `where`, `tp`, `npcs`, `journal`, `clear`; add
   the rest as you need them.
6. **History** — Up/Down through previous commands.

---

## Testing

- `TextField` ✅ done (`TextFieldTests`, 16 tests): typing, Backspace, the max length, Enter
  firing `onSubmit` without clearing, ignoring input when unfocused or hidden, and focus by click.
- `ConsoleScreen.OnTextInput`: the toggle key isn't typed; other characters reach the field.
  It can be called directly, with no window.
- `CommandRegistry`: parsing (quotes, extra spaces), unknown command, wrong argument count.
- Individual commands: most just read/modify game state, so they can be tested against a
  small fake world or the existing test helpers (`TestNpc`, `TestLevels`).
- The console screen itself: by hand.

---

## Open questions

- **Toggle key:** backtick is standard but awkward on some keyboard layouts. Make it
  configurable in settings?
- **Opening while chatting:** allow the console over the chat box, or only over the world?
  (Simplest: only from the world.)
- **Mirror to the terminal?** Also write console output to `Console.WriteLine`, so it's in
  the terminal after a crash.

## What was built

- **`DevConsole/CommandRegistry.cs`**: `Command`, `ConsoleLine` + `LineKind`, `CommandException`.
  - `Register(name, usage, help, run, minArgs, maxArgs)`; names ignore case; registering a
    name twice throws.
  - `Execute(line)` **never throws**:
    - an unknown command, the wrong number of arguments (shows the usage), or a
      `CommandException` each come back as an `Error` line;
    - any other exception comes back as "'x' failed: ...", including one thrown from a lazy
      `yield return` handler.
  - `Split` handles quotes (`journal "Sir Lancelot"`), extra spaces and an unclosed quote.
  - `help` is built in.
- **`DevConsole/WorldCommands.cs`**: `where`, `tp`, `npcs`, `journal`. They're given only
  the `World`, so they're tested against a test level.
  - `FindNpc` takes an id or a name, ignoring case. When there's no match, or several, it
    says what to try.
  - `where` measures the feet, like doors do.
- **`UI/Widgets/ChatLog.cs`**: the chat box's message drawing, pulled out and shared.
  - Newest at the bottom, word-wrapped and cached, with an optional coloured name per entry.
  - An optional cap on how many entries are kept.
  - A `Pending` entry: the chat box's "...", drawn without being kept.
  - `Printable` moved here from `Chat.cs`, and now has tests.
  - `Chat.cs` now uses it, copying new `Conversation.Messages` in as they arrive.
- **`Screens/ConsoleScreen.cs`**: a translucent panel on the top 45% of the screen, with
  the output above a `TextField`.
  - **Kept for the whole game**, so its output and history survive closing it. `Open(w, h)`
    re-fits it to the window and resets the key state.
  - **Typing:** `OnTextInput` drops `` ` ``, `~` and `¬`, so the key that opens it is never typed.
  - **Keys:** Up/Down go through history (no repeats in a row); Escape closes.
  - **Output** is capped at 200 lines.
  - `clear` is registered by the screen, since it's about the screen's own output.
- **`AshenveilGame`**:
  - builds the registry plus `WorldCommands` plus the screen once;
  - `` ` `` toggles it, only over the world, and only when `Debug.DEVMODE` is on;
  - the toggle is `OemTilde` (US layout) or `Oem8` (UK layout).
- **`Debug.DEVMODE`**: on in Debug builds, off in Release. Becomes a settings toggle later.
- **Small additions for the commands:**
  - `NPC.State` and `NPC.Journal`, read-only views of the brain;
  - `NpcRoster.LevelOf(npc)`.
- **Tests:** `CommandRegistryTests`, `WorldCommandsTests`, `ConsoleScreenTests`, `ChatLogTests`.

**Answers to the open questions, for now:**
- **Opening while chatting:** only from the world.
- **Mirroring to the terminal:** not done.
- **A configurable toggle key:** not done. Both common layouts work.

## Depends on / feeds into

- Needs: nothing new.
- Provides: `where`/`tp` (used to verify the world-units plan), `nav` (pathfinding plan),
  a `ChatLog` widget shared with the chat box, and a log the Phase 8 event log can feed.
  (`TextField`, used by the settings plan, already exists.)
