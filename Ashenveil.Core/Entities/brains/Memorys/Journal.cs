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

        public void Add(Memory memory) => _memories.Add(memory);
        public void Add(Transcript transcript) => _transcripts.Add(transcript);
        public void Add(DayEntry day) => _days.Add(day);

        /// <summary>Takes memories out of the hot tier (a reflected day's events).</summary>
        public void Forget(IEnumerable<Memory> memories)
        {
            var gone = memories.ToHashSet();
            _memories.RemoveAll(gone.Contains);
        }

        /// <summary>The last <paramref name="count"/> facts - every memory but events, which are sent apart.</summary>
        public IEnumerable<Memory> Recent(int count) => _memories.Where(m => m.Kind != MemoryKind.Event).TakeLast(count);

        /// <summary>The events seen on <paramref name="day"/> that are still in the hot tier, oldest first.</summary>
        public IEnumerable<Memory> EventsOn(int day) => _memories.Where(m => m.Kind == MemoryKind.Event && m.Day == day);

        /// <summary>Whether anything happened to this NPC on <paramref name="day"/>: an event seen, or a conversation.</summary>
        public bool HasAnythingOn(int day) => EventsOn(day).Any() || _transcripts.Any(t => t.Day == day);
    }
}
