using System.Collections.Generic;
using System.Linq;

namespace Ashenveil.Core.Entities.brains.Memorys
{
    /// <summary>
    /// Everything an NPC remembers, in two tiers:
    ///   hot  - Memories: short facts and events, injected into every conversation.
    ///   cold - Transcripts: full past conversations, kept but only recalled when needed.
    /// </summary>
    public class Journal
    {
        // Hot tier: small, always sent to the LLM.
        private readonly List<Memory> _memories = new();
        public IReadOnlyList<Memory> Memories => _memories;

        // Cold tier: large, recalled on demand (summaries now, full transcripts later).
        private readonly List<Transcript> _transcripts = new();
        public IReadOnlyList<Transcript> Transcripts => _transcripts;

        public void Add(Memory memory) => _memories.Add(memory);
        public void Add(Transcript transcript) => _transcripts.Add(transcript);

        public IEnumerable<Memory> Recent(int count) => _memories.TakeLast(count);
    }
}
