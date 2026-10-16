using System.Collections.Generic;
using System.Linq;

namespace Ashenveil.Core.Entities.brains.Memorys
{
    /// <summary>
    /// An NPC's remembered facts and events, oldest first - the hot tier of memory.
    /// (The cold tier, full conversation transcripts, comes later; see the plan.)
    /// </summary>
    public class Journal
    {
        private readonly List<Memory> _memories = new();
        public readonly List<Transcript> Transcripts = new();  // cold tier of memory, for the plan
        public IReadOnlyList<Memory> Memories => _memories;

        public void Add(Memory memory) => _memories.Add(memory);

        public IEnumerable<Memory> Recent(int count) => _memories.TakeLast(count);
    }
    /// <summary>
    /// A transcript of a conversation, for the cold tier of memory. The hot tier is the extracted facts in <see cref="Journal"/>.
    /// </summary>
    public class Transcript
    {
        public int Id { get; }                       // so a Memory can point back to it
        public string Location { get; }              // level name, not the Location object
        public IReadOnlyList<string> Participants { get; }   // names, not entities
        public IReadOnlyList<string> Lines { get; }  // "Speaker: text"
    }
}
