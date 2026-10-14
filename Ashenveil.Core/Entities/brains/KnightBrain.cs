using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains
{
    public class KnightBrain : NpcBrain
    {
        
        public KnightBrain()
        {
            this.State = NPCState.Idle;
        }
        protected override void UpdateDefault(NPC npc, GameTime gameTime)
        {
            
        }
    }
}