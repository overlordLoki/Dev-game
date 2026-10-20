using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
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
        /// <summary>Every conversation, and what NPCs learn from them.</summary>
        public ConversationSystem Conversations { get; }

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
            Conversations = new ConversationSystem(sendChat, requestDecision);
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
        /// The one place a conversation begins, whoever starts it. The ConversationSystem does
        /// the work; the World adds the one rule that's about the player: if they're in it,
        /// raise <see cref="PlayerConversationStarted"/> so the screen can open the chat box.
        /// </summary>
        public Conversation StartConversation(Entity from, params NPC[] others)
        {
            var convo = Conversations.Start(from, others, location, CurrentDate.TotalDays);
            if (from == player) PlayerConversationStarted?.Invoke(convo);
            return convo;
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
            Conversations.Update(CurrentDate.TotalDays);
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
