# Factions & Opinions — Plan

## Summary

Who likes whom, as data the brains and the LLM can both read. Any NPC can end up fighting
any other NPC or the player, because hostility isn't a fixed team: it's an **opinion**, and
opinions change with what happens.

Four kinds of data:

1. **An NPC's opinion of a person** (this knight about the traveller, by id): "he attacked
   me". Once it exists it's **its own value**. It *starts* at what the NPC's faction view
   says, then only changes because of what happens between them. It never drifts back.
2. **An NPC's opinion of a faction** (this knight about bandits): "I hate bandits". Each NPC
   holds its own row, so two guards can disagree.
3. **The faction table** (guards about bandits, by default): only used to **seed** a new
   NPC's faction opinions when it's created. It's the starting point, not a live rule.
4. **A player's standing with each faction** (how the guard as a whole sees this player): a
   reputation. It's held per player, so it works for more than one player later. It's where an
   NPC's opinion of a player *starts*.

Opinions change in two speeds, following the event log in
[npc-memory-next-plan.md](npc-memory-next-plan.md) Part B:
- **During the day**, what happens becomes **hot memory events** ("the traveller attacked me
  at the well"). The LLM sees them in conversation, and the brain can react in code, but no
  opinion is rewritten yet.
- **At the end of the day**, each NPC's reflection call turns the day's events into lasting
  **after-effects**: opinion changes about people *and* factions ("I hate bandits now").

**Gossip** spreads events beyond the people who saw them. NPCs pass on what they saw to
people they like, so word gets around a village without everyone witnessing everything.

The player can **join factions**, and has a **standing** with every faction, member or not.

**Order:** save system (done) → event log (Part B) → this → combat. Combat posts events and
reads opinions to choose targets; it doesn't own any of it.

---

## Where things are now

- **Nothing about factions or opinions exists.** The NPC plan's A3 "disposition"
  (`friendly | neutral | wary | hostile` on the decision) was the placeholder; this plan
  replaces it.
- **People are known by id:** NPCs by stable key, players by `PlayerId` (save system decision
  13). Transcripts carry ids and names. Opinions key on the same ids.
- **No event log yet:** `MemoryKind.Event` exists but nothing posts one. Part B builds the log,
  perception, `Memory.Day` and the staggered day-end reflection call. `MemoryKind`'s comment
  already expects a `Rumour` kind, which gossip will use.
- **Starting NPCs** are hard-coded in `NpcRoster.SpawnStartingNpcs`, with no faction field.
- **Saving is built** (schema v1 has shipped), so everything this adds to a save is a
  `user_version` migration.

---

## Core idea

### The data

```csharp
// Content/factions.json, loaded once. Read-only defaults, like a level file.
// "guard": { "guard": 50, "bandit": -80, "village": 40, "traveller": 0 }, ...

public class Opinions                       // one per NPC, in the brain next to Journal
{
    // About single people (NPC key or PlayerId). Once an entry exists it's this NPC's own
    // view and only changes through what happens - it never fades back to the faction view.
    private readonly Dictionary<string, int> _people = new();
    // About whole factions. Seeded from the faction table when the NPC is created.
    private readonly Dictionary<string, int> _factions = new();

    public int Of(Person who) { ... }                   // the lookup below
    public void Change(Person who, int by) { ... }      // seeds the entry first if it's missing
    public void ChangeFaction(string faction, int by) { ... }
}

public class Player
{
    public List<string> Factions { get; }               // what they've joined
    public Dictionary<string, int> Standing { get; }    // how each faction sees them
}
```

Every score runs from -100 to +100. A few thresholds turn a score into behaviour:

| Score | Attitude | What the brain does |
|---|---|---|
| ≤ -60 | Hostile | Attacks on sight (once combat exists) |
| -60 to -20 | Wary | Keeps its distance; short with you in chat |
| -20 to +20 | Neutral | Default |
| ≥ +20 | Friendly | Helps allies in a fight; warmer in chat; trusted with gossip |

### The lookup: a personal opinion, or what it would start at

```
Of(who):
  I have a personal opinion of who?   -> that, and nothing else
  otherwise, the faction view:
    who is a player                   -> my faction's standing with that player
    who is an NPC                     -> my opinion of their factions (the lowest one wins:
                                         a guard sees a bandit, even if he's also a villager)
    no factions                       -> 0
```

**A personal opinion is created the first time something changes it** (an event, gossip,
reflection), starting at whatever the faction view was right then. After that it's
independent:
- If the guard's standing with you drops later, a guard who already knows you **keeps his own
  opinion**. Only strangers see the new reputation.
- A guard who hates bandits can still come to like one particular bandit, and that holds.

Meeting someone isn't enough to create an entry. Without one, the NPC keeps tracking its faction
view, so a stranger still reacts when you join a faction or your standing changes.

### Player standing and membership

**Standing** is how a faction as a whole sees a player: their reputation with it. Every
player has one for every faction, member or not.

- **It starts from the faction table.** A new player's standing with each faction is the
  table's view of the player's starting faction (`traveller`), so guards start neutral and
  bandits wary.
- **Joining** a faction:
  - adds it to the player's `Factions`;
  - raises standing with it;
  - shifts standing with every other faction toward what the table says that faction thinks of
    the new one: join the guard and bandits like you less.
  - It's a change, not a reset: the standing you earned isn't wiped.
- **Leaving** works the other way round, with a penalty with the faction you left.
- **What moves standing during play:**
  - **Members' opinions add up to the faction's view.** When reflection changes a member's
    personal opinion of a player, the faction's standing moves by a fraction of that (say a
    quarter). Hurt one guard and the guard thinks a little less of you.
  - **Direct rewards and crimes:** a quest done for the guard, or killing one of theirs, can
    change standing directly (an event type of its own).
  - **Gossip makes it travel** (below): the guards who *hear* about it form their own
    opinions, which feed standing again.
- **Ranks are later:** a member with high standing could become a sergeant, unlock dialogue,
  and so on. Not v1.

### Gossip

What an NPC saw doesn't stay with it. The NPC passes it on to people it likes, who may pass it
on again. That's how a village learns you robbed the miller without anyone else being there.

- **When:** two NPCs who are friendly with each other (opinion ≥ +20) spend time near each
  other (same level, within talking range) **share their recent events**. It's a cheap
  engine step with no LLM call: the event is copied over as a **rumour**.
  - Later, once NPCs talk to each other with the LLM (A1 in the NPC plan), gossip can become
    real conversations. The data stays the same.
- **A rumour is a weaker event:**
  - it's stored as `MemoryKind.Rumour`, with who told them;
  - the LLM sees it framed as hearsay ("Marta told you the traveller robbed the miller");
  - at day end, reflection can change opinions from rumours too, but capped lower (±15 rather
    than ±40).
- **It doesn't spread forever:** each event carries a hop count. A witness passes it on at hop 1
  and a hearer at hop 2, and it stops at hop 2 or 3. Each NPC keeps an event only once,
  however many people tell them.
- **Trust filters it:** NPCs only pass things on to people they like, and only believe people
  they like. Someone who hates the teller ignores the rumour.
- **It can be wrong later:** carrying the teller's id leaves room for exaggeration and lies
  without changing the format. Not v1.

### The two speeds

**Same day (hot, no LLM call):**
- Events are posted to the event log: who did what to whom, where, when.
- NPCs who perceived an event get it as a hot memory (`MemoryKind.Event`); gossip may spread
  it as `Rumour`. Both show in the next conversation's prompt.
- The brain can react in code before the day ends, using **today's grudges**: a quick count of
  today's events about a person ("attacked me" means treat them as hostile until reflection
  decides). Without this, an NPC you attacked an hour ago would greet you as normal until
  midnight. Grudges are temporary; they're cleared once reflection has written the lasting
  change.

**Day end (cold, one LLM call per NPC, staggered):**
- Reflection gets the day's events and rumours and the NPC's current opinions, and returns:
  - a summary for the cold store (Part B already plans this);
  - **opinion changes**, e.g. `[{ "about": "player:3f2a…", "by": -30, "why": "..." },
    { "about": "faction:bandit", "by": -20, "why": "..." }]`.
- **The game validates before applying**, the same rule as decisions:
  - `about` must be a person or faction from that day's events or rumours (the NPC is handed
    the list, with keys, so it answers in keys, not names);
  - each change is capped: ±40 a day from things it saw, ±15 from rumours;
  - unknown keys are dropped.
- Changes about a player also nudge that player's **standing** with the NPC's faction (above).
- **Nothing fades.** Opinions only move because something happened.

### Who reads opinions

- **Conversations:** the prompt gets the NPC's attitude to each participant ("You distrust
  this traveller: he attacked you two days ago"). That covers A3.
- **Brains (with combat):** pick targets (hostile in range), decide whether to join a fight
  (friendly to the victim, or hostile to the attacker), and whether to flee.
- **Gossip:** who tells whom, and who's believed.
- **The console:**
  - `opinions <npc>` and `opinion <npc> <who> <n>` to set one;
  - `factions`;
  - `standing` to show the player's standings, and `standing <faction> <n>` to set one;
  - `join <faction>` / `leave <faction>`;
  - `rumours <npc>`.

---

## Decisions

### Decided (2026-10-28)
1. **The event log comes first.** Fights and other things that happen are stored as events;
   lasting effects come from day-end reflection.
2. **Same-day events are hot memories** the LLM can see; **after-effects are written at day
   end**, including opinions of whole factions.
3. **The player can join factions.**
4. **NPCs hold opinions of whole factions**, seeded from the faction table, so a new NPC
   starts with sensible views of everyone.
5. **A personal opinion is its own value.** It starts at the current faction view, then never
   fades back to it.
6. **Gossip is in:** events spread between NPCs who like each other, as weaker rumours.
7. **Players have a standing with every faction**, held per player.

### Recommended
8. **A score from -100 to 100**, with attitude thresholds, rather than storing the attitude
   itself. Changes are then just additions, and the thresholds can be tuned.
9. **For several factions, the lowest wins** when an NPC judges an NPC stranger.
10. **The faction table is read-only data** (`Content/factions.json`, editable by hand or in the
    editor later). Only each NPC's own copy changes, and that copy is saved.
11. **The game applies the changes, the LLM only proposes them,** with caps and an "only about
    who you met or heard about today" rule.
12. **Today's grudges in code**, so behaviour on the same day doesn't wait for reflection.
13. **Gossip is a game step first** (copying events as rumours, with no LLM call), and only later
    real NPC-to-NPC conversations.

### Open questions
1. **Can NPCs change faction?** A guard turned bandit is a great story beat. The same reflection
   output could propose it, at a much higher bar. *Recommendation: later.*
2. **How much a member's opinion moves standing** (a quarter is a guess), and whether killing a
   member is a fixed big hit. Tuning, not structure.
3. **Gossip about factions:** "bandits are raiding the north road" could spread and change
   faction opinions, not just personal ones. It's the same rumour mechanism with a faction
   `about`. *Recommendation: allow it, but let reflection decide whether it changes anything.*

---

## Steps

1. **Faction table and membership:**
   - `factions.json`, loaded like levels;
   - a `Factions` list on `NPC` and `Player`;
   - the knight is `guard`, the player `traveller`;
   - console `factions`, `join`, `leave`.
2. **`Opinions` in the brain, and player standing:**
   - NPC faction opinions seeded from the table at spawn;
   - player standing seeded from the table, with the join and leave shifts;
   - the lookup (personal, else faction view), and seeding a personal opinion on first change;
   - console `opinions`, `opinion`, `standing`.
3. **Opinions in conversation:** each NPC's attitude to each participant goes in the chat
   request (the Python prompt side too). Covers A3.
4. **Today's grudges:** reading today's events in code (needs Part B's log and perception).
5. **Reflection writes opinion changes:**
   - the reflection request gets the people and factions from the day's events and rumours,
     with keys;
   - the response schema gains `opinion_changes`;
   - the game validates them, caps them, applies them, and passes a share on to standing.
6. **Gossip:**
   - `MemoryKind.Rumour`, carrying the teller and a hop count;
   - the sharing step between friendly NPCs who are near each other, with no duplicates;
   - rumours in the chat prompt as hearsay, and in reflection with the lower cap;
   - console `rumours <npc>`.
7. **Joining factions from the game:** a decision field (`join_faction`) that the game checks
   ("can this NPC let people in?").
8. **Saving** (a `user_version` migration):
   - `npcs.factions` and `players.factions`;
   - an `opinions` table (`owner_key`, `about` (a person key or `faction:<name>`), `score`);
   - a `standings` table (`player_id`, `faction`, `score`);
   - rumours are stored as journal events, with columns for the teller and hop count.

   The faction table isn't saved, because it's data.

Then combat ([combat-plan.md](combat-plan.md)) uses all of it.

---

## Testing

- **`OpinionsTests`:**
  - NPC faction opinions are seeded from the faction table;
  - with no personal entry, the lookup follows the faction view, and keeps following it when
    standing changes;
  - the first change seeds a personal entry from the current faction view, then applies the
    change;
  - once an entry exists, changing standing or faction opinions doesn't move it;
  - for an NPC with several factions, the lowest wins;
  - scores stay within ±100;
  - thresholds map to the right attitude.
- **`StandingTests`:**
  - a new player's standings are seeded from the table;
  - joining raises standing with that faction and shifts the others by the table, without
    resetting what was earned;
  - leaving applies its penalty;
  - a reflection change about a player moves the NPC's faction standing by the set fraction.
- **`GossipTests`** (fake clock, no LLM):
  - friendly NPCs near each other share events, unfriendly ones don't;
  - a rumour stops at the hop limit;
  - an NPC keeps an event only once, however many people tell them;
  - someone who dislikes the teller ignores it.
- **Reflection** (a fake LLM call, like `ConversationSystemTests`):
  - valid changes are applied;
  - unknown keys and people not met or heard about that day are dropped;
  - changes are capped, with the lower cap for rumours;
  - a failed call changes nothing.
- **Today's grudges:** an attack event makes the victim hostile to the attacker straight away,
  and reflection clears the grudge.
- **Prompt:** the chat request includes the attitude line for each participant, and rumours
  framed as hearsay.
- **Console:** `opinions`, `opinion`, `standing`, `join`, `leave`, `rumours`, against a test
  World.
- **Save round trip:** opinions, standings, memberships and rumours survive save and load, and
  the migration upgrades a v1 save.
- **The LLM judge:** a day where bandits robbed the knight produces a negative change about
  `faction:bandit`, and not about the player.

---

## Depends on / feeds into

- **Depends on:** the [save system](Completed/save-system-plan.md) (done: ids, and somewhere
  to keep opinions); the event log and reflection
  ([npc-memory-next-plan.md](npc-memory-next-plan.md) Part B).
- **Replaces:** A3 disposition in the NPC plan.
- **Feeds into:**
  - [combat-plan.md](combat-plan.md): target choice, joining fights, enemies;
  - NPC-started conversations (A1): gossip becomes real conversations;
  - quests, which reward standing; ranks within a faction.
