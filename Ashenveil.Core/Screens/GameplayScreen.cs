using System;
using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Graphics;
using Ashenveil.Core.UI.Widgets;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// The screen the game is played on. The World is the game itself (levels, NPCs, time,
    /// conversations); this screen is everything about *showing* it and *playing* it: keys,
    /// the camera, the window size, drawing, and opening other screens (pause, chat).
    /// </summary>
    public class GameplayScreen : IScreen
    {
        public World World { get; }
        public List<Widget> Widgets { get; set; } = new List<Widget>();   // the HUD, in screen pixels

        // The player's health, top left. Placed by UpdateLayout, filled every frame.
        private readonly ProgressBar _healthBar = new(Rectangle.Empty, "red");
        // "You died", across the middle while the player is down, until they respawn.
        private readonly Label _deathLabel = new(Rectangle.Empty, "You died", Color.DarkRed) { Visible = false };
        private readonly SpriteFont _font;

        // The small bar over a hurt NPC: how wide and tall, and how far above the feet it sits,
        // in cells. Both characters stand about half a cell tall on screen, whatever their frame.
        private const float NpcBarWidth = 0.6f, NpcBarHeight = 0.07f, NpcBarAboveFeet = 0.7f;

        private readonly Camera _camera = new Camera();
        private readonly Action _onPause;
        private KeyboardState _prevKb;
        private int _viewWidth, _viewHeight;

        // The world draws in world coordinates; the camera shifts it onto the screen.
        public Matrix Transform => _camera.View;

        /// <param name="onPause">Opens the pause menu (P).</param>
        /// <param name="onTalk">Opens the chat box for a conversation the player is in.</param>
        /// <param name="font">For HUD text ("You died"). Null (tests): text widgets aren't drawn.</param>
        public GameplayScreen(World world, Action onPause, Action<Conversation> onTalk, SpriteFont font = null)
        {
            World = world;
            _onPause = onPause;
            _font = font;
            World.PlayerConversationStarted += convo => onTalk(convo);
            Widgets.Add(_healthBar);
            if (font != null) Widgets.Add(_deathLabel);
        }

        // Recompute cell size from the window, and remember the window size so the camera
        // can scroll. Call on load and whenever the window resizes.
        public void UpdateLayout(int viewportWidth, int viewportHeight)
        {
            _viewWidth = viewportWidth;
            _viewHeight = viewportHeight;
            Layout.Update(viewportHeight);

            // Sized from the window, like the chat box, so it stays in proportion after a resize.
            int margin = viewportHeight / 40;
            _healthBar.Place(new Rectangle(margin, margin, viewportHeight / 3, Math.Max(28, viewportHeight / 24)));
            _deathLabel.Place(new Rectangle(0, viewportHeight / 3, viewportWidth, viewportHeight / 6));
        }

        public void Update(GameTime gameTime)
        {
            HandleKeys();
            World.Update(gameTime);

            // Last, so the view follows where the player actually ended up this frame
            // rather than where they were before collisions pushed them back. The world is
            // in cells; the camera scrolls in screen pixels, so both convert here.
            var player = World.player;
            _camera.Follow(
                Layout.ToScreen(player.Position + new Vector2(player.Width / 2f, player.Height / 2f)),
                _viewWidth, _viewHeight, Layout.ToScreen(World.Bounds.Area));
        }

        // Edge-triggered, so one press is one action - held down, E would otherwise bounce
        // the player in and out of a door every frame.
        private void HandleKeys()
        {
            var kb = AshenveilGame.Input.Keyboard;
            if (kb.IsKeyDown(Keys.P) && _prevKb.IsKeyUp(Keys.P)) _onPause();
            if (kb.IsKeyDown(Keys.E) && _prevKb.IsKeyUp(Keys.E)) World.Interact();
            _prevKb = kb;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // 1. flat ground first — never occludes anything that stands up
            World.location.tileMap.Draw(spriteBatch);

            // 2. collect everything that stands up (player, NPCs, objects)
            var drawables = new List<IWorldDrawable>();
            drawables.Add(World.player);
            drawables.AddRange(World.NPCs);
            drawables.AddRange(World.location.objects);

            // 3. sort by base Y — lower on screen (bigger Y) draws last = in front
            drawables.Sort((a, b) => a.SortY.CompareTo(b.SortY));

            // 4. draw in that order
            foreach (var d in drawables)
                d.Draw(spriteBatch);

            // 5. health bars over hurt NPCs, above everything in the world
            DrawNpcHealth(spriteBatch, pixel);

            // 6. debug overlay on top of everything
            if (Debug.SHOWNAV) DrawNav(spriteBatch, pixel);

            // 7. the HUD. It's in screen pixels, not through the camera, so the batch the
            // ScreenManager began with our Transform is swapped for a plain one (it ends that).
            var health = World.player.Health;
            _healthBar.Visible = health != null;
            if (health != null) _healthBar.Value = health.Fraction;
            _deathLabel.Visible = World.player.IsDead;
            spriteBatch.End();
            spriteBatch.Begin();
            foreach (var widget in Widgets) widget.Draw(spriteBatch, _font);
        }

        // Only once they've been hurt, and not once they're dead: a full bar over everyone is noise.
        private void DrawNpcHealth(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var npc in World.NPCs)
            {
                var health = npc.Health;
                if (health == null || health.IsDead || health.Current == health.Max) continue;

                var feet = npc.Feet;
                var back = new RectF(feet.X - NpcBarWidth / 2, feet.Y - NpcBarAboveFeet, NpcBarWidth, NpcBarHeight);
                var fill = new RectF(back.X, back.Y, back.Width * health.Fraction, back.Height);
                spriteBatch.Draw(pixel, Layout.ToScreen(back), Color.Black * 0.6f);
                spriteBatch.Draw(pixel, Layout.ToScreen(fill), Color.Red);
            }
        }

        // Blocked cells as red squares, and each NPC's route as a yellow line from its feet
        // through its remaining waypoints. Shows *why* an NPC walks where it does.
        private void DrawNav(SpriteBatch spriteBatch, Texture2D pixel)
        {
            var nav = World.location.Nav;
            for (int row = 0; row < nav.Rows; row++)
                for (int col = 0; col < nav.Cols; col++)
                    if (!nav.IsWalkable(new Point(col, row)))
                        spriteBatch.Draw(pixel, Layout.ToScreen(new RectF(col, row, 1, 1)), Color.Red * 0.35f);

            foreach (var npc in World.NPCs)
            {
                Vector2 from = npc.Feet;
                foreach (var waypoint in npc.CurrentPath)
                {
                    Debug.DrawLine(spriteBatch, Layout.ToScreen(from), Layout.ToScreen(waypoint), Color.Yellow);
                    // A small square on each waypoint, a tenth of a cell across.
                    spriteBatch.Draw(pixel, Layout.ToScreen(new RectF(waypoint.X - 0.05f, waypoint.Y - 0.05f, 0.1f, 0.1f)), Color.Yellow);
                    from = waypoint;
                }
            }
        }
    }
}
