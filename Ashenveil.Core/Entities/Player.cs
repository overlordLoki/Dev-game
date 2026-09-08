using System.Collections.Generic;
using Ashenveil.Core.Collision;
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
        // Tight collision box(es) around the body/feet. The sprite frame has transparent
        // padding, so these are tuned by eye in the editor against the debug outline and
        // stored under "Player" in Content/bounds.json (basis = the sprite's own size).
        public IReadOnlyList<Rectangle> Boxes
        {
            get
            {
                var boxes = BoundsStore.For(nameof(Player));
                var rects = new Rectangle[boxes.Count];
                for (int i = 0; i < boxes.Count; i++)
                    rects[i] = boxes[i].ToRectangle((int)Position.X, (int)Position.Y, Width, Height);
                return rects;
            }
        }

        public Rectangle Bounds => Geometry.Union(Boxes);
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

        // Push out of another entity, tight box against tight box. This deliberately
        // isn't IEntity's default CheckEntityCollision: that one also flips `direction`
        // to bounce the mover away, which is right for a wandering NPC and meaningless
        // for the player, whose direction comes from the keyboard. ResolveCollision is
        // the same push-out without the bounce.
        public void CheckEntityCollision(IEntity entity) =>
            ((IEntity)this).ResolveCollision(entity.Bounds);

        public void Draw(SpriteBatch spriteBatch)
        {
            Rectangle dest = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
            animations[CurrentAnim].Draw(spriteBatch, dest);
            Debug.DrawRect(spriteBatch, Bounds, Color.Black);
        }
    }
}