using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// The day-end reflection: each NPC looks back on a day with one LLM call, and writes a
    /// diary entry (cold) and a few lasting facts (hot). Built like the ConversationSystem's
    /// decisions - fire the call, hold the Task, apply the answer on a later frame - but queued,
    /// so a day turning over in a crowded world doesn't fire every call at once.
    ///
    ///   1. Queue  - when a day turns over, every NPC with anything from the day before; after a
    ///               load, any NPC behind on its days (<see cref="CatchUp"/>).
    ///   2. Start  - at most <see cref="MaxInFlight"/> calls at a time, from the front of the queue.
    ///   3. Apply  - on the main thread, from <see cref="Update"/>. A failed call is dropped and
    ///               the day goes unreflected, as with decisions.
    ///
    /// It's given the NPCs it needs rather than the World, and its LLM call is handed in, so it
    /// can be tested with fakes.
    ///
    /// Reflection is also where opinions change (docs/factions-plan.md). The request names the
    /// people and factions the day was about, with keys; the LLM proposes changes by key; the
    /// game keeps only changes about those, capped per day: ±<see cref="SeenCap"/> for what the
    /// NPC saw, ±<see cref="HeardCap"/> for what it only heard about.
    /// </summary>
    public class ReflectionSystem
    {
        /// <summary>The most reflection calls waiting on the LLM at once.</summary>
        public const int MaxInFlight = 2;

        /// <summary>How many days back a load catches up on, at most.</summary>
        public const int CatchUpDays = 2;

        /// <summary>The most an opinion can move in one day from things the NPC saw, and from rumours.</summary>
        public const int SeenCap = 40, HeardCap = 15;

        /// <summary>
        /// A change to an NPC's opinion of a player moves that player's standing with the NPC's
        /// factions by this share of it: members' opinions add up to the faction's view.
        /// </summary>
        public const int StandingShare = 4;

        /// <summary>The world's event log, to find who an event or rumour was about. Set by the World.</summary>
        public EventLog Events { get; set; }

        /// <summary>Finds a person (player or NPC) by id. Set by the World.</summary>
        public Func<int, Entity> FindPerson { get; set; }

        private readonly Func<ReflectionRequestDto, Task<ReflectionDto>> _request;

        // Waiting to start, oldest first.
        private readonly List<(NPC npc, int day)> _queue = new();

        // Asked for but not back. Each keeps the event and rumour memories the request was written
        // from, so exactly those leave the hot list when it lands - not ones seen since; and the
        // keys it may change opinions about, with each one's cap.
        private readonly List<Flight> _inFlight = new();

        private record Flight(NPC npc, int day, List<Memory> events, Dictionary<string, int> caps, Task<ReflectionDto> task);

        /// <param name="request">How a reflection is asked for. Defaults to the real API.</param>
        public ReflectionSystem(Func<ReflectionRequestDto, Task<ReflectionDto>> request = null)
        {
            _request = request ?? DialogueApi.RequestReflectionAsync;
        }

        /// <summary>Reflections queued but not yet asked for.</summary>
        public int Queued => _queue.Count;

        /// <summary>Reflections asked for but not yet back.</summary>
        public int InFlight => _inFlight.Count;

        // ------------------------------------------------------------------ 1. queue

        /// <summary>
        /// Queues <paramref name="npc"/> to reflect on <paramref name="day"/>. False (and nothing
        /// queued) if it's already queued or in flight for that day.
        /// </summary>
        public bool Queue(NPC npc, int day)
        {
            if (_queue.Contains((npc, day)) || _inFlight.Any(f => f.npc == npc && f.day == day)) return false;
            _queue.Add((npc, day));
            return true;
        }

        /// <summary>
        /// A new day has begun (GameClock.DayStarted): queue every NPC that had anything happen
        /// to it the day before and hasn't reflected on it yet.
        /// </summary>
        public void DayStarted(int newDay, IEnumerable<NPC> npcs)
        {
            foreach (var npc in npcs) QueueIfDue(npc, newDay - 1);
        }

        /// <summary>
        /// After a load: a reflection in flight when the game was saved was lost, so queue any NPC
        /// behind on its days again, back to <see cref="CatchUpDays"/> days ago, oldest first.
        /// Today isn't reflected on - it isn't over.
        /// </summary>
        public void CatchUp(IEnumerable<NPC> npcs, int today)
        {
            foreach (var npc in npcs)
                for (int day = Math.Max(npc.Journal.LastReflectedDay + 1, today - CatchUpDays); day < today; day++)
                    QueueIfDue(npc, day);
        }

        private void QueueIfDue(NPC npc, int day)
        {
            if (day > npc.Journal.LastReflectedDay && npc.Journal.HasAnythingOn(day)) Queue(npc, day);
        }

        // ------------------------------------------------------------------ each frame

        /// <summary>Call once a frame: applies reflections that have come back, then starts queued ones.</summary>
        public void Update()
        {
            ApplyArrived();
            while (_inFlight.Count < MaxInFlight && _queue.Count > 0)
            {
                var (npc, day) = _queue[0];
                _queue.RemoveAt(0);
                Start(npc, day);
            }
        }

        // ------------------------------------------------------------------ 2. start

        private void Start(NPC npc, int day)
        {
            var seen = npc.Journal.EventsOn(day).ToList();
            var heard = npc.Journal.RumoursOn(day).ToList();
            var req = new ReflectionRequestDto
            {
                npc_name = npc.Name,
                persona = npc.Persona,
                date = GameDate.OnDay(day).Display,
                events = seen.Select(m => m.Text).ToList(),
                rumours = heard.Select(m => NPC.Hearsay(m, NameById)).ToList(),
                conversations = npc.Journal.Transcripts
                    .Where(t => t.Day == day && t.Summary != null)
                    .Select(t => t.Summary)
                    .ToList(),
            };

            // Who the day was about: everyone in the events it saw (cap SeenCap) or heard about
            // (HeardCap - unless it also saw something about them), but never itself.
            var caps = new Dictionary<string, int>();
            void Allow(string key, int cap) => caps[key] = Math.Max(cap, caps.GetValueOrDefault(key));
            foreach (var (memories, cap) in new[] { (seen, SeenCap), (heard, HeardCap) })
                foreach (var e in memories.Select(m => m.SourceId is int id ? Events?.Find(id) : null).Where(e => e != null))
                    foreach (var who in new[] { e.ActorId, e.TargetId }.OfType<int>().Where(id => id != npc.Id)
                                                                         .Select(id => FindPerson?.Invoke(id)).Where(w => w != null))
                    {
                        string key = PersonKey(who.Id);
                        if (!caps.ContainsKey(key))
                            req.people.Add(new KnownDto { key = key, name = Conversation.NameOf(who), opinion = npc.OpinionOf(who) });
                        Allow(key, cap);
                        foreach (var faction in FactionsOf(who))
                        {
                            if (!caps.ContainsKey(FactionKey(faction)))
                                req.factions.Add(new KnownDto { key = FactionKey(faction), name = faction, opinion = npc.Opinions.OfFaction(faction) });
                            Allow(FactionKey(faction), cap);
                        }
                    }

            _inFlight.Add(new Flight(npc, day, seen.Concat(heard).ToList(), caps, _request(req)));
        }

        public static string PersonKey(int id) => $"person:{id}";
        public static string FactionKey(string faction) => $"faction:{faction}";

        private static IEnumerable<string> FactionsOf(Entity who) => who switch
        {
            Player player => player.Factions,
            NPC npc => npc.Factions,
            _ => Enumerable.Empty<string>(),
        };

        private string NameById(int id) => FindPerson?.Invoke(id) is { } who ? Conversation.NameOf(who) : null;

        // ------------------------------------------------------------------ 3. apply

        // Walks backwards so removing an entry doesn't skip the next one.
        private void ApplyArrived()
        {
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                var flight = _inFlight[i];
                if (!flight.task.IsCompleted) continue;
                if (flight.task.IsCompletedSuccessfully && flight.task.Result != null)
                {
                    ApplyOpinionChanges(flight.npc, flight.task.Result.opinion_changes, flight.caps);
                    flight.npc.ApplyReflection(flight.task.Result, flight.day, flight.events);
                }
                _inFlight.RemoveAt(i);
            }
        }

        // The LLM proposes, the game disposes: a change survives only if it's about a key the
        // request offered, and all the changes about one key together stay within its cap.
        private void ApplyOpinionChanges(NPC npc, IEnumerable<OpinionChangeDto> changes, Dictionary<string, int> caps)
        {
            var total = new Dictionary<string, int>();
            foreach (var change in changes ?? Enumerable.Empty<OpinionChangeDto>())
            {
                if (change?.about == null || !caps.TryGetValue(change.about, out int cap) || change.by == 0) continue;

                int sofar = total.GetValueOrDefault(change.about);
                int by = Math.Clamp(sofar + change.by, -cap, cap) - sofar;
                if (by == 0) continue;
                total[change.about] = sofar + by;
                string why = string.IsNullOrWhiteSpace(change.why) ? null : change.why.Trim();

                if (change.about.StartsWith("faction:"))
                    npc.Opinions.ChangeFaction(change.about["faction:".Length..], by);
                else if (int.TryParse(change.about["person:".Length..], out int id) && FindPerson?.Invoke(id) is { } who)
                {
                    npc.ChangeOpinionOf(who, by, why);
                    // Hurt one guard, and the guard thinks a little less of you.
                    if (who is Player player)
                        foreach (var faction in npc.Factions) player.ChangeStanding(faction, by / StandingShare);
                }
            }
        }
    }
}
