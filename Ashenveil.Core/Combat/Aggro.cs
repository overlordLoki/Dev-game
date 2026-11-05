using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.GameWorld;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Combat
{
    /// <summary>
    /// Who starts fights, and who joins them (docs/combat-plan.md, step 5). Combat itself has no
    /// teams; this is where opinions turn into blows:
    /// <list type="bullet">
    /// <item><b>Picking fights:</b> a few times a second, every NPC that's free (not fighting,
    /// talking or down) looks round for someone within its <see cref="NPC.AggroRange"/> it
    /// would attack (<see cref="NPC.WouldAttack"/>: by default, hostile ones), and goes for the
    /// nearest.</item>
    /// <item><b>Joining in:</b> when a blow lands, every free NPC near the victim takes the side it
    /// likes more: against the attacker if it's friendly to the victim or hostile to the
    /// attacker, against the victim the other way round.</item>
    /// </list>
    /// Attitudes come from <see cref="Relations"/>, with today's grudges cached per NPC until the
    /// event log or its reflection moves on, since this asks far more often than chat does.
    /// Range only, through walls, like perception; line of sight can come later.
    /// </summary>
    public class Aggro
    {
        /// <summary>How often NPCs look round for someone to fight, in seconds.</summary>
        public const float LookSeconds = 0.25f;
        /// <summary>How close to a blow, feet to feet, an NPC has to be to join in, in cells.</summary>
        public const float JoinRange = Perception.Range;

        private readonly EventLog _log;
        private readonly System.Func<int> _today;
        private float _untilLook = LookSeconds;

        // Each NPC's grudges, and what they were worked out from: redone when any of it changes.
        private readonly Dictionary<int, (int day, int events, int reflected, HashSet<int> ids)> _grudges = new();

        /// <param name="log">Where grudges come from (attacks on the NPC). Null: nobody has any.</param>
        /// <param name="today">Today (GameClock.TotalDays).</param>
        public Aggro(EventLog log, System.Func<int> today)
        {
            _log = log;
            _today = today;
        }

        /// <summary>How <paramref name="npc"/> feels about <paramref name="who"/> right now, grudges included.</summary>
        public Attitude AttitudeTo(NPC npc, Entity who) =>
            GrudgesOf(npc).Contains(who.Id) ? Attitude.Hostile : Attitudes.Of(npc.OpinionOf(who));

        // How much it likes them, grudges counting as the worst there is: for choosing a side.
        private int Liking(NPC npc, Entity who) =>
            GrudgesOf(npc).Contains(who.Id) ? Attitudes.Min : npc.OpinionOf(who);

        private HashSet<int> GrudgesOf(NPC npc)
        {
            int today = _today(), events = _log?.All.Count ?? 0, reflected = npc.Journal.LastReflectedDay;
            if (_grudges.TryGetValue(npc.Id, out var cached)
                && cached.day == today && cached.events == events && cached.reflected == reflected)
                return cached.ids;

            var ids = Relations.Grudges(_log, npc, today);
            _grudges[npc.Id] = (today, events, reflected, ids);
            return ids;
        }

        /// <summary>Whether this NPC is free to start or join a fight: up, and not fighting or talking.</summary>
        public static bool IsFree(NPC npc) =>
            !npc.IsDead && npc.State is not (NpcBrain.NPCState.Fighting or NpcBrain.NPCState.Talking or NpcBrain.NPCState.Dead);

        /// <summary>
        /// Every <see cref="LookSeconds"/>, each free NPC in <paramref name="npcs"/> goes for the
        /// nearest of <paramref name="people"/> it would attack. Both are the level being played.
        /// </summary>
        public void Update(float dt, IReadOnlyList<NPC> npcs, IReadOnlyList<Entity> people)
        {
            if ((_untilLook -= dt) > 0) return;
            _untilLook = LookSeconds;

            foreach (var npc in npcs)
            {
                if (!IsFree(npc)) continue;
                var target = Nearest(npc, people.Where(p => p != npc && !p.IsDead && npc.WouldAttack(AttitudeTo(npc, p))),
                                     npc.AggroRange);
                if (target != null) npc.StartFighting(target);
            }
        }

        /// <summary>
        /// A blow landed: every free NPC near <paramref name="victim"/> that cares takes a side.
        /// The victim fights back on its own (NPC.Provoke); this is everyone else.
        /// </summary>
        public void JoinIn(Entity attacker, Entity victim, IReadOnlyList<NPC> npcs)
        {
            foreach (var npc in npcs)
            {
                if (npc == attacker || npc == victim || !IsFree(npc) || !npc.JoinsFights) continue;
                if (Vector2.Distance(npc.Feet, victim.Feet) > JoinRange) continue;

                Attitude toAttacker = AttitudeTo(npc, attacker), toVictim = AttitudeTo(npc, victim);
                bool likesVictimMore = Liking(npc, victim) >= Liking(npc, attacker);
                if (likesVictimMore && (toVictim == Attitude.Friendly || toAttacker == Attitude.Hostile))
                    npc.StartFighting(attacker);
                else if (!likesVictimMore && (toAttacker == Attitude.Friendly || toVictim == Attitude.Hostile))
                    npc.StartFighting(victim);   // does nothing if the blow put them down
            }
        }

        // The closest of `candidates` to `npc`, feet to feet, within `range`; null if none is.
        private static Entity Nearest(NPC npc, IEnumerable<Entity> candidates, float range)
        {
            Entity best = null;
            float bestDistance = range;
            foreach (var who in candidates)
            {
                float d = Vector2.Distance(npc.Feet, who.Feet);
                if (d <= bestDistance) { best = who; bestDistance = d; }
            }
            return best;
        }
    }
}
