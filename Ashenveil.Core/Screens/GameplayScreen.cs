using System;
using System.Collections.Generic;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Graphics;
using Ashenveil.Core.UI.Widgets;
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
        public List<Widget> Widgets { get; set; } = new List<Widget>();   // HUD goes here

        private readonly Camera _camera = new Camera();
        private readonly Action _onPause;
        private KeyboardState _prevKb;
        private int _viewWidth, _viewHeight;

        // The world draws in world coordinates; the camera shifts it onto the screen.
        public Matrix Transform => _camera.View;

        /// <param name="onPause">Opens the pause menu (P).</param>
        /// <param name="onTalk">Opens the chat box for a conversation the player is in.</param>
        public GameplayScreen(World world, Action onPause, Action<Conversation> onTalk)
        {
            World = world;
            _onPause = onPause;
            World.PlayerConversationStarted += convo => onTalk(convo);
        }

        // Recompute cell size from the window, and remember the window size so the camera
        // can scroll. Call on load and whenever the window resizes.
        public void UpdateLayout(int viewportWidth, int viewportHeight)
        {
            _viewWidth = viewportWidth;
            _viewHeight = viewportHeight;
            Layout.Update(viewportHeight);
        }

        public void Update(GameTime gameTime)
        {
            HandleKeys();
            World.Update(gameTime);

            // Last, so the view follows where the player actually ended up this frame
            // rather than where they were before collisions pushed them back.
            var player = World.player;
            _camera.Follow(
                player.Position + new Vector2(player.Width / 2f, player.Height / 2f),
                _viewWidth, _viewHeight, World.worldBounds.Bounds);
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
        }
    }
}
