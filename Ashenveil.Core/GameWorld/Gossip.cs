using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Memorys;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// How word gets around without everyone being there. Two NPCs near each other who like
    /// each other pass on what they've recently seen or heard, as rumours: an engine step, no
    /// LLM call. Later, NPCs talking to each other (A1) can make it real conversations; the
    /// data stays the same.
    ///
    ///   - A rumour is a weaker event: MemoryKind.Rumour, with who told them and a hop count.
    ///     A witness passes it on at hop 1, a hearer at hop 2, and it stops there.
    ///   - Trust filters it: an NPC only tells people it likes, and only believes people it likes.
    ///   - An NPC keeps an event once, however many people tell them (or if they saw it).
    /// </summary>
    public static class Gossip
    {
        /// <summary>How close two NPCs have to be to gossip, feet to feet, in cells.</summary>
        public const float Range = 2f;

        /// <summary>The furthest a rumour travels: a rumour at this hop isn't passed on.</summary>
        public const int MaxHops = 2;

        /// <summary>How often, in seconds of play, NPCs near each other share news.</summary>
        public const float IntervalSeconds = 5f;

        /// <summary>One round: every pair of NPCs in the same level and in range shares news both ways.</summary>
        public static int Step(IEnumerable<IReadOnlyList<NPC>> levels, int today)
        {
            int shared = 0;
            foreach (var level in levels)
                foreach (var teller in level)
                    foreach (var hearer in level)
                        if (teller != hearer && Vector2.Distance(teller.Feet, hearer.Feet) <= Range)
                            shared += Share(teller, hearer, today);
            return shared;
        }

        /// <summary>
        /// <paramref name="teller"/> passes on its recent news to <paramref name="hearer"/>, if
        /// each likes the other. Returns how many rumours the hearer took in.
        /// </summary>
        public static int Share(NPC teller, NPC hearer, int today)
        {
            if (Attitudes.Of(teller.OpinionOf(hearer)) != Attitude.Friendly) return 0;   // doesn't tell them
            if (Attitudes.Of(hearer.OpinionOf(teller)) != Attitude.Friendly) return 0;   // wouldn't believe them

            var news = teller.Journal.Memories
                .Where(m => m.SourceId != null && m.Day >= today - 1
                            && (m.Kind == MemoryKind.Event || (m.Kind == MemoryKind.Rumour && m.Hops < MaxHops)))
                .ToList();

            int taken = 0;
            foreach (var m in news)
            {
                bool known = hearer.Journal.Memories.Any(h =>
                    h.SourceId == m.SourceId && h.Kind is MemoryKind.Event or MemoryKind.Rumour);
                if (known) continue;

                hearer.Journal.Add(new Memory(m.Text, MemoryKind.Rumour)
                {
                    SourceId = m.SourceId,
                    Day = today,
                    TellerId = teller.Id,
                    Hops = m.Kind == MemoryKind.Event ? 1 : m.Hops + 1,
                });
                taken++;
            }
            return taken;
        }
    }
}
