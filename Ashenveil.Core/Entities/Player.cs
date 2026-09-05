using System;
using System.Collections;
using System.Collections.Generic;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Entities
{
    public class Player : IEntity
    {
        public int id { get;}
        public Vector2 Position { get; set; }
        public Texture2D Texture { get; private set; }
        public float Speed { get; set; } = 200f;
        // ~90% of a tile cell, so the player reads as roughly one tile in size.
        public int Height => (int)(Layout.CellSize * 0.9f);
        public int Width  => (int)(Layout.CellSize * 0.9f);
        // Depth-sort key: the feet = bottom of the player's box.
        public int SortY => (int)(Position.Y + Height);
        // Tight collision box around the body/feet. The sprite frame has transparent
        // padding, so tune these knobs by eye against the debug box.
        public Rectangle Bounds
        {
            get
            {
                const float boxW      = 0.35f;  // width  as fraction of Width
                const float boxH      = 0.28f;  // height as fraction of Height
                const float footInset = 0.12f;  // lift box UP off the bottom edge
                const float xShift    = 0.0f;   // + right, - left (fraction of Width)

                int w = (int)(Width * boxW);
                int h = (int)(Height * boxH);
                int x = (int)Position.X + (Width - w) / 2 + (int)(Width * xShift);
                int y = (int)Position.Y + Height - h - (int)(Height * footInset);
                return new Rectangle(x, y, w, h);
            }
        }
        public Color color { get; set; } = Color.Red;
        public Vector2 direction { get; set; }
        public Dictionary<string, Animation> animations { get; set; } = new();
        public string CurrentAnim = "";

        public Player(Vector2 vector2, int id)
        {
            this.Position = vector2;
            this.id = id;
            //Texture = Textures.Get(imgName);
            var playerSheet = Textures.Get(Assets.PLAYERIDLE.Path);
            this.Texture = playerSheet;
            // texture2D is the idle sheet — build the idle animation from it
            animations["idle"] = new Animation(playerSheet, Assets.PLAYERIDLE.FrameCount, Assets.PLAYERIDLE.FrameSize, 0.15);
            CurrentAnim = "idle";
        }
        public void Update(GameTime gameTime)
        {
            Move(gameTime);
            animations[CurrentAnim].Update(gameTime);
        }

        public void Move(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var kb = Keyboard.GetState();

            if (kb.IsKeyDown(Keys.Right)) Position = new Vector2(Position.X + Speed * delta, Position.Y);
            if (kb.IsKeyDown(Keys.Left))  Position = new Vector2(Position.X - Speed * delta, Position.Y);
            if (kb.IsKeyDown(Keys.Up))    Position = new Vector2(Position.X, Position.Y - Speed * delta);
            if (kb.IsKeyDown(Keys.Down))  Position = new Vector2(Position.X, Position.Y + Speed * delta);
        }

        public void CheckEntityCollision(IEntity entity)
        {
            Rectangle myBounds = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
            Rectangle otherBounds = new Rectangle((int)entity.Position.X, (int)entity.Position.Y, entity.Width, entity.Height);

            if (myBounds.Intersects(otherBounds))
            {
                float overlapLeft   = myBounds.Right  - otherBounds.Left;
                float overlapRight  = otherBounds.Right  - myBounds.Left;
                float overlapTop    = myBounds.Bottom - otherBounds.Top;
                float overlapBottom = otherBounds.Bottom - myBounds.Top;

                float pushX = overlapLeft < overlapRight ? -overlapLeft : overlapRight;
                float pushY = overlapTop  < overlapBottom ? -overlapTop : overlapBottom;

                if (Math.Abs(pushX) < Math.Abs(pushY))
                    Position = new Vector2(Position.X + pushX, Position.Y);
                else
                    Position = new Vector2(Position.X, Position.Y + pushY);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Rectangle dest = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
            animations[CurrentAnim].Draw(spriteBatch, dest);
            Debug.DrawRect(spriteBatch, Bounds, Color.Black);
        }
    }
}