using System;
using Ashenveil.Core.Entities.brains.Jobs;
using Ashenveil.Core.GameWorld;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains
{
    /// <summary>
    /// A brain for things that attack on sight (a bandit): it hangs about near home, goes for
    /// anyone it doesn't like (wary is enough, not just hostile), and if a chase drags it more than
    /// <see cref="LeashDistance"/> from home it gives up and walks back, leaving people alone
    /// until it's there.
    /// </summary>
    public class EnemyBrain : NpcBrain
    {
        /// <summary>How far from home it ambles when nothing's going on, in cells.</summary>
        public const float WanderRadius = 1.5f;
        /// <summary>A chase that takes it further than this from home is dropped, in cells.</summary>
        public const float LeashDistance = 6f;
        /// <summary>How close it spots someone to attack, in cells (a bit shorter than an NPC's usual).</summary>
        public const float EnemyAggroRange = 3.5f;
        // Ambling is slower than walking somewhere: a share of Speed.
        private const float AmbleFactor = 0.6f;

        /// <summary>Where it hangs about: where it first stood (its spawn, or where a save put it).</summary>
        public Vector2? Home { get; set; }

        private readonly Random _random;
        private float _untilTurn;
        // Walking home after a leash: doesn't pick new fights until it's there.
        private bool _returning;

        /// <param name="random">How it ambles. Tests pass a seeded one; the game, Random.Shared.</param>
        public EnemyBrain(Random random = null)
        {
            _random = random ?? Random.Shared;
            State = NPCState.Walking;
        }

        public bool IsReturning => _returning;

        public override float AggroRange => EnemyAggroRange;
        public override bool WouldAttack(Attitude attitude) =>
            !_returning && attitude is Attitude.Hostile or Attitude.Wary;

        protected override void UpdateDefault(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Home ??= Npc.Feet;

            Vector2 away = Npc.Feet - Home.Value;
            if (away.Length() > WanderRadius)
            {
                Npc.Direction = Vector2.Normalize(-away) * AmbleFactor;   // drifted off: back towards home
                return;
            }
            _returning = false;   // home again

            if ((_untilTurn -= dt) > 0) return;
            _untilTurn = 1.5f + (float)_random.NextDouble() * 2f;
            if (_random.NextDouble() < 0.4) Npc.Direction = Vector2.Zero;   // stands about a while
            else
            {
                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                Npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * AmbleFactor;
            }
        }

        protected override void UpdateFighting(float dt)
        {
            Home ??= Npc.Feet;
            if (Vector2.Distance(Npc.Feet, Home.Value) > LeashDistance)
            {
                StopFighting();
                _returning = true;
                Enqueue(new GoToJob(Home.Value));
                return;
            }
            base.UpdateFighting(dt);
        }

        // Bounce off whatever it walks into, like a wanderer.
        public override void OnBlocked(bool horizontal)
        {
            if (State == NPCState.Fighting) return;
            Npc.Direction = horizontal
                ? new Vector2(-Npc.Direction.X, Npc.Direction.Y)
                : new Vector2(Npc.Direction.X, -Npc.Direction.Y);
        }
    }
}
