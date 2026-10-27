# Combat — Plan

## Summary

Real-time melee combat in the world you already walk around in: press a key, the player swings
in the direction they face, anything with health in front of them takes damage, gets knocked
back and flashes. NPCs fight back, chase you round obstacles with the existing pathfinder, and
can die. No separate battle screen.

What makes it Ashenveil rather than a generic action game is the link to NPC memory: **the LLM
never runs inside a fight** (far too slow), but NPCs *remember* fights afterwards, and later the
LLM can *start* one (a decision that turns the knight hostile). Combat is fast code; memory and
grudges are the LLM's job.

It's a big feature, so it's split into phases that each leave the game playable and testable:
facing → health → the player's attack → NPCs fight back → death → enemies → saving it.

**It comes after two other pieces of work.** Combat *posts* what happens to the event log, and
*reads* opinions to decide who to fight. It owns neither:
1. The **event log and day-end reflection** ([npc-memory-next-plan.md](npc-memory-next-plan.md)
   Part B, **done**). Fights are stored as events: hot memories that same day, and lasting
   after-effects at day end. Combat adds `Attacked` / `Killed` to `EventType` and posts them
   with `EventLog.Post`.
2. **Factions and opinions** ([factions-plan.md](factions-plan.md)). Who is hostile to whom,
   for any NPC against any other NPC or the player.

---

## Where things are now

- **No health anywhere.** `Entity` has position, speed, `Direction`, collision boxes, `Feet`
  and animations; nothing can be hurt.
- **Facing is left/right only:** `Entity.FacingLeft`, set in `Entity.Update` from the change in
  X. Moving straight up or down keeps the last facing.
- **The player** moves by reading the arrow keys directly in `Player.Move` (diagonals are
  faster than straight lines: the two axes aren't normalised). `PlayerState` is
  `Idle / Walking / Talking`. Only `D_Idle` is loaded.
- **Player art is ready for this:** `Content/Sprites/Player` has `D_` (down), `S_` (side, flip
  for left) and `U_` (up) sheets, 32x32 frames, for each action:

  | Sheet | Frames |
  |---|---|
  | Idle | 4 |
  | Walk | 6 |
  | Attack | 4 |
  | Hurt | 2 |
  | Death | 8 |

- **The knight has combat art already loaded:** `attack1/2/3`, `defend`, `hurt` and `death`
  are registered in `Knight`'s constructor, and `Animation` already supports one-shot clips
  (`Loop = false`, `IsFinished`). He's side-view only (left/right).
- **The NPC brain** has a state machine: `Idle / Walking / Talking / Tasked`. Talking pauses
  the task queue and `StopTalking` resumes it. That pattern is the model for fighting.
  - It already has an unused `target` field ("for later AI, like following the player or
    attacking them").
  - It has a parked `// TODO (separate feature): combat` comment, which this plan replaces.
- **Pathfinding is done** (`NavGrid`, `Pathfinder`, `GoToTask` replanning), so chasing is
  mostly solved.
- **`World.Update`** steps the clock, conversations, then movement and collision for everyone.
  `ConversationSystem` is a good model for a `CombatSystem`: the needed pieces are handed in,
  and it's testable on its own.
- **UI:** `ProgressBar` exists ("health, loading, XP"); `GameplayScreen.Widgets` is marked
  "HUD goes here" but is empty.
- **Debug:** `Debug.SHOWBOUNDS` outlines collision boxes; the console has `where`, `tp`,
  `npcs` and `journal`, built from `CommandRegistry`.
- **Input keys used:** E interact, P pause, \` / Oem8 console.
- **Saving is built** ([Completed/save-system-plan.md](Completed/save-system-plan.md), with
  slots in [Completed/save-slots-plan.md](Completed/save-slots-plan.md)). Schema v1 has
  shipped, so anything combat adds to a save is a migration.

---

## Core idea

Three small pieces of data, one system that resolves them, and brains that decide when to
swing. All of them use cells and seconds, like the rest of the game.

### 1. `Health` (a component, not a base class)

Anything that can be hurt *has* a `Health`; it isn't a subclass. The player and NPCs get one;
later a breakable barrel or a training dummy can too, without being an `Entity` subclass.
`Entity.Health` is null for things that can't be hurt.

```csharp
public class Health
{
    public int Max { get; }
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;
    public float Fraction => (float)Current / Max;   // straight into a ProgressBar

    // Seconds left of "just got hit, can't be hit again". Stops one swing that overlaps
    // for several frames from hitting several times, and gives the victim a moment.
    public float Invulnerable { get; private set; }

    public bool TakeDamage(int amount, float invulnerableFor) { ... }   // false if it didn't land
    public void Heal(int amount) { ... }
    public void Update(float dt) { ... }   // counts Invulnerable down
}
```

### 2. `AttackData` and `Swing`: an attack is data plus a timer

An attack has three phases, all in seconds. It isn't driven by animation frames, because tests
run with no textures (`NullContentManager`) and frame lengths change when the art changes.

```
 windup ──────► active ──────► recovery ──► done
 (telegraph,    (hitbox is     (can't move
  can't move)    live)          or swing yet)
```

```csharp
public record AttackData(int Damage, float Reach, float Width,
                         float Windup, float Active, float Recovery,
                         float Knockback, string Animation);
```

`Swing` is one attack in progress. It holds the `AttackData`, the time since it started, the
facing it was thrown in, and **who it has already hit**: each swing hits each target at most
once.

Its hitbox is a `RectF` in front of the attacker's `Feet`, in the facing direction, `Reach`
deep and `Width` wide. It only exists during the active phase.

### 3. `Facing`: four directions

`Facing { Down, Up, Left, Right }` on `Entity`, set from the actual movement (whichever axis
moved more), replacing `FacingLeft`. It picks the player's `D_ / U_ / S_` sheet and aims the
hitbox. The knight only has side art, so up and down fall back to his last left/right.

### 4. `CombatSystem`: resolves hits

It lives in `GameWorld`, next to `ConversationSystem`, and is called from `World.Update`
**after** movement and collision, so hitboxes use where everyone actually ended up. Each frame:

1. Tick every `Health` (invulnerability) and every `Swing` (phase).
2. For each swing in its active phase, find every entity whose `Bounds` overlaps the hitbox,
   that isn't the attacker, isn't already hit by this swing, and isn't dead.
3. Apply the hit:
   - damage;
   - knockback, a short push away from the attacker that decays over ~0.15 s (collision and
     `Bounds.Clamp` still apply, so nobody is knocked into a wall);
   - the hurt animation and a red tint flash.
4. Raise events: `Hit(attacker, victim, damage)` and `Died(victim, killer)`. The World uses them
   for aggro, memory and the HUD, the same way `PlayerConversationStarted` is used.

It's given the list of combatants rather than the whole `World`, so tests can build three
`TestEntity`s and a swing, and check who got hit.

---

## Decisions

### Recommended (change any of these)

1. **Real-time, in the world.** The game already is real-time with collision and pathfinding,
   and the art is action-game art. A turn-based battle screen would be a second game.
2. **Melee only for v1.** Ranged (projectiles) is a later phase; it reuses `Swing`'s
   "hitbox + already-hit list" as a moving hitbox.
3. **Attack key: Space.** E stays interact. One attack for the player at first. The knight's
   three attack clips become a combo later.
4. **Timing is time-based**, in `AttackData` seconds. Animations are just picked to match.
5. **`Health` and `Swing` are components on `Entity`**, not a `Combatant` subclass, so the
   player and NPCs share them without a new class tree.
6. **The LLM is never in the combat loop.** Brains decide chase/attack/flee in code. The LLM
   comes in before (it can make an NPC hostile) and after (the NPC remembers).
7. **Fighting is an NPC state, like Talking.** `NPCState.Fighting` pauses the task queue and
   ends by resuming it, just like `StopTalking`; `NPCState.Dead` stops everything.
   - Not a task in the queue: a fight has to interrupt whatever the NPC was doing, and come
     back to it after.
8. **Anyone can fight anyone.** Combat code has no teams: a swing hits whatever is in its
   hitbox (friendly fire is on). Who a brain *chooses* to attack comes from its opinions
   ([factions-plan.md](factions-plan.md)). Attacking a friendly NPC has consequences: they
   fight back now, and remember it (events, then reflection).
9. **No talking during a fight.** `World.Interact` skips NPCs that are fighting or dead.
   Getting hit while the chat box is open can't happen, because the chat screen stops the
   world updating.
10. **Hits are shown with:** a hurt animation, a red tint, knockback, and a short
    invulnerability window (~0.4 s). Damage numbers and screen shake can come later.

### Open questions (your call)

1. **When the player dies:** respawn at `Player_spawn` with full health (simple), or a
   "You died" screen offering Load (fits "only save on Save button"; it needs the save system
   finished)? *Recommendation: respawn for now, the Load screen once saving lands.*
2. **When a named NPC dies:** do they stay dead? Losing Sir Lancelot and everything he
   remembers is a big deal in a memory game.
   - **(a)** Permanent death.
   - **(b)** Knocked out: down for a while, then gets up and remembers it.
   - **(c)** Named NPCs can't die; only enemies can.

   *Recommendation: (b) for named NPCs and real death for enemies. It keeps the memory payoff
   without losing your only LLM character to a misclick.*
3. **Enemy art:** there are no enemy sprites yet. Options: a recoloured knight as a "bandit",
   or a free asset pack (wolves fit the knight's warning about "wolves, bandits, and worse").
   The early phases don't need it; the knight is the first sparring partner.
4. **Numbers:** HP values, damage, reach. The plan has starting values; they're for tuning,
   not decisions.

---

## Phases and steps

Each step leaves the game working, with tests. Steps 0–4 are the core; 5 onward can wait.

### Step 0 — Four-way facing and the player's animation set

- `Facing` enum on `Entity`, set from movement. `FacingLeft` becomes `Facing == Left` (the
  draw flip still applies to side sheets).
- Load the player's `D_ / S_ / U_` idle, walk, attack, hurt and death sheets. Pick the
  animation by state + facing (`"walk_up"`, `"attack_side"`...).
- Normalise the player's diagonal movement while you're in there.
- *Visible result:* the player looks the way they walk, and stops looking down all the time.

### Step 1 — Health, and hurting things by console

- `Health` class and `Entity.Health`. Player 100, knight 80 (tuning).
- Death without combat: hitting 0 sets `NPCState.Dead` / `PlayerState.Dead` and plays `death`
  once. Dead NPCs are skipped for talking, collision and AI.
- Console commands (that's how to test before there's an attack):
  - `hp [npc]`: show health;
  - `damage <npc|player> <n>` and `heal <npc|player> [n]`;
  - `kill <npc>`;
  - `god`: toggles player invulnerability.
- HUD: a `ProgressBar` for the player's health in `GameplayScreen.Widgets`. NPCs get a small bar
  over their head, only after they've been hurt.

### Step 2 — The player's attack

- `AttackData`, `Swing`, and `CombatSystem` (hit resolution and events), wired into
  `World.Update`.
- Space starts a swing if none is running. No movement during windup and recovery.
- Starting values for the player:

  | Field | Value |
  |---|---|
  | Damage | 10 |
  | Reach | 0.6 cells |
  | Width | 0.7 cells |
  | Windup / Active / Recovery | 0.08 / 0.12 / 0.2 s |
  | Knockback | 0.4 cells |

- Knockback, the tint flash and invulnerability.
- Debug: with `SHOWBOUNDS` on, draw the live hitbox in red.
- *Visible result:* walk up to the knight and hit him. He flinches and his bar drops; he
  doesn't fight back yet.

### Step 3 — NPCs fight back

- `NPCState.Fighting` with `target` (the field that's already there). Entered when hit (aggro),
  or by a console command `aggro <npc>`. It pauses the task queue like Talking.
- `UpdateFighting` in the brain, virtual so each brain can differ:
  - **Chase:** steer toward the target's `Feet`, replanning with `Pathfinder` every ~0.5 s
    (the same approach as the planned `follow` task, A2 in
    [npc-memory-next-plan.md](npc-memory-next-plan.md)).
  - **Attack:** when the target is in reach, face it and swing (the knight cycles
    `attack1/2/3`), then a cooldown.
  - **Give up:** the target dead, out of range for N seconds, or in another level, then
    `StopFighting()` (back to the task queue, or wandering).
  - **Knight extra:** sometimes `defend` (blocks damage from the front while the clip plays).
- *Visible result:* hit the knight and he chases you round the well and hits back.

### Step 4 — Death and consequences

- **Player death:** the death animation, then respawn (open question 1).
- **NPC death or knockout:** per open question 2.
- **Fights become events.** `CombatSystem`'s `Hit` and `Died` are posted to the event log
  (Part B) as structured events: attacked, wounded, killed, fled, with attacker and victim
  ids. Perception decides who saw them.
  - **That same day**, they're hot memories (`MemoryKind.Event`), so the knight says "you
    struck me this morning", and today's grudges make him hostile straight away.
  - **At day end**, reflection turns them into lasting opinion changes, about you or about a
    whole faction ([factions-plan.md](factions-plan.md)).

  This is the payoff step, and combat writes no memory code of its own.

### Step 5 — Picking fights: opinions and enemies

- **Brains choose targets from opinions.** A few times a second, an NPC looks at the people it
  can perceive and attacks anyone it's hostile to (opinion or today's grudge). That covers NPC
  against NPC too: the knight goes for a bandit with nobody telling him to.
- **Joining in:** an NPC that sees a fight joins the side it likes more: friendly to the
  victim, or hostile to the attacker.
- Aggro radius, and later line of sight on the `NavGrid`.
- A first enemy type (e.g. `Bandit : NPC` or `Wolf : NPC`) with an `EnemyBrain` (wander, aggro,
  fight, leash back home).
- Spawning from the level JSON instead of the hard-coded `NpcRoster.SpawnStartingNpcs` switch
  (that was always the plan), and an "Enemy" option in the editor's spawn dropdown.

### Step 6 — Saving it

- Health for players and NPCs; dead / knocked-out state (and knock-out time left).
- Fights in progress aren't saved, like conversations: you can only save from the pause menu,
  so the World isn't running. Health persists; who you were fighting doesn't.
- Save schema v1 has shipped, so this is a `user_version` migration (add the `hp` columns),
  unless the factions work's migration has already gone in first; then they share one.

### Later (not planned in detail)

- Ranged attacks (projectiles), a stamina or attack cooldown bar.
- Weapons and items changing `AttackData` (needs an inventory).
- The LLM starting fights: a decision `behaviour: "hostile"` or a task `attack <who>` sets
  Fighting with a target. That's one new arm in `ApplyDecision`, like `go_to`.
- Hit-stop, damage numbers, screen shake, sounds.

---

## Testing

Like the rest of the game, everything is tested in cells and seconds with no window: fake
`GameTime` steps, `TestEntity` / `TestNpc`, and `FakeInput` for keys.

- **`HealthTests`:**
  - damage lowers it and never goes below 0;
  - invulnerability blocks a second hit, then wears off;
  - `Heal` never goes above `Max`;
  - `IsDead` at 0.
- **`SwingTests`:**
  - phases change at the right times;
  - the hitbox is only live in the active phase;
  - the hitbox sits in front of the feet for each facing (four cases with `Near.Equal`).
- **`CombatSystemTests`** (no World, like `ConversationSystemTests`):
  - a swing hits an entity in front, not one behind;
  - one swing hits a target once, even over several frames;
  - two targets in the hitbox both get hit;
  - the attacker never hits themselves;
  - dead entities aren't hit;
  - `Hit` and `Died` are raised once each;
  - knockback moves the victim away from the attacker.
- **`FacingTests`:** movement sets the facing; standing still keeps it.
- **Brain tests:**
  - being hit sends an NPC into Fighting with the attacker as target;
  - it walks toward the target, swings when in reach;
  - it gives up when the target dies, and resumes its task queue (the knight's walk carries
    on).
- **`WorldTests`:**
  - press Space next to the knight (`FakeInput`), step a few frames, and his health drops;
  - a dead NPC can't be talked to;
  - after a fight, the event log has it, and the knight's journal has it as a hot event memory;
  - two NPCs that are hostile to each other fight with no player involved.
- **`WorldCommandsTests`:** `hp`, `damage`, `heal`, `kill`, `god`, `aggro`.
- **By hand:** how the swing feels (timings, reach, knockback) can only be tuned by playing.
  Keep those numbers in one place (`AttackData` values on each class) so tuning is a one-line
  change.

---

## Depends on / feeds into

- **Depends on:**
  - pathfinding (done) and world units (done);
  - the save system (done);
  - the event log and reflection (NPC plan, Part B; done), needed by step 4;
  - [factions-plan.md](factions-plan.md), needed by step 5.

  Steps 0–3 need neither, so they can start while the others are still being built.
- **Feeds into:**
  - [npc-memory-next-plan.md](npc-memory-next-plan.md): A2 `follow` (same chase code), and
    Part B (fights are the most important events it gets).
  - [draw-layers-plan.md](draw-layers-plan.md): health bars over heads and hitbox debug
    drawing are overlay-layer drawing.
