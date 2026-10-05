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
        public override void Start(NPC npc)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        public override void Update(NPC npc, GameTime gameTime)
        {
            switch (State)
            {
                case NPCState.Idle:
                    // Do nothing.
                    break;
                case NPCState.Walking:
                    //chance to change direction every second
                    if (Random.Shared.NextDouble() < gameTime.ElapsedGameTime.TotalSeconds)
                    {
                        // 10% chance to change direction on this second. 
                        if (Random.Shared.NextDouble() < 0.1)
                        {
                            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                            npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                        }
                    }
                    break;
                case NPCState.Talking:
                    // TODO: maybe have them face the player and just stop moving
                    break;
            }
        }

        // Flip the heading on the pushed axis, so a wanderer turns away instead of
        // pressing into whatever it hit.
        public override void OnBlocked(NPC npc, bool horizontal)
        {
            npc.Direction = horizontal
                ? new Vector2(-npc.Direction.X, npc.Direction.Y)
                : new Vector2(npc.Direction.X, -npc.Direction.Y);
        }
    }
}
