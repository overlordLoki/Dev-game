# Settings Menu — Plan

## Summary

Turn the settings screen from a lone Back button into real settings, starting with the one
that matters most for development: **the backend server address** (with a "Test connection"
button). Settings are **saved to a file**, so they survive a restart. That file is a small
rehearsal for the save system: same folder, same safe-write and versioning approach — but a
**separate file**, because settings belong to the machine, not to a playthrough.

---

## Where things are now

- `SettingsScreen` has one widget: a Back button.
- `Settings` (static) only holds `Scale`, computed from the window height. Nothing is
  user-set or saved.
- The backend address is **hard-coded** in `DialogueApi`:
  `new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8000") }`.
- Widgets available: `Button`, `Checkbox`, `Slider`, `Label`, `Panel`, `Image`,
  `ProgressBar`. Missing: a **text field** (coming from the debug-console plan).

---

## Decisions

1. **Split "computed" from "chosen".** `Scale` is computed from the window and isn't a
   setting. Chosen values go in a new **`GameSettings`** data class (plain data, like the
   level DTOs) that's loaded at startup and saved on change.
2. **Saved to `%LOCALAPPDATA%\Ashenveil\settings.json`**
   (`Environment.SpecialFolder.LocalApplicationData`) — not the install folder, which is
   read-only content (and not writable at all on Android).
3. **Safe write**: write `settings.json.tmp`, then replace — a crash mid-write can't corrupt
   it. **`version` field** from day one. A missing or broken file → defaults, never a crash.
4. **Settings are applied, not just stored.** Each setting has a clear "apply" step (change
   the backend URL, set the window mode, set volume) that runs on load and on change.
5. **Changing the backend address at runtime.** `HttpClient.BaseAddress` can't change after
   the first request. Options:
   - rebuild the `HttpClient` when the URL changes (simplest), or
   - drop `BaseAddress` and build the full URL per request.
   Either way `DialogueApi` reads the URL from settings instead of a constant.

---

## What goes in it

| Setting | Widget | Notes |
|---|---|---|
| **Backend URL** | `TextField` + "Test connection" button | The button calls `/health` and shows the result: unreachable / reachable but model missing / OK (with the model name). |
| Dev mode | `Checkbox` | Turns the debug console on/off. |
| Fullscreen / window size | `Checkbox` / buttons | Applying it triggers the existing resize path (`OnResize`). |
| Master volume | `Slider` | Once there's audio. |
| Console toggle key | — | Later, if backtick is awkward on your keyboard. |

The backend URL is the reason to do this now; the rest can come one at a time.

---

## Steps

1. **`GameSettings` + `SettingsStore`** — load (defaults if missing/broken), save (temp file
   then replace), `version` field.
2. **Load at startup** in `AshenveilGame.Initialize`, before anything uses the values.
3. **`DialogueApi` reads the URL** from settings; rebuild the client when it changes.
4. **Settings screen layout** — rows of label + widget; Back saves.
5. **Backend URL field** (needs `TextField` from the console plan) + **Test connection**
   showing the `/health` result.
6. **Dev mode** checkbox → console availability.
7. Window mode and volume as they become relevant.

---

## Testing

- `SettingsStore`: round-trip (save then load gives the same values), missing file →
  defaults, corrupt file → defaults, unknown/extra fields ignored, older `version` handled.
- URL validation: empty, missing `http://`, trailing slash — normalise or reject clearly.
- `DialogueApi` URL switch: after changing settings, the next request goes to the new
  address (test against a fake handler, no network).

---

## Open questions

- **Apply immediately or on "Back"?** Immediate is nicer for sliders; the URL probably only
  on confirm.
- **Validate the URL on save, or just let "Test connection" tell you?**
- **Should the model name be a setting too?** It's chosen server-side (`OLLAMA_MODEL` in
  `main.py`); exposing it means a new endpoint. Probably not yet.

## Depends on / feeds into

- Needs: `TextField` from the debug-console plan.
- Provides: the save-file folder, safe write and versioning approach the save system reuses.
