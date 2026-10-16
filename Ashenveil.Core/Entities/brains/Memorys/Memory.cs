namespace Ashenveil.Core.Entities.brains.Memorys
{
    /// <summary>Where a memory came from. Grows as the game does (Quest, Rumour, ...).</summary>
    public enum MemoryKind
    {
        Conversation,
        Event,
    }

    /// <summary>
    /// One discrete thing an NPC remembers - a fact learned in conversation
    /// ("the traveller is Loki") or, later, an event ("killed 2 zombies by the well").
    /// The hot tier of memory: small, and always available to inject into a prompt.
    /// </summary>
    public class Memory
    {
        public int? SourceId { get; set; }       // if it came from a conversation, so we can trace back to the full transcript
        public string Text { get; }
        public MemoryKind Kind { get; }

        public Memory(string text, MemoryKind kind)
        {
            Text = text;
            Kind = kind;
        }
    }
}
