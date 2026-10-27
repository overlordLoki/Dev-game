# NPC Memory & Behaviour, Next — Plan

## Summary

The first NPC memory plan is done. Phases 1–6 are in
[Completed/npc-memory-plan.md](Completed/npc-memory-plan.md): NPCs run a task queue,
the LLM makes one structured decision at the end of each conversation, and the knight
remembers facts and past conversations. This plan is what comes next, in two parts:

- **Part A** is a list of independent extensions, pick-as-needed: NPCs starting
  conversations, more task types, disposition, memory consolidation, and tool-calling recall.
- **Part B** is the event log and daily reflection: NPCs learning from what *happens*,
  not just from what's *said*.

The ground rule from the first plan still holds. The LLM *proposes* at a few controlled
points; the engine *validates and disposes*; there's no LLM in the per-frame loop.

---

## Where things are now

*(For the details, see the completed plan. Names below are the current ones.)*

- **Conversations** live in `GameWorld/ConversationSystem.cs`:
  - `Start` builds the request, with each NPC's memories and past conversations with that
    person.
  - When a chat ends, it records one shared `Transcript` and asks each NPC what it learned.
  - Arriving decisions go to `NPC.ApplyDecision`.
- **NPCs** are in `GameWorld/NpcRoster.cs`, per level, and kept across level changes.
- **Memory** is the `Journal`: the hot tier is `Memory { Text, Kind, SourceId }`, the cold
  tier is `Transcript { Id, Day, Location, Participants, Lines, Summary }`.
- **Tasks:** `GoToTask` (walks the **feet** to a POI's `StandingSpot()`) and `WaitTask`.
  `NpcBrain.ApplyDecision` turns `go_to` / `wait` specs into tasks.
- **Pathfinding is done** ([Completed/pathfinding-plan.md](Completed/pathfinding-plan.md)):
  - `GoToTask` plans an A\* route round objects on each level's `Location.Nav` grid and smooths it.
  - It replans when stuck, and **fails** (`TaskState.Failed`) when there's no way through.
  - NPCs know their level (`NPC.Location`, set by `NpcRoster.Add`).
  - `nav` in the console shows the routes.
- **Failures aren't remembered yet:** there's a TODO in `NpcBrain.TickTasks` waiting for the
  event log (Part B).
- **No fixed greeting:** whoever starts a conversation speaks first. A player-started chat
  opens with only a grey game note.
- **Time:** `GameWorld/GameClock.cs` raises `DayStarted` when a day turns over (10 real
  minutes per day).
- **The dev console** can already inspect all of this: `npcs`, `journal <npc>`,
  `where <npc>`.

---

## Part A — Extensions (independent, pick as needed)

### A1. NPC-started conversations
A player-started chat opens empty, and the LLM's first line answers the player. When an
NPC walks up to the player, it should open with something it **has in mind**, and that
comes from whatever sent it over:
1. The decision LLM queues a `talk_to { target, reason }` task (needs A2).
2. On arrival, the task starts the conversation with the reason.
3. The conversation asks the LLM for the opening line straight away: "You approached the
   Traveller to: warn them about the wolves. Open the conversation."
4. Python side: an empty history plus a reason means *open*, not *reply*.
5. `ConversationSystem.Start` already has the shape for it: it knows who started.

### A2. More task types
`patrol`, `talk_to`, `follow`. Each is one enum value in the decision schema (`main.py`) plus
one `switch` arm in `NpcBrain.ApplyDecision`, plus its `ITask` class. Pathfinding is done, so
the walking ones can reuse it:
- **`patrol`:** a loop of `GoToTask`s between POIs.
- **`talk_to`:** walk to the target with `Pathfinder` (it ends next to a blocked goal, so it
  works for a person standing somewhere awkward), then start the conversation (A1).
- **`follow`:** replan towards a moving target every second or so, like the stuck check does.
- Each should fail cleanly (`TaskState.Failed`) when there's no route, like `GoToTask`.

### A3. Disposition
**Replaced by [factions-plan.md](factions-plan.md).** Disposition became an opinion score
per person and per faction, changed at day end by reflection (Part B), and injected into the
conversation prompt as an attitude line.

### A4. Memory consolidation
Dedupe and merge facts (an LLM job), and pick the most relevant N to inject, once journals
grow large. Reworded duplicates currently slip past the exact-match check in
`ApplyDecision`. Needs `Memory.Day` (B6) for recency.

### A5. LLM tool-calling recall
`recall(query)` as a real tool, once there's a tool-capable model. It takes several round
trips, so it's best on the decision step, not live dialogue. The cold store already has what
it would search.

### A6. Neutral summaries for group chats
Summaries are written first-person by the NPC ("I warned Loki..."). In a shared transcript
the other NPCs inherit that "I". Fix: ask the decision prompt for a neutral, third-person
summary. Only matters once NPCs chat in groups.

### A7. Small things
- **Hidden thinking** (`think: True`) on the decision step, to compare fact extraction quality.
  Keep it off for live dialogue, where it's just latency.
- **`TaskContext` as a struct**, if NPC crowds appear (avoids a heap allocation per tick).
- **Persistence** is done ([Completed/save-system-plan.md](Completed/save-system-plan.md)):
  journals and tasks keyed by a stable string id (`"medows_knight"`), not the runtime int.
  New memory data (events, `Memory.Day`) needs a schema migration.

---

## Part B — Event log & daily reflection

**Goal:** NPCs learn from the world, not just conversations.

**Build this before combat and factions.** Fights are stored as events. Same day, they're
hot memories; at day end, reflection writes the lasting after-effects (opinions, including
of whole factions). See [factions-plan.md](factions-plan.md) and [combat-plan.md](combat-plan.md).

1. **Structured `EventLog`**: `GameEvent { Day, Location, Type, ActorId, TargetId, Text }`
   ("Loki killed 2 zombies at the well"). Game systems post to it.
   - **Ids, not just text:** the engine reads events in code (today's grudges, who to fight),
     so who did what to whom must be data. `Text` is for prompts and the console.
   - **`Type`** is a small enum that grows: `Attacked`, `Killed`, `TaskFailed`, `Joined`...
   - Build the data first, not console text.
   - **The console is one consumer:** `ConsoleScreen.Write(text, LineKind.Event)` already
     exists for this.
   - **NPCs are another.**
2. **Perception:** each NPC only receives events near it (same location, maybe a range).
   Without this, every NPC knows everything, which breaks the illusion.
3. **Hot during the day:** an NPC's recently witnessed events flow into the same framed
   memory channel as facts (`MemoryKind.Event`). No LLM call per event.
4. **Cold at day end:** on `GameClock.DayStarted`, each NPC makes one reflection call that
   summarises the day's events into the cold store, as a daily entry alongside transcripts.
   - It also returns **opinion changes** about the people and factions met that day. The
     engine validates and applies them ([factions-plan.md](factions-plan.md), step 5).
   - Build the summary first; opinion changes come with the factions work.
5. **Stagger the day-end calls:** every NPC wants a call at once, so queue them through
   fire-and-poll with one or two in flight, not a stampede. (`ConversationSystem`'s pending
   list is the pattern.)
6. **`Memory.Day`:** events give memories a real timestamp. Add it here; it also unlocks
   recency and decay for consolidation (A4).
7. **Failed tasks become events:** `NpcBrain.TickTasks` has a TODO for exactly this
   ("couldn't reach the well"). An NPC then knows it failed, and can say so next time you talk.

**Console commands that help here:**
- `time` / `time +<n>d` to skip to day end;
- `events` to list the log.

Both are on the console plan's to-do list.

---

## Open decisions

- **Day/night clock?** There's a day counter (`GameClock`) but no time-of-day schedule. Only
  add one if a time-of-day *Schedule* is genuinely wanted; behaviour stays decision-driven.
- **LLM task authority:** start **additive-biased** (add tasks, plus `clear`), with no
  surgical delete or update by id. Widen only once the outputs are trusted.
- **Naming:**
  - Keep concrete tasks specific (`GoToTask`, never a bare `Task`, which clashes with
    `System.Threading.Tasks.Task`).
  - Consider renaming the namespaces `brains.Task` → `brains.Tasks` and `Memorys` → `Memories`.

---

## Testing

Same approach as the completed plan (see its "Testing notes"):
- **Fakes for both LLM calls**, so the decision loop and new tasks are tested without Ollama.
- **pytest** for the Python side.
- **The LLM judge** (`pytest -m judge`) for live checks about *meaning*.
- **Event log:** perception (who hears what), and that day-end calls are staggered. Both are
  testable with a fake clock and a fake reflection call, no LLM needed.
