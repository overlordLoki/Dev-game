# NPC Memory, Tasks & LLM-Driven Behaviour — Plan

## Summary

NPCs should feel persistent and reactive: they remember past conversations, and
what the player says can change what they *do*. The design keeps the LLM at a
single, controllable choke point — **the end of a conversation** — where it
produces one structured `NpcDecision` (a summary, memories, a behaviour mode, and
a short task list). Between conversations the NPC runs a **deterministic task
queue** (go here, wait, go there) with no LLM in the per-frame loop. The LLM
*proposes*; the engine *validates and disposes*. This avoids a game clock, keeps
behaviour predictable, and reuses the async fire-and-poll pattern already proven
in `Conversation`.

---

## Where we are now (done)

- **Per-level NPCs** — `World._npcsByLevel` keeps each level's NPCs alive across
  transitions; `NPCs` points at the current level's bucket. `FindNpc(id)` searches
  all levels. NPC ids are session-unique via a monotonic counter.
- **Input seam** — all input reads through `AshenveilGame.Input` (`IInput`), so a
  `ScriptedInput` fake can drive the game in tests.
- **LLM dialogue** — `DialogueApi` (async `send`), `Conversation` holds the
  transcript, injects the greeting, seeds `history`, falls back cleanly when the
  API is down. NPCs carry `Persona` + `Greeting`.
- **Task system (plumbing)**
  - `ITask` = `State` + `Execute(TaskContext)`; `TaskContext` carries `Owner`,
    `Blackboard`, `DeltaTime`.
  - `NpcBrain.Update` is the fixed skeleton (Talking / Tasked / default); subclasses
    override `UpdateDefault` only, so the task machinery can't be shadowed.
  - `TickTasks` peeks the front task across many frames, dequeues on `Completed`,
    reverts to the default loop when the queue drains. One `Blackboard` per brain.
  - `Enqueue(task)` switches to `Tasked`; `StopTalking` resumes an interrupted errand.
  - Concrete tasks: `GoToTask(target)`, `WaitTask(seconds)`. `NPC.GiveTask(...)`
    passthrough for enqueuing from `World`.

---

## Phase 1 — Prove the task chain (no LLM)

**Goal:** watch a knight walk a route end to end before anything LLM touches it.

1. Temporary test in `SpawnLevelNpcs`: `GoTo well → Wait 3s → GoTo spawn`.
2. Verify: walks out, pauses, walks back, then drops into wandering (queue empty →
   `Walking`).
3. Confirm arrival threshold stops jitter, and a talk mid-route interrupts then
   resumes (`StopTalking` → `Tasked`).
4. Remove the throwaway lines once green.

**Done when:** the full chain (enqueue → Tasked → multi-tick Execute → complete →
revert) works deterministically.

---

## Phase 2 — The `NpcDecision` contract + Apply (still no LLM)

**Goal:** the engine can take a decision payload and act on it, tested with
hand-written payloads.

1. **DTO** (`Dialogue/` or `brains/`): `NpcDecisionDto { summary; memories[];
   behaviour; clear_current_tasks; tasks[] }`, `TaskSpecDto { type; target; seconds }`.
   Lowercase keys to match the Python JSON, same as `DialogueApi` DTOs.
2. **Apply** (on `NpcBrain` or a small applier) — deterministic, validated:
   - Write `summary` + capped/blank-filtered `memories` to the journal.
   - `clear_current_tasks` → clear the queue.
   - For each task spec: map `go_to`→`GoToTask` (only if the POI exists in the
     level), `wait`→`WaitTask` (clamp seconds); **drop anything invalid**.
   - `behaviour` → map the label to state via `SetBehaviour` (`resume`/`idle`/
     `do_tasks`). Never let the payload write raw `NPCState`.
   - Garbage payload degrades to "nothing changed."
3. **Test** by feeding fake `NpcDecisionDto`s (the two worked examples: ordinary
   chat → `resume`; "fetch water" → `do_tasks` with a 3-task queue). No Ollama.

**Done when:** a hand-written decision reliably reshapes the knight's behaviour and
journal, invalid input is dropped safely.

---

## Phase 3 — Journal as the single memory store

**Goal:** one memory system, wired into dialogue.

1. **Consolidate** `Memory` / `Memories` and `Journal` into *one* store — a
   `List<JournalEntry>` owned per NPC. Delete the duplicate. `JournalEntry` =
   `summary` + `facts[]` (+ source conversation / timestamp later).
2. **Inject** recent journal entries into the conversation request (via `extra` or
   `history`) on `StartConversation`, so the NPC "remembers" previous talks.
3. Still no planning LLM — Phase 2's Apply already writes entries on decision.

**Done when:** talking to a knight twice shows he recalls the first conversation.

---

## Phase 4 — Wire the LLM (the closing of the loop)

**Goal:** the decision comes from the model at conversation end.

1. **`main.py`**: new `POST /decision` (or extend summarise) returning the decision
   JSON, held to a `decision_schema(poi_names)` with `enum` on `behaviour`,
   task `type`, **and `target`** (the level's POIs, sent in the request). Mirror the
   existing `turns_schema` pattern.
2. **Request**: add `poi_names` (current level's POIs) so `target` can be an enum —
   the model can't route to a place that doesn't exist.
3. **Fire on `Conversation.End`**: async call via an injected
   `Func<..., Task<NpcDecisionDto>>` (same seam as `send`). Hold the task reference.
4. **Poll + apply on the main thread**: when the decision lands, call Apply. Expect
   a visible 1–2s "thinking" delay after the box closes — that's fine, even nice.
5. Fallback: API down / bad payload → no change (journal unchanged, behaviour
   `resume`).

**Done when:** asking the knight in-game to do something makes him do it, and he
remembers the exchange next time.

---

## Phase 5 — Later / optional

- **Persistence (disk)** — serialize each NPC's journal + current tasks, keyed by a
  **stable string id** (`"medows_knight"`), not the runtime int. Survives restart.
  This is really a save-system feature; design the journal as save data.
- **Returning greeting** — generated at decision time (nobody's waiting) and stored,
  so the next opener is instant *and* personalised. Keep the live greeting instant.
- **Disposition** — `friendly|neutral|wary|hostile` enum on the decision; the hook
  for the knight warming to / souring on the player.
- **Memory consolidation** — dedupe/merge journal facts (itself an LLM job) and
  relevance-select the top-N to inject, once the journal grows large.
- **More task types / behaviours** — `patrol`, `talk_to`, `follow`; each is one enum
  value + one `switch` arm. Grows by addition.
- **TaskContext as struct** — if NPC crowds appear, avoid per-tick heap allocation.

---

## Open decisions

- **Day/night clock?** Deliberately avoided (decision-driven, not time-driven). Only
  revisit if a time-of-day *Schedule* is genuinely wanted.
- **LLM task authority:** start **additive-biased** (add tasks + `clear`), no
  surgical delete/update by id. Widen only once outputs are trusted.
- **Naming:** keep concrete tasks specific (`GoToTask`, never a bare `Task`, which
  clashes with `System.Threading.Tasks.Task`). Consider renaming namespace
  `brains.Task` → `brains.Tasks`, and `Memorys` → `Memories`.

---

## Testing notes

- Task logic, Apply, and the decision loop are all testable **without Ollama** —
  feed fake `NpcDecisionDto`s and a fake `send`/decision func (the injected-seam
  trick). Build these in `Ashenveil.Tests`.
- The Python side gets its own `pytest` + FastAPI `TestClient`, mocking the Ollama
  call.
