using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Tiles;
using Ashenveil.Core.Utility;
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
        public List<IEntity> NPCs = new List<IEntity>();
        private MouseState _prevMouse;
        private Texture2D _pixel;
        private Action _onPause;
        private KeyboardState _prevKb;
        private int _viewWidth, _viewHeight;
        public List<Location> locations = new List<Location>();

        // The world draws in world coordinates; the camera shifts it onto the screen.
        public Matrix Transform => camera.View;

        public World(Player player, Texture2D pixel, Action onPause)
        {
            this.player = player;
            this._pixel = pixel;
            this._onPause = onPause;
            var medows = new Medows();
            this.locations.Add(medows);
            // Keep entities inside the active map, in world space - see WorldBounds.
            this.worldBounds = new WorldBounds(medows.tileMap);
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
            foreach (Location loc in locations)
                loc.tileMap.Draw(spriteBatch);

            // 2. collect everything that stands up (player, NPCs, objects)
            var drawables = new List<IDrawable>();
            drawables.Add(player);
            drawables.AddRange(NPCs);
            foreach (Location loc in locations)
                drawables.AddRange(loc.objects);

            // 3. sort by base Y — lower on screen (bigger Y) draws last = in front
            drawables.Sort((a, b) => a.SortY.CompareTo(b.SortY));

            // 4. draw in that order
            foreach (var d in drawables)
                d.Draw(spriteBatch);
        }

        public void Update(GameTime gameTime)
        {
            player.Update(gameTime);
            worldBounds.Clamp(player);
            foreach (var npc in NPCs)
            {
                npc.Move(gameTime);
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
            foreach (var loc in locations)
            {
                foreach (var obj in loc.objects)
                {
                    // Cast needed: ResolveCollision lives only as a default method on
                    // IEntity, so it isn't visible through the Player type. Boxes is the
                    // object's full multi-box shape; the resolver picks the deepest hit.
                    ((IEntity)player).ResolveCollision(obj.Boxes);
                    foreach (var npc in NPCs)
                        npc.ResolveCollision(obj.Boxes);
                }
            }

            // Last, so the view follows where the player actually ended up this frame
            // rather than where they were before collisions pushed them back.
            camera.Follow(
                player.Position + new Vector2(player.Width / 2f, player.Height / 2f),
                _viewWidth, _viewHeight, worldBounds.Bounds);
        }
        private void OnMouseAction()
        {
            var mouse = Mouse.GetState();
            // if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
            // {
            //     // fired once on the frame the button goes down
            //     int clickX = mouse.X;
            //     int clickY = mouse.Y;
            //     //print
            //     Console.WriteLine($"Mouse position: {mouse.X}, {mouse.Y}");
            //     int id = NPCs.Count;
            //     //create a new npc at the location. //(Texture2D texture2D, Vector2 pos)
            //     NPC npc = new(_pixel, new Vector2(mouse.X, mouse.Y), id);
            //     NPCs.Add(npc);
            // }
            _prevMouse = mouse;  // always save at the end of Update
        }
        private void OnKeyAction()
        {
            var kb = Keyboard.GetState();
            if (kb.IsKeyDown(Keys.P) && _prevKb.IsKeyUp(Keys.P))
            {
                _onPause();
            }
            _prevKb = kb;
        }
    }
}