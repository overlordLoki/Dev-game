using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Pick a random heading, walk it, and bounce off whatever gets in the way.
    /// </summary>
    public class WanderBrain : NpcBrain
    {
        public WanderBrain()
        {
            this.State = NPCState.Walking;
        }
        protected override void Start()
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            Npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        // The base runs this for Idle/Walking; Talking and Working are handled by the base.
        protected override void UpdateDefault(GameTime gameTime)
        {
            //chance to change direction every second
            if (Random.Shared.NextDouble() < gameTime.ElapsedGameTime.TotalSeconds)
            {
                // 10% chance to change direction on this second.
                if (Random.Shared.NextDouble() < 0.1)
                {
                    float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                    Npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                }
            }
        }

        // Flip the heading on the pushed axis, so a wanderer turns away instead of
        // pressing into whatever it hit.
        public override void OnBlocked(bool horizontal)
        {
            Npc.Direction = horizontal
                ? new Vector2(-Npc.Direction.X, Npc.Direction.Y)
                : new Vector2(Npc.Direction.X, -Npc.Direction.Y);
        }
    }
}
