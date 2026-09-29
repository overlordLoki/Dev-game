using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Shared code for everything the player doesn't control: a name, a brain that
    /// decides the heading, and push-out collision. The subclass only brings its art,
    /// size and speed (see Knight).
    /// </summary>
    public abstract class NPC : Entity
    {
        protected NpcBrain Brain { get; }
        public string Name { get; }
        public string Portrait { get; set; }
        protected NPC(Vector2 position, int id, string name) : base(position, id)
        {
            Brain = new WanderBrain();
            this.Name = name;
            Brain.Start(this);
        }

        public override void Update(GameTime gameTime)
        {
            // TODO: NPC AI logic. Subclasses register at least "idle" and "walk".
            Play(Direction == Vector2.Zero ? "idle" : "walk");
            Brain.Update(this, gameTime);
            base.Update(gameTime);
        }

        /// <summary>
        /// Push out of another entity, then let the brain decide how to react (a
        /// wanderer bounces away).
        /// </summary>
        public override void CheckEntityCollision(IEntity other)
        {
            Rectangle me = Bounds;
            Rectangle them = other.Bounds;
            if (!me.Intersects(them)) return;

            var (pushX, pushY) = Overlap(me, them);
            bool horizontal = Math.Abs(pushX) < Math.Abs(pushY);
            if (horizontal)
                Position = new Vector2(Position.X + pushX, Position.Y);
            else
                Position = new Vector2(Position.X, Position.Y + pushY);

            Brain.OnBlocked(this, horizontal);
        }
    }
}
