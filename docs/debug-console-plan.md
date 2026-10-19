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

---

## Where things are now

- **Typed text** already works, but only inside `Chat.cs`: it subscribes to MonoGame's
  `Window.TextInput` event, keeps its own text, handles Backspace, and unsubscribes on close.
  Nothing else can reuse it.
- **Keyboard input** goes through `AshenveilGame.Input` (`IInput`), so key presses like the
  toggle key can be scripted in tests. `Window.TextInput` does **not** go through `IInput`.
- **Screens** are a stack (`ScreenManager`): only the top screen updates; all draw. The
  chat box is a screen pushed on top of `World`.
- **Debug output today** = `Console.WriteLine` and the `[DEBUG]` prints we used in
  `main.py` — neither is visible in-game.

---

## Decisions

1. **The console is a screen pushed on the stack**, like the chat box and pause menu. Open:
   the world stops updating underneath (the stack only updates the top). That's what you
   want for a dev console — nothing moves while you type.
2. **A reusable `TextField` widget, extracted from `Chat.cs`.** The console, the settings
   screen (backend URL) and the chat box all need typed text. Build it once:
   - Owns its text, cursor, focus, Backspace/Enter handling, and drawing.
   - Takes characters through a plain method (`Type(char)`), with the screen wiring
     `Window.TextInput` to it. That keeps it **testable**: a test calls `Type('a')` instead
     of needing a real window.
   - The chat box is switched over to it, so there's one implementation.
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

| Command | Does | Why it's useful now |
|---|---|---|
| `help` | Lists commands | Discoverable |
| `where` | Prints level, cell and pixel position | Essential for the world-units refactor |
| `tp <col> <row>` | Moves the player to a cell | Testing levels, POIs, exits |
| `level <name>` | Loads a level | Skip walking |
| `npcs` | Lists NPCs in all levels: id, name, level, state | Replaces reading debug prints |
| `journal <npc>` | Dumps an NPC's memories and transcripts | Checking the memory feature |
| `task <npc> go_to <poi>` / `wait <s>` | Gives an NPC a task | Testing the task system by hand |
| `time` / `time +<n>d` | Shows / skips game time | Phase 8 day-end reflection |
| `server` / `health` | Shows the backend URL / calls `/health` | Backend problems at a glance |
| `clear` | Clears the output | |

Commands that name an NPC should accept its id **or** its name, and say clearly when it's
not found.

---

## Steps

1. **`TextField` widget** — extract from `Chat.cs`; `Type(char)`, Backspace, Enter → an
   `OnSubmit` callback. Switch the chat box to it and confirm chat still works.
2. **`CommandRegistry`** — register/lookup, argument splitting (respect quotes:
   `journal "Sir Lancelot"`), unknown-command and bad-argument errors as `Error` lines.
3. **`ConsoleScreen`** — `TextField` at the bottom, output lines above, a dark translucent
   panel over the world. Toggle with `` ` `` (through `IInput`, edge-triggered). Escape
   also closes.
4. **Wire it in** — `AshenveilGame` pushes/pops it; only when dev mode is on.
5. **First commands** — start with `help`, `where`, `tp`, `npcs`, `journal`, `clear`; add
   the rest as you need them.
6. **History** — Up/Down through previous commands.

---

## Testing

- `TextField`: typing, Backspace, Enter firing `OnSubmit`, ignoring input when unfocused —
  all through `Type(char)` and fake key states, no window needed.
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

## Depends on / feeds into

- Needs: nothing new.
- Provides: `TextField` (used by the settings plan), `where`/`tp` (used to verify the
  world-units plan), a log the Phase 8 event log can feed.
