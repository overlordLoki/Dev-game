using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Saving;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Levels;
using Ashenveil.Core.GameWorld.Locations;
using Microsoft.Xna.Framework;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// The game itself, with no screen attached: levels, NPCs, the player, the clock,
    /// conversations and what NPCs learn from them. It knows nothing about input, the
    /// camera, drawing or other screens - GameplayScreen handles those - so a test can
    /// build a World from a level and step it forward like the game does.
    /// </summary>
    public class World
    {
        /// <summary>The edges of the current level; keeps everyone inside.</summary>
        public WorldBounds Bounds { get; private set; }
        public Player player;
        /// <summary>Every conversation, and what NPCs learn from them.</summary>
        public ConversationSystem Conversations { get; }
        /// <summary>Game time: days, seasons, years. Raises DayStarted when a day turns over.</summary>
        public GameClock Clock { get; } = new GameClock();
        /// <summary>Which NPCs exist, and which level each one is in.</summary>
        public NpcRoster Roster { get; } = new NpcRoster();
        /// <summary>Everything meaningful that's happened. Posting one tells whoever saw it.</summary>
        public EventLog Events { get; }
        /// <summary>Each NPC looking back on its day, when the day turns over.</summary>
        public ReflectionSystem Reflections { get; }
        /// <summary>The factions and their default views of each other. Read-only data.</summary>
        public FactionTable Factions { get; }

        /// <summary>The NPCs in the level being played.</summary>
        public IReadOnlyList<NPC> NPCs => Roster.In(location.Name);

        /// <summary>
        /// Raised when a conversation the player is part of begins, so whoever is showing the
        /// world (GameplayScreen) can open the chat box. The world itself never opens screens.
        /// </summary>
        public event Action<Conversation> PlayerConversationStarted;

        /// The area currently being played. Only ever one: entering a building swaps
        /// this rather than stacking a second screen, so the player, camera and pause
        /// stack all survive the transition untouched.
        public Location location;

        // Every level visited so far, by name. A level is read from disk the first time it's
        // entered and kept after that, so going back is the same Location, not a fresh copy:
        // whatever happens to a level (a nav grid built for pathfinding, state to save, NPCs
        // travelling between levels) still holds on the return trip.
        private readonly Dictionary<string, Location> _visited = new();

        // How close an NPC has to be to talk to, body centre to body centre, in cells.
        private const float TalkRange = 1.5f;

        /// <param name="start">The level the game begins in.</param>
        /// <param name="sendChat">How a chat reply is asked for. Defaults to the real API.</param>
        /// <param name="requestDecision">How an end-of-conversation decision is asked for. Defaults to the real API.</param>
        /// <param name="requestReflection">How a day-end reflection is asked for. Defaults to the real API.</param>
        public World(Location start,
                     Func<ConversationRequestDto, System.Threading.Tasks.Task<ChatMessage[]>> sendChat = null,
                     Func<DecisionRequestDto, System.Threading.Tasks.Task<NpcDecisionDto>> requestDecision = null,
                     Func<ReflectionRequestDto, System.Threading.Tasks.Task<ReflectionDto>> requestReflection = null,
                     FactionTable factions = null)
        {
            Factions = factions ?? FactionTable.Default;
            Roster.Factions = Factions;
            Conversations = new ConversationSystem(sendChat, requestDecision);
            Reflections = new ReflectionSystem(requestReflection);
            // Everything that posts events is wired to the one log: NPCs (their tasks) through
            // the roster, conversations directly. The World posts level changes itself.
            Events = new EventLog(() => Clock.TotalDays, Perceive);
            Roster.PostEvent = e => Events.Post(e);
            Roster.FindPersonNear = FindPersonNear;
            Roster.StartConversationFor = NpcStartsConversation;
            Conversations.Events = Events;
            Conversations.FindPerson = FindPerson;
            Conversations.Factions = Factions;
            Reflections.Events = Events;
            Reflections.FindPerson = FindPerson;
            Clock.DayStarted += day => Reflections.DayStarted(day, Roster.All);
            // Id 0 is this save's owner, not "the player": other players would take ids from
            // the roster like NPCs do, so ask `is Player`, never `Id == 0`.
            this.player = new Player(Vector2.Zero, 0);   // Init stands them on the spawn point
            player.SeedStanding(Factions);
            SetLocation(start);
        }

        /// <summary>
        /// Called on first load of a new game: stands the player on the spawn point and brings
        /// in the level's starting NPCs. Everything is in cells, so this doesn't depend on the
        /// window size - it can run before the first frame or in a test with no window at all.
        /// </summary>
        public void Init()
        {
            location.PlaceAt(player, "Player_spawn");
            Roster.Populate(location);
        }

        /// <summary>
        /// The other way to start: carry on a saved game instead of calling <see cref="Init"/>.
        /// Puts back the player (id, name, level, feet), the clock, both id counters, which
        /// levels have spawned their NPCs, and every saved NPC in its own level, with its
        /// journal and task queue.
        ///
        /// Levels themselves come from their files as always. Throws (IOException, or
        /// JsonException for a broken file) if a level the save names can't be read, and
        /// InvalidDataException if the save has no player - the caller says so and stays in the menu.
        /// </summary>
        public static World FromSave(SaveData save,
                                     Func<ConversationRequestDto, System.Threading.Tasks.Task<ChatMessage[]>> sendChat = null,
                                     Func<DecisionRequestDto, System.Threading.Tasks.Task<NpcDecisionDto>> requestDecision = null,
                                     Func<ReflectionRequestDto, System.Threading.Tasks.Task<ReflectionDto>> requestReflection = null)
        {
            // The save's owner. Other players are a multiplayer feature and would join here.
            var owner = save.Players.FirstOrDefault()
                ?? throw new System.IO.InvalidDataException("The save has no player in it.");

            var world = new World(new Location(owner.Level), sendChat, requestDecision, requestReflection);
            world.player = new Player(Vector2.Zero, owner.Id, owner.Name);
            world.player.StandAt(new Vector2(owner.X, owner.Y));
            SaveMapper.FillFactions(world.player, owner, world.Factions);

            world.Clock.Restore(save.TotalDays, save.SecondsIntoDay);
            world.Roster.Restore(save.NextPersonId, save.PopulatedLevels);
            world.Conversations.Restore(save.NextTranscriptId);
            world.Events.Restore(save.NextEventId, save.Events.Select(SaveMapper.FromSave));

            // One dictionary for every NPC, so a conversation two of them had is one object again.
            var transcripts = SaveMapper.TranscriptsFrom(save);
            foreach (var saved in save.Npcs)
            {
                var npc = SaveMapper.FromSave(saved, transcripts, world.Factions);
                // An NPC elsewhere gets its level loaded now: it needs a Location (and its nav
                // grid) for its tasks, and the player walking in later finds that same Location.
                if (npc != null) world.Roster.Add(world.LocationNamed(saved.Level), npc);
            }

            // Reflections in flight when it was saved were lost: ask for any day an NPC missed.
            world.Reflections.CatchUp(world.Roster.All, world.Clock.TotalDays);
            return world;
        }

        /// <summary>
        /// Puts an NPC in the level being played. Tests use this, and a future debug-console
        /// "spawn" command can too.
        /// </summary>
        public void AddNpc(NPC npc) => Roster.Add(location, npc);

        /// <summary>
        /// Makes <paramref name="next"/> the active area and re-fences the world to its
        /// grid. WorldBounds is rebuilt because it holds the map it clamps against.
        /// </summary>
        private void SetLocation(Location next)
        {
            location = next;
            _visited[next.Name] = next;
            Bounds = new WorldBounds(next.tileMap);
            // No NPC spawning here: a new game spawns in Init, a level change in UseExit.
        }

        /// <summary>
        /// The player's "interact" action (the E key): use the exit the player is touching,
        /// otherwise talk to an NPC in reach. Returns the conversation it started, or null (an
        /// exit was used, or there was nothing to do). Doors win: being pressed against one is
        /// the more specific thing to be doing, and NPCs hang around doorways (the knight's walk
        /// ends right below Medows' castle door) - talking first made the door unusable.
        /// </summary>
        public Conversation Interact()
        {
            var exit = location.ExitTouching(player.Bounds);
            if (exit != null)
            {
                UseExit(exit);
                return null;
            }

            var npc = player.GetNearestEntityInRange(NPCs, TalkRange) as NPC;
            return npc != null ? StartConversation(player, npc) : null;
        }

        /// <summary>
        /// Walks through a doorway: loads the target level and stands the player on the point
        /// the exit names there (its "default" point if it names none, or one that isn't there).
        /// That point normally sits just inside the destination's own door back.
        /// </summary>
        public void UseExit(ExitData exit)
        {
            Events.Post(GameEvent.Of(EventType.LeftLevel, location.Name, player.Feet, player.Id, null,
                                     $"{player.Name} left {location.Name}"));
            SetLocation(LocationNamed(exit.to));
            Roster.Populate(location);   // first visit spawns, return visit reuses
            location.PlaceAt(player, exit.arriveAt);
            Events.Post(GameEvent.Of(EventType.EnteredLevel, location.Name, player.Feet, player.Id, null,
                                     $"{player.Name} arrived at {location.Name}"));
        }

        /// <summary>A player or NPC by id, in any level; null if there's nobody with that id.</summary>
        public Entity FindPerson(int id) => id == player.Id ? player : Roster.Find(id);

        // Perception: every NPC who saw an event remembers it (see Perception for who that is).
        private void Perceive(GameEvent e)
        {
            foreach (var npc in Perception.WhoKnows(e, Roster)) npc.Witness(e);
        }

        /// <summary>
        /// The level with this name: the one already loaded, or read from disk the first time
        /// (and kept, so the next ask gets the same Location).
        /// </summary>
        private Location LocationNamed(string name)
        {
            if (!_visited.TryGetValue(name, out var level)) _visited[name] = level = new Location(name);
            return level;
        }

        /// <summary>
        /// The one place a conversation begins, whoever starts it. The ConversationSystem does
        /// the work; the World adds the one rule that's about the player: if they're in it,
        /// raise <see cref="PlayerConversationStarted"/> so the screen can open the chat box.
        /// </summary>
        public Conversation StartConversation(Entity from, params NPC[] others)
        {
            var convo = Conversations.Start(from, others, location, Clock.TotalDays);
            if (from == player) PlayerConversationStarted?.Invoke(convo);
            return convo;
        }

        /// <summary>
        /// An NPC walked up to someone to talk (a talk_to job). For now only the player can be
        /// talked to: the chat box opens as if they'd walked up themselves, and the NPC answers
        /// whatever they say first. Opening with the NPC's <paramref name="reason"/>, and NPCs
        /// talking among themselves, come with A1 (docs/npc-memory-next-plan.md) - an NPC-only
        /// conversation has nobody to end it yet. False if it can't happen: another NPC, the
        /// player in another level, or the player already in a conversation.
        /// </summary>
        public bool NpcStartsConversation(NPC npc, Entity target, string reason)
        {
            if (target != player || npc.Location?.Name != location.Name) return false;
            if (Conversations.Active.Any(c => !c.IsFinished && c.Participants.Contains(player))) return false;
            StartConversation(player, npc);
            return true;
        }

        /// <summary>
        /// Whether the game has run since it was last saved (or loaded, or started), so quitting
        /// should ask first. Any frame of play counts: NPCs walk and time passes even with the
        /// player standing still. Cleared by a successful save.
        /// </summary>
        public bool HasUnsavedPlay { get; internal set; }

        private float _untilGossip = Gossip.IntervalSeconds;

        /// <summary>One step of the game: time, conversations, decisions, reflections, movement, collision.</summary>
        public void Update(GameTime gameTime)
        {
            HasUnsavedPlay = true;
            Clock.Update(gameTime);
            Conversations.Update(Clock.TotalDays);
            Reflections.Update();
            // Gossip every few seconds, not every frame: it's people chatting, not physics.
            if ((_untilGossip -= (float)gameTime.ElapsedGameTime.TotalSeconds) <= 0)
            {
                _untilGossip = Gossip.IntervalSeconds;
                Gossip.Step(Roster.Levels, Clock.TotalDays);
            }
            player.Update(gameTime);
            Bounds.Clamp(player);
            foreach (var npc in NPCs)
            {
                npc.Update(gameTime);
                npc.CheckEntityCollision(player);
                player.CheckEntityCollision(npc);
                Bounds.Clamp(npc);

                foreach (var other in NPCs)
                {
                    if (npc != other)
                        npc.CheckEntityCollision(other);
                }
            }
            foreach (var obj in location.objects)
            {
                // Boxes is the object's full multi-box shape; the resolver picks the
                // deepest hit.
                player.ResolveCollision(obj.Boxes);
                foreach (var npc in NPCs)
                    npc.ResolveCollision(obj.Boxes);
            }
        }
        public Entity FindPersonNear(NPC asker, int id)
        {
            Entity who = FindPerson(id);
            string level = who switch
            {
                NPC other => other.Location?.Name,
                Player    => location.Name,      // the player is always in the current level
                _         => null,
            };
            return level != null && level == asker.Location?.Name ? who : null;
        }
    }
}
