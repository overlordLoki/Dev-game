

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Entities
{
    public class NPC : IEntity
    {
        public int id { get;}
        public Vector2 Position { get; set; }
        private Vector2 direction { get; set; }
        public Texture2D Texture { get; private set; }

        public float Speed { get; set; }
        public int Width  => (int)(Layout.CellSize * 0.9f);
        public int Height => (int)(Layout.CellSize * 0.9f);
        // Depth-sort key: the feet = bottom of the NPC's box.
        public int SortY => (int)(Position.Y + Height);
        public Rectangle Bounds
        {
            get
            {
                int w = (int)(Width * 0.5f);
                int h = (int)(Height * 0.4f);
                int x = (int)Position.X + (Width - w) / 2;
                int y = (int)Position.Y + (Height - h);
                return new Rectangle(x, y, w, h);
            }
        }
        public Color color { get; set; } = Color.Blue;
        Vector2 IEntity.direction { get => direction; set => direction = value; }

        public NPC(Texture2D texture2D, Vector2 pos, int id)
        {
            this.Texture = texture2D;
            this.Position = pos;
            Speed = 100f;
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            this.id = id;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Rectangle shape = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
            //(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
            spriteBatch.Draw(Texture, Position, shape, color);
        }

        public void Update(GameTime gameTime)
        {
            // TODO: NPC AI logic
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
                {
                    Position = new Vector2(Position.X + pushX, Position.Y);
                    direction = new Vector2(-direction.X, direction.Y);
                }
                else
                {
                    Position = new Vector2(Position.X, Position.Y + pushY);
                    direction = new Vector2(direction.X, -direction.Y);
                }
            }
        }
    }
}