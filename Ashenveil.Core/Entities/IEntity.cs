using System;
using System.Collections.Generic;
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
        IReadOnlyList<Rectangle> Boxes { get; }   // tight collision boxes (not the whole sprite)
        Rectangle Bounds { get; }                 // union of Boxes, the mover's body box
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
        /// <summary>
        /// Push out of a multi-box obstacle. Resolving against every overlapping box in
        /// one frame makes them fight and jitter, so this picks the single deepest overlap
        /// and resolves against that one - the rest sort themselves out over later frames.
        /// </summary>
        public void ResolveCollision(IReadOnlyList<Rectangle> others)
        {
            Rectangle me = Bounds;
            Rectangle deepest = Rectangle.Empty;
            float best = 0f;

            foreach (var other in others)
            {
                if (!me.Intersects(other)) continue;
                Rectangle o = Rectangle.Intersect(me, other);
                float penetration = Math.Min(o.Width, o.Height);
                if (penetration > best) { best = penetration; deepest = other; }
            }

            if (best > 0f) ResolveCollision(deepest);
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