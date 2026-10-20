using System;
using System.Collections.Generic;
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
        /// Called on first load of a new game: stands the player on the spawn point and brings
        /// in the level's starting NPCs. Layout must already have a cell size (the game sets it
        /// from the window; tests call Layout.Update).
        /// </summary>
        public void Init()
        {
            location.PlaceAt(player, "Player_spawn");
            Roster.Populate(location);
        }

        /// <summary>
        /// Puts an NPC in the level being played. Tests use this, and a future debug-console
        /// "spawn" command can too.
        /// </summary>
        public void AddNpc(NPC npc) => Roster.Add(location.Name, npc);

        /// <summary>
        /// Makes <paramref name="next"/> the active area and re-fences the world to its
        /// grid. WorldBounds is rebuilt because it holds the map it clamps against; it
        /// still reads cell size from Layout, so resizes remain its own business.
        /// </summary>
        private void SetLocation(Location next)
        {
            location = next;
            Bounds = new WorldBounds(next.tileMap);
            // No NPC spawning here - Layout may not be ready yet (the constructor calls this).
            // Init and UseExit call Roster.Populate once it is.
        }

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
            Roster.Populate(location);   // first visit spawns, return visit reuses

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
            var convo = Conversations.Start(from, others, location, Clock.TotalDays);
            if (from == player) PlayerConversationStarted?.Invoke(convo);
            return convo;
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
            Clock.Update(gameTime);
            Conversations.Update(Clock.TotalDays);
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
    }
}
