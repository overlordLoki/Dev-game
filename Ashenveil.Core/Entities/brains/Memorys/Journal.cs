using System;
using System.Collections.Generic;
using System.Linq;

namespace Ashenveil.Core.Entities.brains.Memorys
{
    /// <summary>
    /// Everything an NPC remembers, in two tiers:
    ///   hot  - Memories: short facts and today's events, injected into every conversation.
    ///   cold - Transcripts: full past conversations; and the diary, one entry per reflected
    ///          day. Kept, but only recalled as one-line summaries.
    /// </summary>
    public class Journal
    {
        // Hot tier: small, always sent to the LLM.
        private readonly List<Memory> _memories = new();
        public IReadOnlyList<Memory> Memories => _memories;

        // Cold tier: large, recalled on demand (summaries now, full transcripts later).
        private readonly List<Transcript> _transcripts = new();
        public IReadOnlyList<Transcript> Transcripts => _transcripts;

        // Cold tier: the diary, oldest first. Usually one entry per day; a day reflected early
        // (the console's `reflect`) can have more.
        private readonly List<DayEntry> _days = new();
        public IReadOnlyList<DayEntry> Days => _days;

        /// <summary>
        /// The last day this NPC reflected on (0: none yet). Saved, so a reflection lost to
        /// saving and loading mid-call is asked for again after the load.
        /// </summary>
        public int LastReflectedDay { get; set; }

        /// <summary>
        /// How long an event stays known after reflection takes its memory out of the hot tier:
        /// the day it was learned and this many after. Gossip only passes news on until two days
        /// after the event (a witness tells the next day at the latest, and whoever they told
        /// passes it on the day after), so nothing older can come round again.
        /// </summary>
        public const int KnownEventDays = 2;

        // Every event this NPC saw or heard about lately, by id, with the day it learned of it.
        // Outlives the memory, so gossip can't tell it the same news again once it's reflected
        // on - that would count the event twice. Saved; trimmed as new events come in.
        private readonly Dictionary<int, int> _knownEvents = new();
        public IReadOnlyDictionary<int, int> KnownEvents => _knownEvents;

        public void Add(Memory memory)
        {
            _memories.Add(memory);
            if (memory.Kind is MemoryKind.Event or MemoryKind.Rumour && memory.SourceId is int eventId)
                Know(eventId, memory.Day);
        }

        /// <summary>Whether this NPC has seen or heard about an event lately, even if it's reflected on it since.</summary>
        public bool Knows(int eventId) => _knownEvents.ContainsKey(eventId);

        /// <summary>Records an event as known from <paramref name="day"/>, and drops ones too old to come round again.</summary>
        public void Know(int eventId, int day)
        {
            _knownEvents[eventId] = Math.Max(day, _knownEvents.GetValueOrDefault(eventId));
            foreach (var old in _knownEvents.Where(k => k.Value < day - KnownEventDays).Select(k => k.Key).ToList())
                _knownEvents.Remove(old);
        }
        public void Add(Transcript transcript) => _transcripts.Add(transcript);
        public void Add(DayEntry day) => _days.Add(day);

        /// <summary>Takes memories out of the hot tier (a reflected day's events).</summary>
        public void Forget(IEnumerable<Memory> memories)
        {
            var gone = memories.ToHashSet();
            _memories.RemoveAll(gone.Contains);
        }

        /// <summary>The last <paramref name="count"/> facts - every memory but events and rumours, which are sent apart.</summary>
        public IEnumerable<Memory> Recent(int count) =>
            _memories.Where(m => m.Kind is not (MemoryKind.Event or MemoryKind.Rumour)).TakeLast(count);

        /// <summary>The events seen on <paramref name="day"/> that are still in the hot tier, oldest first.</summary>
        public IEnumerable<Memory> EventsOn(int day) => _memories.Where(m => m.Kind == MemoryKind.Event && m.Day == day);

        /// <summary>The rumours heard on <paramref name="day"/> that are still in the hot tier, oldest first.</summary>
        public IEnumerable<Memory> RumoursOn(int day) => _memories.Where(m => m.Kind == MemoryKind.Rumour && m.Day == day);

        /// <summary>Whether anything happened to this NPC on <paramref name="day"/>: an event seen, a rumour heard, or a conversation.</summary>
        public bool HasAnythingOn(int day) => EventsOn(day).Any() || RumoursOn(day).Any() || _transcripts.Any(t => t.Day == day);
    }
}
