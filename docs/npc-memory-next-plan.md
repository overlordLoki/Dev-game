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

**Part B is built** (2026-10-28), and so is [factions](factions-plan.md), which builds on it
(opinions are written by the day-end reflection). [Combat](combat-plan.md) is next: fights are
stored as events, and their lasting after-effects go through the same reflection. Part A stays
open, so this plan stays out of `Completed/`. A2 (`follow`, `talk_to`) is worth doing before
combat step 3, and A1 after combat.

The ground rule from the first plan still holds. The LLM *proposes* at a few controlled
points; the engine *validates and disposes*; there's no LLM in the per-frame loop.

---

## Where things are now

*(For the details, see the completed plan. Refreshed 2026-10-28, after saving was built.)*

- **Conversations** live in `GameWorld/ConversationSystem.cs`:
  - `Start` builds the request, with each NPC's memories and past conversations with that
    person.
  - When a chat ends, it records one shared `Transcript` and asks each NPC what it learned.
  - Arriving decisions go to `NPC.ApplyDecision`.
  - The pending-decision list (fire the call, hold the `Task`, pick up the answer on a later
    frame) is the pattern for day-end reflection.
- **NPCs** are in `GameWorld/NpcRoster.cs`, per level, and kept across level changes.
- **People are known by int id:** players and NPCs share one counter
  (`NpcRoster.NextId`, saved as `NextPersonId`), and ids are saved, so they're stable across
  save and load. The player is a `Name` that can change; recall goes by id.
- **Memory** is the `Journal`:
  - the hot tier is `Memory { Text, Kind, SourceId }`, where `SourceId` is a transcript id;
  - the cold tier is `Transcript { Id, Day, Location, Participants, Lines, Summary }`;
  - `MemoryKind` already has `Event`, but nothing creates one.
  - **Memories have no day.** Transcripts do; facts don't.
  - `RecentMemories(5)` takes the last five facts of any kind.
- **Tasks:** `GoToTask` (walks the **feet** to a POI's `StandingSpot()`, pathfinding round
  objects) and `WaitTask`. `NpcBrain.ApplyDecision` turns `go_to` / `wait` specs into tasks.
  A task that can't get there **fails** (`TaskState.Failed`) and is dropped; there's a TODO in
  `NpcBrain.TickTasks` waiting for the event log.
- **No fixed greeting:** whoever starts a conversation speaks first. A player-started chat
  opens with only a grey game note.
- **Time:** `GameClock` (10 real minutes a day) raises `DayStarted` with the new day number.
  **Nothing subscribes to it yet.** `Restore` sets a loaded time without raising it.
- **Saving is built** ([Completed/save-system-plan.md](Completed/save-system-plan.md), slots in
  [Completed/save-slots-plan.md](Completed/save-slots-plan.md)):
  - `SaveMapper` (game ↔ save classes) and `SaveStore` (save classes ↔ SQLite);
  - schema v1 has shipped. `SaveStore.Migrations` is empty and waiting for v2: anything this
    plan adds to a save is the first real migration;
  - decisions waiting on the LLM aren't saved; they're lost, by design.
- **The dev console** has `where`, `tp`, `tpxy`, `npcs`, `journal <npc>`, `nav`, `server`,
  `health`, `saves`, `save` and `load`. `LineKind.Event` exists for lines the game reports by
  itself, and is unused so far. There's no `time` or `events` yet.
- **Python** (`TestAPI/main.py`) has `/conversation` and `/decision`, with pytest and the LLM
  judge (`judge.py`).

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
  journals and tasks are saved per NPC, under saved int ids that stay stable across loads.
  Part B's new data is the first schema migration (v2).

---

## Part B — Event log & daily reflection

> **Status (2026-10-28): built**, steps 1–7, with these small departures from the text below:
> - `GameEvent` has an optional `TranscriptId` (and `events` a `transcript_id` column), so a
>   `Talked` event really does point at its transcript.
> - Reflected facts are `MemoryKind.Reflection` (a fact like any other, but traceable).
> - `day_entries` is keyed `(npc_id, seq)`, not by day: `reflect` can write a second entry for a day.
> - The chat request also gains `recent_days` (the diary recall), next to `events_today`.
> - `memories.event_id` has no `REFERENCES`, like `transcript_id`, so one bad pointer can't fail a save.
> - New saves are created at v1 and migrated, so there's one path to every version.
>
> The judge tests (`pytest -m judge`, 12 including Part B's 6) all pass, run from a terminal.

**Goal:** NPCs learn from the world, not just from conversations. Something happens, the NPCs
who were there remember it **that day** (hot), and at the end of the day each one **reflects**
on its day with one LLM call (cold). Factions and combat plug into both ends later.

### The pieces

**1. `GameEvent` and the `EventLog`** (in `GameWorld/`, owned by the World):

```csharp
public enum EventType { EnteredLevel, LeftLevel, TaskDone, TaskFailed, Talked /* later: Attacked, Killed, Joined... */ }

public record GameEvent(int Id, int Day, string Level, Vector2 Where,
                        EventType Type, int? ActorId, int? TargetId, string Text);
```

- **Ids and a type, not just text.** Later systems read events in code (today's grudges, who
  to fight, gossip), so who did what to whom must be data. `Text` ("Sir Lancelot couldn't reach
  the well") is for the prompt and the console.
- **Event ids count up from one counter**, like transcript ids, and are saved.
- **`Post(GameEvent)`** is the only way in. It stores the event, hands it to perception, and
  raises `Posted`, which the console shows as a `LineKind.Event` line when it's open.
- **The log keeps every event.** It's the cold store reflection reads from, and what gossip
  and recall search later. It stays small: only meaningful things are events, never every step.

**2. The first events**, from what the game already does:

| Event | Posted by | Example text |
|---|---|---|
| `EnteredLevel` / `LeftLevel` | `World.UseExit` | "The traveller arrived at Medows" |
| `TaskDone` | `NpcBrain.TickTasks` | "Sir Lancelot reached the entry" (only `go_to`; waits aren't news) |
| `TaskFailed` | `NpcBrain.TickTasks` (the TODO) | "Sir Lancelot couldn't reach the well" |
| `Talked` | `ConversationSystem`, when a transcript is filed | "Sir Lancelot talked with the traveller" (points at the transcript) |

`Talked` matters because reflection should see the whole day, conversations included,
through one list.

The brain can't see the World, so it posts through an `Action<GameEvent>` handed in via
`TaskContext`, the same way `Nav` is handed in. Tests pass a list.

**3. Perception: who hears about it.** When an event is posted:
- the actor and the target always know;
- any other NPC in the **same level**, within **perception range** of `Where` (start at about
  6 cells), knows too;
- nobody else does. Gossip (in the [factions plan](factions-plan.md)) is how word spreads
  further.

Each NPC that perceives it gets a hot memory: `Memory(Text, MemoryKind.Event)` with its `Day`
and `SourceId` = the event id. `SourceId` already points at a transcript for
`Conversation` memories; for `Event` memories it points at an event, and `Kind` says which.

**4. `Memory.Day`.** Every memory gets the day it was formed: facts from conversations get the
transcript's day, and events get the event's day. This unlocks "today" for the prompt, recency
for consolidation (A4), and grudges.

**5. Hot: today's events in conversation.**
- The chat request gets each NPC's **events from today** as their own list, separate from
  remembered facts. Otherwise a busy day would push out "the traveller's name is Loki".
- **Facts:** the last 5, as now. **Events today:** the last 8, framed as "Earlier today you
  saw...".
- **Python:** `ParticipantDto` gains `events_today`, and the prompt frames it. pytest checks
  the field reaches the prompt; the judge checks the knight mentions what he saw.
- **No LLM call per event.** It's just data in the next prompt.

**6. Cold: the day-end reflection.**
- **On `DayStarted(newDay)`**, every NPC with anything from the day before (events or
  conversations) is **queued** for reflection on that day.
- **`ReflectionSystem`** (`GameWorld/`, built like `ConversationSystem`):
  - a queue, with **at most 2 calls in flight**;
  - polled each frame from `World.Update`, and applied on the main thread;
  - a failed call is dropped (the day goes unreflected, as with decisions).
- **The request** (`/reflection`, a new endpoint): the NPC's name and persona, the day's events
  (as text), and the summaries of that day's conversations.
- **The response:** a short **diary entry** (first person, 1–3 sentences) and up to 3 **lasting
  facts** worth remembering ("The well path is blocked").
  - The [factions plan](factions-plan.md) adds `opinion_changes` to this response later.
- **Where it goes:**
  - a new cold-tier item, `DayEntry { Day, Summary }`, in the `Journal` next to transcripts;
  - the facts become ordinary hot memories, with the day.
- **Recall:** the chat request gets the last 3 diary entries ("Yesterday: I waited at the
  castle door; the traveller arrived from the south..."), like `PastConversationsWith`.
- **Once a day is reflected, its raw events leave the hot list.** The diary entry stands in
  for them, and the events stay in the log.

**7. Not losing a day to saving.** A reflection in flight isn't saved (like decisions). So each
NPC keeps a **`LastReflectedDay`**, and that is saved. After a load, any NPC behind is queued
again for the days it missed, back to a limit (2 days), so a save made just after midnight
doesn't skip a day.

**8. The console:**
- `events [n]`: the last n events (default 10), with ids;
- `time`: the date and time of day;
- `time +<n>d` (skip to the start of a later day, raising `DayStarted`) and `time +<n>h`;
- `reflect <npc>`: queue a reflection on today now, without waiting for midnight;
- `journal <npc>` also lists diary entries, and events with their day.

### Steps

1. **The log:**
   - `GameEvent`, `EventType` and `EventLog`, with `Post` and `Posted`;
   - post `EnteredLevel` / `LeftLevel` from `UseExit`, and `Talked` when a transcript is filed;
   - console `events`, and `Posted` shown live in the console.
2. **Task events:** an `Action<GameEvent>` on `TaskContext`, and `TaskDone` / `TaskFailed` from
   `TickTasks` (closing the TODO).
3. **Perception and `Memory.Day`:**
   - the range rule;
   - `Event` memories in the journal, with the day and the event id;
   - `journal` shows them.
4. **Today's events in chat:** a separate `events_today` list in the request (C# and Python,
   with pytest).
5. **Time commands:** `time`, `time +<n>d`, `time +<n>h`. Needed before reflection can be
   tried without waiting 10 minutes.
6. **Reflection:**
   - `/reflection` in Python (models, prompt, pytest, a judge test);
   - `ReflectionSystem` with the queue and the in-flight limit;
   - `DayEntry`, facts, and recall of recent days in chat;
   - `LastReflectedDay`;
   - console `reflect`.
7. **Saving: schema v2,** the first real migration:
   - an `events` table (`event_id`, `day`, `level`, `x`, `y`, `type`, `actor_id`, `target_id`,
     `text`) and `next_event_id` on `game`;
   - `memories` gains `day` and `event_id`;
   - a `day_entries` table (`npc_id`, `day`, `summary`) and `npcs.last_reflected_day`.

   Test that a v1 save opens, migrates, and plays.

Then the [factions plan](factions-plan.md) adds opinions, grudges and gossip on top, and
[combat](combat-plan.md) posts its fights here.

### Part B open questions
1. **Perception range:** 6 cells is a guess. Should walls matter (line of sight on the
   `NavGrid`)? *Recommendation: range only for now; add line of sight with combat's aggro
   check.*
2. **Event text perspective:** stored neutral ("Sir Lancelot couldn't reach the well") and
   framed per NPC in the prompt, or written per perceiver ("You couldn't...")? *Recommendation:
   neutral, written once. The prompt says "you are Sir Lancelot", which is enough.*
3. **Does the player get a journal too?** A quest-log-style list of what you've seen would come
   cheaply from the same log. *Not needed for this plan.*

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
- **Event log** (no LLM needed, like `ConversationSystemTests`):
  - **`EventLogTests`:** ids count up; `Posted` is raised once per event; the log keeps
    order.
  - **Perception:**
    - the actor and the target always get the memory;
    - an NPC in range in the same level does;
    - one out of range or in another level doesn't;
    - the memory has the day and the event id.
  - **Task events:** a `GoToTask` with no way through posts `TaskFailed`; a finished one
    posts `TaskDone`; a `WaitTask` posts nothing.
  - **`WorldTests`:** using an exit posts `LeftLevel` and `EnteredLevel`; finishing a chat
    where the player spoke posts `Talked`.
  - **The prompt:** today's events are sent separately from facts, and yesterday's aren't
    sent once reflected.
  - **`ReflectionSystemTests`** (a fake clock and a fake reflection call):
    - a day turning over queues only NPCs with something to reflect on;
    - no more than 2 calls are in flight at once;
    - an answer adds a `DayEntry` and facts with the day;
    - a failed call changes nothing;
    - `LastReflectedDay` catches up after a load.
  - **Console:** `events`, `time`, `time +1d` (raises `DayStarted` once), `reflect`.
  - **Saving:** a v1 save migrates to v2; events, memory days, diary entries and
    `LastReflectedDay` survive a round trip.
  - **Python:** `/reflection` request and response shapes in pytest; a judge test that a day
    of failed trips to the well produces a diary entry that mentions it.
