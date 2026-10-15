using System.Collections.Generic;
using System.Linq;


namespace Ashenveil.Core.Entities.brains.Memorys
{
    public class JournalEntry
    {
        public string Summary { get; }
        public IReadOnlyList<string> Facts { get; }
        public JournalEntry(string summary, IReadOnlyList<string> facts)
        {
            Summary = summary;
            Facts = facts;
        }
    }

    public class Journal
    {
        public List<JournalEntry> Entries { get; } = new();
        public void AddEntry(string summary, IEnumerable<string> facts) =>
            Entries.Add(new JournalEntry(summary, facts?.ToList() ?? new List<string>()));
    }
}