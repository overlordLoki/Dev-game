using Ashenveil.Core.Dialogue;

namespace Ashenveil.Core.Entities.brains.Memorys
{
    public class Memory
    {
        public string Summary { get; set; }
        public Conversation Conversation { get; set; }
        public Memory(string summary, Conversation conversation)
        {
            Summary = summary;
            Conversation = conversation;
        }
    }
}