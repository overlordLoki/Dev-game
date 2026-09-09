using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    public class World : IScreen
    {
        public WorldBounds worldBounds;
        public Player player;
        public List<IEntity> NPCs = new List<IEntity>();
        private MouseState _prevMouse;
        private Texture2D _pixel;
        private Action _onPause;
        private KeyboardState _prevKb;
        public List<Location> locations = new List<Location>();

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

        // Recompute cell size from the window + the active location's grid.
        // Call on load and whenever the window resizes.
        public void UpdateLayout(int viewportWidth, int viewportHeight)
        {
            if (locations.Count == 0) return;
            var map = locations[0].tileMap;
            Layout.Update(viewportWidth, viewportHeight, map.Cols, map.Rows);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            var vp = spriteBatch.GraphicsDevice.Viewport;
            spriteBatch.Draw(pixel, new Rectangle(0, 0, vp.Width, vp.Height), Color.Black);

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