using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Task;
using Ashenveil.Core.Levels;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Tiles;
using Ashenveil.Core.Utility;
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
        private static readonly string[] Seasons = { "Spring", "Summer", "Fall", "Winter" };
        private const float SecondsPerDay = 600f; // 10 real minutes = 1 game day

        public WorldBounds worldBounds;
        public Player player;
        public List<NPC> NPCs;
        private List<Conversation> _conversations = new List<Conversation>();
        // Decisions in flight: an end-of-conversation LLM call per NPC, with the level it
        // was had in (so a go_to resolves against the right POIs even if the player wanders
        // off). We hold the Task so it isn't GC'd, and apply the result on the main thread.
        // Each one also carries the conversation's transcript, so the decision's summary and
        // facts can be tied back to it when it lands.
        private List<(NPC npc, Location level, Transcript transcript, System.Threading.Tasks.Task<NpcDecisionDto> task)> _pendingDecisions = new();
        // Hands out transcript ids. Only counts up, so a Memory.SourceId always points at the
        // right conversation.
        private int _nextTranscriptId = 1;
        // How many past-conversation memories to feed into a new conversation's prompt.
        private const int MaxInjectedMemories = 5;
        // How many earlier conversations (as one-line summaries) to recall into a new one.
        private const int MaxRecalledConversations = 3;

        // How the world talks to the LLM backend. Handed in rather than called directly, so
        // tests can pass fakes and never touch the network (same idea as Conversation's send).
        private readonly Func<ConversationRequestDto, System.Threading.Tasks.Task<ChatMessage[]>> _sendChat;
        private readonly Func<DecisionRequestDto, System.Threading.Tasks.Task<NpcDecisionDto>> _requestDecision;

        /// <summary>
        /// Raised when a conversation the player is part of begins, so whoever is showing the
        /// world (GameplayScreen) can open the chat box. The world itself never opens screens.
        /// </summary>
        public event Action<Conversation> PlayerConversationStarted;

        // Time tracking: 10 real minutes = 1 game day
        private float _elapsedSeconds;
        public GameDate CurrentDate { get; private set; } = new GameDate();

        public int TotalDays => CurrentDate.TotalDays;
        public int Year => CurrentDate.Year;
        public int Day => CurrentDate.Day;
        public int Season => CurrentDate.Season;
        public string SeasonName => Seasons[Season];
        public string DateDisplay => $"{SeasonName} {Day}, Year {Year}";
        // Every level's NPCs, kept alive across level changes so returning to a level
        // gives back the same instances (and later, their accumulated memory).
        private Dictionary<string, List<NPC>> _npcsByLevel = new();
        // Which levels have already had their first-visit spawn run, so we don't respawn on return.
        private HashSet<string> _populated = new();
        // Hands out NPC ids. Only ever counts up, so ids stay unique across levels and
        // despawns for the whole session. 0 is the player; NPCs start at 1.
        private int _nextNpcId = 1;
        private int NextId() => _nextNpcId++;

        /// The area currently being played. Only ever one: entering a building swaps
        /// this rather than stacking a second screen, so the player, camera and pause
        /// stack all survive the transition untouched.
        public Location location;

        /// <param name="start">The level the game begins in.</param>
        /// <param name="sendChat">How a chat reply is asked for. Defaults to the real API.</param>
        /// <param name="requestDecision">How an end-of-conversation decision is asked for. Defaults to the real API.</param>
        public World(Location start,
                     Func<ConversationRequestDto, System.Threading.Tasks.Task<ChatMessage[]>> sendChat = null,
                     Func<DecisionRequestDto, System.Threading.Tasks.Task<NpcDecisionDto>> requestDecision = null)
        {
            _sendChat = sendChat ?? DialogueApi.RequestAsync;
            _requestDecision = requestDecision ?? DialogueApi.RequestDecisionAsync;
            this.player = new Player(new Vector2(100, 100), 0);
            SetLocation(start);
        }

        /// <summary>
        /// Called on first load of a new game, player is spawned here. Layout must already
        /// have a cell size (the game sets it from the window; tests call Layout.Update).
        /// </summary>
        public void Init()
        {
            Spawn(player, "Player_spawn");
            EnsurePopulated();
        }

        public void Spawn(Entity entity, String poi_name)
        {
            PointOfInterest poi;
            if(location.Poi(poi_name) == null)
            {
                poi = location.Poi("default");
            }
            else
            {
                poi = location.Poi(poi_name);
            }

            entity.Position = poi.ToPixel() - new Vector2(entity.Width / 2f, entity.Height / 2f);

        }

        /// <summary>
        /// Puts an NPC in the current level. Level spawns use this, and so can tests (and a
        /// future debug-console "spawn" command).
        /// </summary>
        public void AddNpc(NPC npc) => NPCs.Add(npc);

        /// <summary>
        /// Makes <paramref name="next"/> the active area and re-fences the world to its
        /// grid. WorldBounds is rebuilt because it holds the map it clamps against; it
        /// still reads cell size from Layout, so resizes remain its own business.
        /// </summary>
        private void SetLocation(Location next)
        {
            location = next;
            worldBounds = new WorldBounds(next.tileMap);

            // Point NPCs at this level's bucket, creating an empty one on first visit.
            // No spawning here — Layout may not be ready yet (we're called from the ctor).
            if (!_npcsByLevel.TryGetValue(next.Name, out var bucket))
            {
                bucket = new List<NPC>();
                _npcsByLevel[next.Name] = bucket;
            }
            NPCs = bucket;
        }
        // First-visit spawn for the current level. Safe to call every time you enter a
        // level — it only does work the first time. Must run after Layout is ready.
        private void EnsurePopulated()
        {
            if (_populated.Contains(location.Name)) return;
            _populated.Add(location.Name);
            SpawnLevelNpcs(location.Name);
        }

        // What NPCs each level starts with. Hardcoded per level for now; later this reads
        // from the level's JSON the same way POIs and exits already do.
        private void SpawnLevelNpcs(string levelName)
        {
            switch (levelName)
            {
                case "Medows":
                    var knight = new Knight(new Vector2(200, 200), NextId(), "Sir Lancelot", new KnightBrain());
                    Spawn(knight, "Knight_spawn");
                    AddNpc(knight);
                    knight.GiveTask(new GoToTask(location.Poi("entry").ToPixel()));
                    knight.GiveTask(new WaitTask(3f));
                    knight.GiveTask(new GoToTask(location.Poi("Knight_spawn").ToPixel()));
                    break;
            }
        }

        /// <summary>
        /// Finds an NPC by id across every level, not just the current one, so a lookup
        /// works even while the player is somewhere else. Null if no NPC has that id.
        /// </summary>
        public NPC FindNpc(int id) =>
            _npcsByLevel.Values.SelectMany(bucket => bucket).FirstOrDefault(npc => npc.Id == id);

        /// <summary>
        /// The player's "interact" action (the E key): talk to an NPC in reach, otherwise use
        /// the exit underfoot. Returns the conversation it started, or null (an exit was used,
        /// or there was nothing to do). Talking wins over doors when an NPC is within reach.
        /// </summary>
        public Conversation Interact()
        {
            var npc = player.GetNearestEntityInRange(NPCs, Layout.CellWidth * 1.5f) as NPC;
            if (npc != null) return StartConversation(player, npc);

            var (col, row) = PlayerCell();
            var exit = location.ExitAt(col, row);
            if (exit != null) UseExit(exit);
            return null;
        }

        /// <summary>
        /// Walks through a doorway: loads the target level and stands the player on its
        /// spawn cell. The destination's own exit sits under that cell, which is what
        /// lets the player turn round and walk back out.
        /// </summary>
        public void UseExit(ExitData exit)
        {
            SetLocation(new Location(exit.to));
            EnsurePopulated();          // first visit spawns, return visit reuses

            // Centre the player in the spawn cell rather than at its corner, so they
            // don't start half-inside whatever is standing on the neighbouring tile.
            player.Position = new Vector2(
                exit.spawnCol * Layout.CellWidth + (Layout.CellWidth - player.Width) / 2f,
                exit.spawnRow * Layout.CellHeight + (Layout.CellHeight - player.Height) / 2f);
        }

        /// <summary>
        /// The one place a conversation begins, whoever starts it. Conversations the player
        /// is in raise <see cref="PlayerConversationStarted"/>; the rest just live in the list.
        /// </summary>
        public Conversation StartConversation(Entity from, params NPC[] others)
        {
            // Everyone in the room, initiator first (matches your "first one started it" rule).
            var everyone = new Entity[others.Length + 1];
            everyone[0] = from;
            others.CopyTo(everyone, 1);

            var req = new ConversationRequestDto {
                participants = new List<ParticipantDto>(),
                location = location.Name,
                // One reply per thing said, so it reads as a back-and-forth.
                max_lines = others.Length,
            };
            // The initiator is present but listening (your player drives, doesn't get lines).
            string initiator = (from as NPC)?.Name ?? Conversation.PlayerName;
            req.participants.Add(new ParticipantDto { name = initiator, speaks = false });
            // Every NPC is a speaker, with their persona and what they remember of past
            // conversations - attached to them, so the prompt frames it as their knowledge.
            foreach (var npc in others)
                req.participants.Add(new ParticipantDto
                {
                    name = npc.Name,
                    persona = npc.Persona,
                    memories = npc.RecentMemories(MaxInjectedMemories).ToList(),
                    past_conversations = npc.PastConversationsWith(
                        initiator, MaxRecalledConversations, CurrentDate.TotalDays).ToList(),
                });

            var convo = new Conversation(everyone, location, req, _sendChat);
            // The opener is already "said"; the LLM continues from it once the player answers.
            convo.Add(others[0].Name, others[0].Greeting);

            foreach (var npc in others) npc.StartTalking();
            _conversations.Add(convo);
            if (from == player) PlayerConversationStarted?.Invoke(convo);
            return convo;
        }

        // Lets everyone in a finished conversation go back to what they were doing, and asks
        // the LLM, per NPC, what they now remember and want to do next.
        private void EndFinishedConversations()
        {
            foreach (var convo in _conversations.Where(c => c.IsFinished).ToList())
            {
                var npcs = convo.Participants.OfType<NPC>().ToList();
                foreach (var npc in npcs) npc.StopTalking();

                // One transcript per conversation, shared by everyone in it; then each NPC
                // asks what it learned. Skipped entirely if there was nothing to learn.
                var transcript = RecordConversation(convo, npcs);
                if (transcript != null)
                    foreach (var npc in npcs) FireDecision(npc, convo, transcript);

                _conversations.Remove(convo);
            }
        }

        // Snapshots a finished conversation and files it in the cold-tier memory of every NPC
        // that took part. Null when nobody but the NPCs spoke (opened and closed the chat):
        // there's nothing to remember, and asking the LLM anyway gets filler memories
        // ("the traveller was present") and a wasted call.
        private Transcript RecordConversation(Conversation convo, List<NPC> npcs)
        {
            var npcNames = npcs.Select(n => n.Name).ToHashSet();
            bool anyoneElseSpoke = convo.Messages.Any(m => m.Speaker != null && !npcNames.Contains(m.Speaker));
            if (!anyoneElseSpoke) return null;

            var transcript = convo.ToTranscript(_nextTranscriptId++, CurrentDate.TotalDays);
            foreach (var npc in npcs) npc.RememberConversation(transcript);
            return transcript;
        }

        // Kicks off the end-of-conversation decision for one NPC. Fire-and-hold: we keep the
        // Task so it isn't collected, and PollDecisions applies it when it lands.
        private void FireDecision(NPC npc, Conversation convo, Transcript transcript)
        {
            var req = new DecisionRequestDto
            {
                npc_name = npc.Name,
                persona = npc.Persona,
                location = transcript.Location,
                transcript = transcript.Lines.ToArray(),   // already only what was actually said
                poi_names = convo.Location?.pointsOfInterest.Keys.ToArray() ?? Array.Empty<string>(),
            };
            _pendingDecisions.Add((npc, convo.Location, transcript, _requestDecision(req)));
        }

        // Apply any decisions that have come back. Runs on the main thread (from Update), so
        // mutating the NPC's brain here is safe. A failed call is dropped - the NPC just
        // doesn't learn from that conversation.
        private void PollDecisions()
        {
            for (int i = _pendingDecisions.Count - 1; i >= 0; i--)
            {
                var (npc, level, transcript, task) = _pendingDecisions[i];
                if (!task.IsCompleted) continue;
                if (task.IsCompletedSuccessfully) npc.ApplyDecision(task.Result, level, transcript);
                _pendingDecisions.RemoveAt(i);
            }
        }

        // Advance game time: 10 real minutes = 1 game day
        private void AdvanceTime(GameTime gameTime)
        {
            _elapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

            while (_elapsedSeconds >= SecondsPerDay)
            {
                _elapsedSeconds -= SecondsPerDay;
                CurrentDate.AdvanceDay();
            }
        }

        /// <summary>
        /// Which cell the player is standing in, measured from their feet - the bottom
        /// centre of the collision box, not the sprite, which has transparent padding
        /// and would report the cell above.
        /// </summary>
        private (int col, int row) PlayerCell()
        {
            Rectangle body = player.Bounds;
            return (body.Center.X / Layout.CellWidth, body.Bottom / Layout.CellHeight);
        }

        /// <summary>One step of the game: time, conversations, decisions, movement, collision.</summary>
        public void Update(GameTime gameTime)
        {
            AdvanceTime(gameTime);
            EndFinishedConversations();
            PollDecisions();
            player.Update(gameTime);
            worldBounds.Clamp(player);
            foreach (var npc in NPCs)
            {
                npc.Update(gameTime);
                npc.CheckEntityCollision(player);
                player.CheckEntityCollision(npc);
                worldBounds.Clamp(npc);

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
    }
}
