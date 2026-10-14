using System.Collections.Generic;
using Ashenveil.Core.Dialogue;

namespace Ashenveil.Core.Entities.brains.Memorys
{
    public class JournalEntry
    {
        public string Summary { get; set; }
        public List<Conversation> Conversations { get; set; } = new List<Conversation>();
        public JournalEntry(string summary, List<Conversation> conversations)
        {
            Summary = summary;
            Conversations = conversations;
        }
    }
    public class Journal
    {
        public List<JournalEntry> Entries { get; set; } = new List<JournalEntry>();
        public void AddEntry(string summary, List<Conversation> conversations)
        {
            Entries.Add(new JournalEntry(summary, conversations));
        }
    }
}