# Settings Menu — Plan

> **Status: complete (steps 1–7).** Step 8, window mode and volume, waits until it's needed.
> The save system reuses this plan's file approach: see [save-system-plan.md](../save-system-plan.md).

## Summary

Turn the settings screen from a lone Back button into real settings, starting with the one
that matters most for development: **the backend server address** (with a "Test connection"
button). Settings are **saved to a file**, so they survive a restart. That file is a small
rehearsal for the save system: same folder, same safe-write and versioning approach — but a
**separate file**, because settings belong to the machine, not to a playthrough.

---

## Status (2026-10-27)

Steps 1 to 7 are built; step 8 (window mode, volume) waits until it's needed.

- **`Config/`** (namespace `Ashenveil.Core.Config`):
  - `GameSettings`: `version`, `backendUrl`, `devMode`. Plain data; the initial values are the defaults.
  - `SettingsStore`: `Load()` never throws (missing, broken, `null` or wrong-typed file → defaults;
    unknown keys ignored; comments, trailing commas and any key case accepted; a bad stored URL →
    the default), and `Save()` writes `settings.json.tmp` then renames it over `settings.json`.
    `SettingsStore.DefaultFolder` is `%LOCALAPPDATA%\Ashenveil`.
  - `BackendUrl.TryNormalize`: adds a missing `http://`, drops a trailing slash, keeps a path,
    refuses non-http(s), `?`/`#` parts and empty input, with a reason.
  - `UiScale` (was `Utility/Settings.Scale`): computed from the window, never saved.
- **`AshenveilGame`** loads the settings in `Initialize` *before* `base.Initialize()` (which runs
  `LoadContent`), and has one `ApplySettings()` (backend URL, dev mode) plus `CommitSettings()`
  (apply, then save).
- **`DialogueApi`** builds the full URL per request from `DialogueApi.BaseUrl` (option 2 of
  decision 6), so a change takes effect on the next request. `UseHandler(HttpMessageHandler)` is
  the test seam. `CheckHealthAsync(url)` calls `/health` and returns a `HealthResult`:
  `Ok` / `ModelMissing` / `OllamaDown` (a 503) / `Unreachable` (incl. an 8 s timeout) / `Error`.
- **Settings screen**: Backend server field + Test connection (checks the *typed* address,
  answer shown underneath when it arrives), Dev mode checkbox, Back. Escape is Back. Laid out in
  `Open(w, h)`, called each time the menu shows it, so it fits after a resize.
- **`Debug.DEVMODE`** is set from the settings; `Debug.DevModeAllowed` is true in Debug builds
  and in Release only with `--dev` on the command line. The checkbox only appears where it's allowed.
- **Console**: `server [url]` shows or sets (and saves) the address; `health [url]` prints
  "Checking..." at once and the answer when it comes, via the new thread-safe
  `ConsoleScreen.WriteLater` (queued, written at the next `Update` or `Open`).
- **Tests**: `SettingsStoreTests`, `BackendUrlTests`, `DialogueApiTests` (fake handler),
  `BackendCommandsTests`, `SettingsScreenTests`, and two `WriteLater` tests in `ConsoleScreenTests`.

---

## Where things were (before this was built)

*(Refreshed after the debug console, world units and pathfinding landed.)*

- **`Screens/SettingsScreen.cs`** has one widget: a Back button. The menu pushes it; Back
  pops it.
- **`Utility/Settings.cs`** is a static class holding only `Scale`, which is computed from the
  window height (`UpdateScale`). Nothing in it is chosen by the user or saved. (When this
  feature is built, it moves out of `Utility/`, as agreed during the folder refactor.)
- **The backend address is hard-coded** in `Entities/brains/Dialogue/DialogueAPI.cs`:
  `private static readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:8000") };`
  Both LLM calls (`RequestAsync` for chat, `RequestDecisionAsync` for decisions) go through it.
- **The Python backend already has `GET /health`** (`TestAPI/main.py`), ready for a "Test
  connection" button.
- **The widgets exist:** `Button`, `Checkbox`, `Slider`, `Label`, `Panel`, `Image`,
  `ProgressBar`, `ChatLog`, and **`TextField`**. `TextField` is built and tested
  (`TextFieldTests`):
  - Typing reaches it through `IScreen.OnTextInput(char)`, which the `ScreenManager` sends to
    the top screen, so the settings screen needs no `Window.TextInput` wiring of its own.
  - `TextField.Update` unfocuses on a click outside it. That's what you want on a screen with
    several fields, unlike the console and chat, which force their single field to stay focused.
- **Dev mode is `Debug.DEVMODE`** (`Utility/Debug.cs`). It's on in Debug builds and off in
  Release, and the `` ` `` console only opens when it's on. It's a hard-coded flag waiting to
  become a setting.
- **The console** has `help`, `where`, `tp`, `tpxy`, `npcs`, `journal`, `nav`, `clear`. The
  plan's `server` / `health` commands aren't built yet; they belong with this feature.

---

## Decisions

1. **Split "computed" from "chosen".** `Scale` is computed from the window and isn't a
   setting. Chosen values go in a new **`GameSettings`** data class (plain data, like the
   level DTOs) that's loaded at startup and saved on change.
2. **Where the code goes:** a new folder, but **not named `Settings`**. A namespace called
   `Settings` would clash with the static `Settings` class, the same problem `UI.UI` and
   `World` had. Suggested: `Config/` with `GameSettings` and `SettingsStore`. The static
   `Settings.Scale` can move there too, or be renamed (e.g. `UiScale`) to stop the confusion.
3. **Saved to `%LOCALAPPDATA%\Ashenveil\settings.json`**
   (`Environment.SpecialFolder.LocalApplicationData`), not the install folder, which is
   read-only content (and not writable at all on Android).
4. **Safe write**: write `settings.json.tmp`, then replace, so a crash mid-write can't corrupt
   it. **`version` field** from day one. A missing or broken file means defaults, never a crash.
5. **Settings are applied, not just stored.** Each setting has a clear "apply" step (change
   the backend URL, set dev mode, set the window mode) that runs on load and on change.
6. **Changing the backend address at runtime.** `HttpClient.BaseAddress` can't change after
   the first request. Options:
   - rebuild the `HttpClient` when the URL changes (simplest), or
   - drop `BaseAddress` and build the full URL per request.

   Either way, `DialogueApi` reads the URL from settings instead of a constant.
7. **Make `DialogueApi` testable while in there.** Today its `HttpClient` is a private static
   with a fixed address. Letting a test hand it an `HttpMessageHandler` (or a base URL plus a
   handler) means "the next request goes to the new address" can be tested with no network.
   It's the same seam idea as the fake `sendChat` / `requestDecision` functions.

---

## What goes in it

| Setting | Widget | Notes |
|---|---|---|
| **Backend URL** | `TextField` + "Test connection" button | The button calls `/health` and shows the result: unreachable / reachable but model missing / OK (with the model name). |
| Dev mode | `Checkbox` | Replaces the hard-coded `Debug.DEVMODE`. Turns the `` ` `` console on or off. |
| Fullscreen / window size | `Checkbox` / buttons | Applying it triggers the existing resize path (`OnResize`). Positions are in cells now, so a resize is safe mid-game. |
| Master volume | `Slider` | Once there's audio. |
| Console toggle key | — | Later. Both US (`OemTilde`) and UK (`Oem8`) layouts already work. |

The backend URL is the reason to do this now; the rest can come one at a time.

---

## Steps

1. **`GameSettings` + `SettingsStore`**: load (defaults if missing or broken), save (temp file
   then replace), `version` field. Pure file logic, so testable with a temp folder.
2. **Load at startup** in `AshenveilGame.Initialize`, before anything uses the values.
3. **`DialogueApi` reads the URL** from settings; rebuild the client when it changes (and add
   the test seam from decision 7).
4. **Settings screen layout**: rows of label plus widget; Back saves.
5. **Backend URL field** (`TextField`) + **Test connection**, showing the `/health` result.
   The screen needs `OnTextInput` to pass characters to the focused field.
6. **Dev mode** checkbox, replacing `Debug.DEVMODE`.
7. **Console commands** `server` (show or set the URL) and `health` (call `/health`), sharing
   the code the settings screen uses.
8. Window mode and volume as they become relevant.

---

## Testing

- `SettingsStore`: round trip (save then load gives the same values); missing file gives
  defaults; corrupt file gives defaults; unknown or extra fields are ignored; an older
  `version` is handled.
- URL validation: empty, missing `http://`, a trailing slash. Normalise or reject clearly.
- `DialogueApi` URL switch: after changing settings, the next request goes to the new
  address (a fake handler, no network).
- `server` / `health` console commands against a fake, like `WorldCommandsTests`.
- The screen itself: by hand.

---

## Open questions (answered when built)

- **Apply immediately or on "Back"?** Both, by kind: the checkbox applies and saves the moment
  it's clicked; the URL only on Enter or Back, so a half-typed address is never used.
- **Validate the URL on save, or just let "Test connection" tell you?** Validate the *form* on
  save (an invalid address keeps you on the screen with the reason); whether anything answers
  there is Test connection's job, so an offline backend can still be saved.
- **Should the model name be a setting too?** Not yet: still `OLLAMA_MODEL` in `main.py`.
  Test connection shows which model the backend is using.
- **Should a Release build be able to turn dev mode on from settings?** Only when started with
  `--dev`; otherwise the checkbox isn't shown and a saved `devMode: true` is ignored.
- **Save failures**: shown on the screen (and in the console for `server`), never thrown. The
  change still applies for the session; on the screen, Back a second time leaves anyway.

## Depends on / feeds into

- **Needs:** nothing new. `TextField` and text routing are done (see
  [debug-console-plan.md](debug-console-plan.md)).
- **Provides:** the save-file folder, safe write and versioning approach that the
  [save system](../save-system-plan.md) reuses.
