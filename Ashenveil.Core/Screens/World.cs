using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Dialogue;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Levels;
using Ashenveil.Core.Screens.Locations;
using Ashenveil.Core.Tiles;
using Ashenveil.Core.Utility;
using Ashenveil.Core.Utility.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    public class World : IScreen
    {
        public WorldBounds worldBounds;
        public Player player;
        public Camera camera = new Camera();
        public List<NPC> NPCs = new List<NPC>();
        public List<Widget> Widgets { get; set; } = new List<Widget>();
        private MouseState _prevMouse;
        private Texture2D _pixel;
        private Action _onPause;
        private Action<Conversation> _onTalk;
        private List<Conversation> _conversations = new List<Conversation>();
        private KeyboardState _prevKb;
        private int _viewWidth, _viewHeight;

        /// The area currently being played. Only ever one: entering a building swaps
        /// this rather than stacking a second screen, so the player, camera and pause
        /// stack all survive the transition untouched.
        public Location location;

        // The world draws in world coordinates; the camera shifts it onto the screen.
        public Matrix Transform => camera.View;

        public World(Texture2D pixel, Action onPause, Action<Conversation> onTalk)
        {
            this.player = new Player(new Vector2(100, 100), 0);
            this._pixel = pixel;
            this._onPause = onPause;
            this._onTalk = onTalk;
            SetLocation(new Location("Medows"));
        }

        /// <summary>
        /// Called on first load of a new game, player is spawned here.
        /// </summary>
        public void Init()
        {
            // spawn the player at the level's spawn point, which is a named point of interest
            var player_spawn_point = location.Poi("Player_spawn");
            Spawn(player, "Player_spawn");

            // add a knight npc to the world
            var knight = new Knight(new Vector2(200, 200), 1, "Sir Lancelot");
            Spawn(knight, "Knight_spawn");
            NPCs.Add(knight);
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
        /// Makes <paramref name="next"/> the active area and re-fences the world to its
        /// grid. WorldBounds is rebuilt because it holds the map it clamps against; it
        /// still reads cell size from Layout, so resizes remain its own business.
        /// </summary>
        private void SetLocation(Location next)
        {
            location = next;
            worldBounds = new WorldBounds(next.tileMap);
        }

        /// <summary>
        /// Walks through a doorway: loads the target level and stands the player on its
        /// spawn cell. The destination's own exit sits under that cell, which is what
        /// lets the player turn round and walk back out.
        /// </summary>
        public void UseExit(ExitData exit)
        {
            SetLocation(new Location(exit.to));

            // Centre the player in the spawn cell rather than at its corner, so they
            // don't start half-inside whatever is standing on the neighbouring tile.
            player.Position = new Vector2(
                exit.spawnCol * Layout.CellWidth + (Layout.CellWidth - player.Width) / 2f,
                exit.spawnRow * Layout.CellHeight + (Layout.CellHeight - player.Height) / 2f);
        }

        /// <summary>
        /// The one place a conversation begins, whoever starts it. Only the player's
        /// conversations get the chat box; the rest just live in the list.
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
            req.participants.Add(new ParticipantDto {
                name = (from as NPC)?.Name ?? Conversation.PlayerName, speaks = false });
            // Every NPC is a speaker, with their persona.
            foreach (var npc in others)
                req.participants.Add(new ParticipantDto { name = npc.Name, persona = npc.Persona });

            var convo = new Conversation(everyone, location, req, DialogueApi.RequestAsync);
            // The opener is already "said"; the LLM continues from it once the player answers.
            convo.Add(others[0].Name, others[0].Greeting);

            foreach (var npc in others) npc.StartTalking();
            _conversations.Add(convo);
            if (from == player) _onTalk(convo);
            return convo;
        }

        // Lets everyone in a finished conversation go back to what they were doing.
        private void EndFinishedConversations()
        {
            foreach (var convo in _conversations.Where(c => c.IsFinished).ToList())
            {
                foreach (var entity in convo.Participants)
                    if (entity is NPC npc) npc.StopTalking();
                _conversations.Remove(convo);
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

        // Recompute cell size from the window, and remember the window size so Update
        // can scroll the camera. Call on load and whenever the window resizes.
        public void UpdateLayout(int viewportWidth, int viewportHeight)
        {
            _viewWidth = viewportWidth;
            _viewHeight = viewportHeight;
            Layout.Update(viewportHeight);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // 1. flat ground first — never occludes anything that stands up
            location.tileMap.Draw(spriteBatch);

            // 2. collect everything that stands up (player, NPCs, objects)
            var drawables = new List<IDrawable>();
            drawables.Add(player);
            drawables.AddRange(NPCs);
            drawables.AddRange(location.objects);

            // 3. sort by base Y — lower on screen (bigger Y) draws last = in front
            drawables.Sort((a, b) => a.SortY.CompareTo(b.SortY));

            // 4. draw in that order
            foreach (var d in drawables)
                d.Draw(spriteBatch);
        }

        public void Update(GameTime gameTime)
        {
            EndFinishedConversations();
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
            OnMouseAction();
            OnKeyAction();
            foreach (var obj in location.objects)
            {
                // Boxes is the object's full multi-box shape; the resolver picks the
                // deepest hit.
                player.ResolveCollision(obj.Boxes);
                foreach (var npc in NPCs)
                    npc.ResolveCollision(obj.Boxes);
            }

            // Last, so the view follows where the player actually ended up this frame
            // rather than where they were before collisions pushed them back.
            camera.Follow(
                player.Position + new Vector2(player.Width / 2f, player.Height / 2f),
                _viewWidth, _viewHeight, worldBounds.Bounds);
        }
        private void OnMouseAction()
        {
            var mouse = AshenveilGame.Input.Mouse;
            _prevMouse = mouse;  // always save at the end of Update
        }
        private void OnKeyAction()
        {
            var kb = AshenveilGame.Input.Keyboard;
            if (kb.IsKeyDown(Keys.P) && _prevKb.IsKeyUp(Keys.P))
            {
                _onPause();
            }
            // Edge-triggered, so one press is one transition - held down it would
            // otherwise bounce the player in and out of the door every frame.
            if (kb.IsKeyDown(Keys.E) && _prevKb.IsKeyUp(Keys.E))
            {
                // Talking wins over doors when an NPC is within reach.
                var npc = player.GetNearestEntityInRange(NPCs, Layout.CellWidth * 1.5f) as NPC;
                if (npc != null) StartConversation(player, npc);
                else
                {
                    var (col, row) = PlayerCell();
                    var exit = location.ExitAt(col, row);
                    if (exit != null) UseExit(exit);
                }
            }
            _prevKb = kb;
        }
    }
}