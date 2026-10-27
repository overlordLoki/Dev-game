using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// How an NPC feels about someone right now: its opinion, overridden by today's grudges.
    ///
    /// Opinions only change at day end, when reflection writes them. Without grudges, an NPC you
    /// attacked an hour ago would greet you as normal until midnight. So anyone who attacked
    /// this NPC since its last reflection (today, or yesterday if that isn't reflected yet) is
    /// treated as hostile until reflection has written the lasting change.
    /// </summary>
    public static class Relations
    {
        /// <summary>The ids of everyone with an attack on <paramref name="npc"/> it hasn't reflected on yet.</summary>
        public static HashSet<int> Grudges(EventLog log, NPC npc, int today) =>
            (log?.All ?? (IReadOnlyList<GameEvent>)new List<GameEvent>())
                .Where(e => e.Type == EventType.Attacked && e.TargetId == npc.Id && e.ActorId is int
                            && e.Day > npc.Journal.LastReflectedDay && e.Day >= today - 1)
                .Select(e => e.ActorId.Value)
                .ToHashSet();

        /// <summary>The NPC's attitude to someone: hostile if it holds a grudge, else what its opinion says.</summary>
        public static Attitude AttitudeTo(NPC npc, Entity who, EventLog log, int today) =>
            Grudges(log, npc, today).Contains(who.Id) ? Attitude.Hostile : Attitudes.Of(npc.OpinionOf(who));

        /// <summary>
        /// One line for the conversation prompt about how <paramref name="npc"/> feels about
        /// someone there: "Loki: wary (-35) - he attacked me at the well".
        /// </summary>
        public static string Describe(NPC npc, Entity who, string name, EventLog log, int today)
        {
            int score = npc.OpinionOf(who);
            var line = $"{name}: {AttitudeTo(npc, who, log, today).ToString().ToLowerInvariant()} ({score})";
            if (npc.Opinions.TryPersonal(who.Id, out var mine) && mine.Why != null) line += $" - {mine.Why}";
            if (Grudges(log, npc, today).Contains(who.Id)) line += " - attacked you recently";
            return line;
        }
    }
}
