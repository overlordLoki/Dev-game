using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Entities
{
    public interface IEntity : IDrawable
    {
        int id { get;}
        Vector2 Position { get; set; }
        Texture2D Texture { get; }
        float Speed { get; set; }
        int Width { get; }
        int Height { get; }
        Rectangle Bounds { get; }        // tight collision box (not the whole sprite)
        Vector2 direction { get; set; }

        Color color { get; set; }
        // Draw and SortY come from IDrawable
        void Update(GameTime gameTime);
        void Move(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Position += direction * Speed * delta;
        }
        public void CheckEntityCollision(IEntity entity)
        {
            Rectangle myBounds = Bounds;
            Rectangle otherBounds = entity.Bounds;

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
        public void ResolveCollision(Rectangle other)
        {
            Rectangle me = Bounds;
            if (!me.Intersects(other)) return;

            float overlapLeft   = me.Right  - other.Left;
            float overlapRight  = other.Right - me.Left;
            float overlapTop    = me.Bottom - other.Top;
            float overlapBottom = other.Bottom - me.Top;
            float pushX = overlapLeft < overlapRight ? -overlapLeft : overlapRight;
            float pushY = overlapTop  < overlapBottom ? -overlapTop : overlapBottom;

            if (Math.Abs(pushX) < Math.Abs(pushY))
                Position = new Vector2(Position.X + pushX, Position.Y);
            else
                Position = new Vector2(Position.X, Position.Y + pushY);
        }
    }
}