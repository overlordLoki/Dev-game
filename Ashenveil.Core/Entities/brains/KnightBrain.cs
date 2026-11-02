using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains
{
    public class KnightBrain : NpcBrain
    {
        /// <summary>How often he raises his guard against a swing coming his way.</summary>
        public const double DefendChance = 0.35;

        private readonly Random _random;

        /// <param name="random">Decides when he defends. Tests pass a seeded one; the game, Random.Shared.</param>
        public KnightBrain(Random random = null)
        {
            _random = random ?? Random.Shared;
            this.State = NPCState.Idle;
        }

        protected override void UpdateDefault(GameTime gameTime)
        {

        }

        // Sometimes: a guard on every swing would make him impossible to hit.
        protected override bool ShouldDefend(Entity who) => _random.NextDouble() < DefendChance;
    }
}
