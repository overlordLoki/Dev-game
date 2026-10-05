using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{

    /// <summary>
    /// Decides how an NPC moves. NPC owns the body (position, push-out collision,
    /// animation); the brain owns the heading - where to go and how to react when
    /// something gets in the way.
    /// </summary>
    public abstract class NpcBrain
    {
        public Entity target { get; set; } = null; //use for later AI, like following the player or attacking them
        //State machine for NPCs
        public enum NPCState
        {
            Idle,
            Walking,
            Talking,
        }
        public NPCState State { get; protected set; } = NPCState.Idle;
        /// <summary>Called once when the NPC is created, to set its starting heading.</summary>
        public virtual void Start(NPC npc) {}

        public virtual void StartTalking(NPC npc)
        {
            State = NPCState.Talking;
            //face the player
            npc.Direction = Vector2.Zero;
        }
        public virtual void StopTalking(NPC npc)
        {
            State = NPCState.Walking;
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            
        }

        public virtual void Update(NPC npc, GameTime gameTime)
        {
            switch (State)
            {
                case NPCState.Idle:
                    // Do nothing.
                    break;
                case NPCState.Walking:
                    // Do nothing.
                    break;
                case NPCState.Talking:
                    // Do nothing.
                    break;
            }
        }

        /// <summary>
        /// Called after the NPC was pushed out of another entity. <paramref name="horizontal"/>
        /// is true when the push was along X. Default: do nothing, just stay pushed out.
        /// </summary>
        public virtual void OnBlocked(NPC npc, bool horizontal) { }
    }
}
