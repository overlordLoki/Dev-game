# NPC Memory, Tasks & LLM-Driven Behaviour — Plan

> **Status: complete (Phases 1–6).** Kept as a record of how the feature was built. Some
> names have moved since: `World` was split into `ConversationSystem`, `NpcRoster` and
> `GameClock`; the fixed `Greeting` was removed; positions are in cells. What comes next is
> in [npc-memory-next-plan.md](../npc-memory-next-plan.md).

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

> **Status (built so far):** Phases 1–6 are implemented and test-covered (tasks,
> `NpcDecisionDto` + `ApplyDecision`, the `/decision` endpoint, fire-and-poll on
> conversation end, facts carried into the next conversation, and a cold transcript store
> with summaries recalled into later chats). Verified live: the knight remembers your name,
> and what you talked about last time. Next candidates are in Phase 7 (later list) and
> Phase 8 (event log), now in [npc-memory-next-plan.md](../npc-memory-next-plan.md).

---

## Memory architecture — tiered (the shape to build toward)

Memory is **tiered**, because you can't put every past conversation in every prompt
(context, latency, noise):

- **Hot tier** — discrete `Memory` objects: facts ("the traveller is Loki"), summaries,
  and later events ("killed 2 zombies by the well"). Small, always available to inject.
- **Cold tier** — full conversation transcripts, stored but *not* in every prompt.
  Retrieved only when detail is actually needed.

**Recall has two mechanisms, and we do the cheap one first:**
- **Heuristic retrieval (now)** — *our code* picks which hot memories (and optionally a
  cold transcript) are relevant and injects them. No tool-calling, works on any model.
- **LLM tool-calling (later milestone)** — the model calls `recall(query)` itself,
  mid-generation, to pull from the cold store. Powerful, but gated on a tool-capable
  model (gemma4:e4b is unreliable at it) and multi-round-trip latency — best suited to
  the decision step, not live dialogue. The data model below is the same either way, so
  deferring the tool costs nothing: a future tool queries the exact store we build now.

**Hidden thinking** — Ollama's `think` flag lets the model reason in a field we don't
display. Cheap win on the **decision step** (may improve fact extraction); keep it
**off** for live dialogue, where it's just latency.

`Memory` is the unit of the hot tier: `{ Text, Kind (Conversation | Event | …) }`,
extensible so conversation facts and world events flow through the same pipe.

---

## Phase 5 — Facts & framed memory ✅ done

**Goal:** the knight actually uses what he remembers. **Verified live.**

1. **`Memory` class** (`{ Text, Kind, SourceId? }`) + `MemoryKind { Conversation, Event }`.
   `Journal` holds a `List<Memory>`. `ApplyDecision` turns each extracted fact into a
   `Memory(fact, Conversation)`; `RecentMemories` returns `Memory.Text`.
2. **Decision prompt** insists on discrete facts in `memories` (names, promises,
   requests) and says to record only what's *new* — never the NPC's own background,
   persona or greeting (those were crowding real facts out).
3. **Memories framed per participant** — `ParticipantDto.memories`, rendered by
   `build_prompt` under "What {name} remembers from before:", ahead of "Already said:".
   Per-participant so group conversations keep each NPC's knowledge attributed.
4. **System-prompt nudge** — characters use what they remember and never pretend not to.
5. **Duplicate check** in `ApplyDecision` — a fact already in the journal (case- and
   edge-whitespace-insensitive) isn't stored again, so repeats don't push real memories
   out of the 5-memory injection window. Reworded duplicates still slip through
   (consolidation, Phase 6).
6. **No decision for an empty chat** — `FireDecision` skips conversations where nobody but
   the NPCs spoke; otherwise the model invents filler ("the traveller was present").

Not done (still optional): `think: True` on the decision step to compare extraction quality.

---

## Phase 6 — Transcript store & recall ✅ done

**Goal:** the NPC keeps every conversation it was in, and opens a new one remembering
what you talked about last time - not just isolated facts.

**Decisions (agreed):**
- **Recall level (a):** inject short summaries of past conversations *with the same
  person*. Full-transcript retrieval comes later via embeddings (see the next plan), not
  keyword triggers.
- **One shared `Transcript` per conversation**, the same object added to each
  participating NPC's journal. Only participants get it (built-in perception).
- **The decision `summary` moves onto the transcript** instead of being thrown away.
- **Player identity stays the name `"Traveller"`** for now; a stable player id comes with
  the save system.

**Steps:**
1. **`Transcript`** — `{ Id, Day, Location, Participants, Lines, Summary }`, with a
   constructor. `Summary` is filled in later (it arrives with the decision).
   **`Journal`** — both tiers wrapped the same way (private list, `IReadOnlyList`, `Add`)
   and labelled: class comment explains hot vs cold, each field says which it is.
2. **`Conversation.ToTranscript(id, day)`** — pure conversion of the conversation's data,
   so it's unit-testable without a `World`.
3. **`World`** — a monotonic transcript-id counter. `EndFinishedConversations` runs the
   empty-chat guard, builds **one** transcript (today's `TotalDays`), adds it to each NPC's
   journal (via an `NPC` passthrough, the brain is protected), then fires a decision per
   NPC. The pending list carries the transcript.
4. **`ApplyDecision`** takes the transcript: sets `transcript.Summary`, and stamps
   `SourceId` on each new fact so it traces back to its conversation.
5. **Recall** — `NPC` returns its last few transcript summaries with a given person
   (`"Day 3, Medows: ..."`, newest first, skipping transcripts with no summary, e.g. when
   the API was down). `ParticipantDto` / Python `Participant` gain `past_conversations`;
   `build_prompt` renders them under their own heading next to "remembers from before".
6. **Tests** — `ToTranscript`, `ApplyDecision` setting summary + `SourceId`, the recall
   selection, and Python prompt rendering. The live recall check is graded by the
   **LLM judge** (see Testing notes), since the model paraphrases.

**As built, beyond the steps above:**
- The empty-chat guard moved into `World.RecordConversation`: no transcript and no decision
  when nobody but the NPCs spoke.
- In a shared transcript the **first** NPC's summary sticks; later ones don't overwrite it.
- Recall is worded relative to today ("Yesterday, at Medows: ..."), since the model doesn't
  know the current day number. Last 3 conversations with that person, oldest first.
- Old `Conversation.Summarize()` / `World.SummarizeConversations()` removed (replaced by
  `ToTranscript`).

**Known limitation:** summaries are written first-person by the NPC ("I warned Loki..."). In
a group chat the other NPC inherits that "I". Fix if group chats matter: ask the decision
prompt for a neutral third-person summary.
---

## What's next

Phases 7 (the "later" list) and 8 (the event log and daily reflection), plus the open
decisions, moved to their own plan: [npc-memory-next-plan.md](../npc-memory-next-plan.md).

---

## Testing notes

- Task logic, Apply, and the decision loop are all testable **without Ollama** —
  feed fake `NpcDecisionDto`s and a fake `send`/decision func (the injected-seam
  trick). Build these in `Ashenveil.Tests`.
- The Python side gets its own `pytest` + FastAPI `TestClient`, mocking the Ollama
  call.
- **LLM judge** (`TestAPI/judge.py`) — for live tests about *meaning*, a stronger model (the
  Claude CLI, Sonnet by default) grades the local model's reply against a specific expectation
  ("does it recall the sword conversation?"). Paraphrase passes; dodging or contradicting fails.
  - Dev-only; the game never uses it. Marked `judge` in pytest: `pytest -m judge` /
    `pytest -m "not judge"`. Skips if the CLI isn't found or pytest runs inside a Claude Code
    session (the CLI refuses to nest).
  - **Calibrated**: `test_judge_grades_known_replies_correctly` feeds it hand-written replies with
    known verdicts (right, paraphrase, wrong topic, dodge, confidently wrong). If those fail, the
    judge can't be trusted. Re-run them whenever `JUDGE_MODEL` changes.
  - Cost: each call carries the CLI's agent context, so it's cents per test - fine occasionally,
    don't run it in a tight loop.
  - Use it for meaning, not exact facts: a remembered *name* is still a plain keyword check.
