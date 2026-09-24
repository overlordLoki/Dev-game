using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Shared code for everything the player doesn't control. For now that's wandering:
    /// pick a random heading, walk it, and bounce off whatever gets in the way. The
    /// subclass only brings its art, size and speed (see Knight).
    /// </summary>
    public abstract class NPC : Entity
    {
        protected NpcBrain Brain { get; }
        public string Name { get; }
        protected NPC(Vector2 position, int id, string name) : base(position, id)
        {
            Brain = new WanderBrain();
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            this.Name = name;
        }

        public override void Update(GameTime gameTime)
        {
            // TODO: NPC AI logic. Subclasses register at least "idle" and "walk".
            Play(Direction == Vector2.Zero ? "idle" : "walk");
            base.Update(gameTime);
        }

        /// <summary>
        /// Push out of another entity and bounce off it by flipping Direction on the
        /// pushed axis, so a wanderer turns away instead of pressing into it.
        /// </summary>
        public override void CheckEntityCollision(IEntity other)
        {
            Rectangle me = Bounds;
            Rectangle them = other.Bounds;
            if (!me.Intersects(them)) return;

            var (pushX, pushY) = Overlap(me, them);
            if (Math.Abs(pushX) < Math.Abs(pushY))
            {
                Position = new Vector2(Position.X + pushX, Position.Y);
                Direction = new Vector2(-Direction.X, Direction.Y);
            }
            else
            {
                Position = new Vector2(Position.X, Position.Y + pushY);
                Direction = new Vector2(Direction.X, -Direction.Y);
            }
        }
    }
}
