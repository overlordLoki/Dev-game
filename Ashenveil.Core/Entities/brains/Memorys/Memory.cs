namespace Ashenveil.Core.Entities.brains.Memorys
{
    /// <summary>Where a memory came from. Grows as the game does (Quest, Rumour, ...).</summary>
    public enum MemoryKind
    {
        Conversation,   // a fact learned in conversation; SourceId is the transcript
        Event,          // something seen happen; SourceId is the event (EventLog)
        Reflection,     // a lasting fact from the day-end reflection; no SourceId
    }

    /// <summary>
    /// One discrete thing an NPC remembers - a fact learned in conversation
    /// ("the traveller is Loki"), or an event it saw ("Sir Lancelot couldn't reach the well").
    /// The hot tier of memory: small, and always available to inject into a prompt.
    /// </summary>
    public class Memory
    {
        // Where to trace it back to: a transcript id for a Conversation memory, an event id
        // for an Event memory - Kind says which. Null if it has no source.
        public int? SourceId { get; set; }
        public string Text { get; }
        public MemoryKind Kind { get; }
        // GameDate.TotalDays when it was formed: the conversation's day, or the event's.
        // 0 for a memory from before days were kept (an old save's facts with no transcript).
        public int Day { get; set; }

        public Memory(string text, MemoryKind kind)
        {
            Text = text;
            Kind = kind;
        }
    }
}
