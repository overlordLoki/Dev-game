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
        //State machine for NPCs
        public enum NPCState
        {
            Idle,
            Walking,
        }
        public NPCState State { get; protected set; } = NPCState.Idle;
        /// <summary>Called once when the NPC is created, to set its starting heading.</summary>
        public virtual void Start(NPC npc) {}

        public abstract void Update(NPC npc, GameTime gameTime);

        /// <summary>
        /// Called after the NPC was pushed out of another entity. <paramref name="horizontal"/>
        /// is true when the push was along X. Default: do nothing, just stay pushed out.
        /// </summary>
        public virtual void OnBlocked(NPC npc, bool horizontal) { }
    }
}
