using System.Collections.Generic;
using Ashenveil.Core.Entities;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// Who hears about an event when it's posted. The actor and the target always know; any
    /// other NPC in the same level whose feet are within <see cref="Range"/> cells of where it
    /// happened knows too; nobody else does. Word spreading further is gossip's job (factions plan).
    ///
    /// Range only, through walls: line of sight can come with combat's aggro check.
    /// </summary>
    public static class Perception
    {
        /// <summary>How far away an NPC still notices something, in cells.</summary>
        public const float Range = 6f;

        /// <summary>The NPCs who know about <paramref name="e"/>, each once.</summary>
        public static List<NPC> WhoKnows(GameEvent e, NpcRoster roster)
        {
            var who = new List<NPC>();
            void Add(NPC npc)
            {
                if (npc != null && !who.Contains(npc)) who.Add(npc);
            }

            // The player isn't an NPC (Find gives null), and has no journal to remember it in.
            if (e.ActorId is int actor) Add(roster.Find(actor));
            if (e.TargetId is int target) Add(roster.Find(target));
            if (e.Level != null)
                foreach (var npc in roster.In(e.Level))
                    if (Vector2.Distance(npc.Feet, e.Where) <= Range) Add(npc);
            return who;
        }
    }
}
