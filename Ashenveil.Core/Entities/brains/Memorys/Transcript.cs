using System.Collections.Generic;

namespace Ashenveil.Core.Entities.brains.Memorys
{
    /// <summary>
    /// Someone who took part in a conversation. Id is who it was (their entity Id, which
    /// never changes); Name is what they were called at the time, for the LLM and the
    /// console. Kept together so the two can't drift apart.
    /// </summary>
    public record Participant(int Id, string Name);

    /// <summary>
    /// A finished conversation, kept for the cold tier of memory. Plain data only - ids,
    /// names and lines, never the live Conversation (which holds entities, a whole Location
    /// and a pending Task) - so it can outlive the level it happened in and be saved later.
    /// One transcript is shared by every NPC that took part.
    /// </summary>
    public class Transcript
    {
        public int Id { get; }                                    // facts point back here via Memory.SourceId
        public int Day { get; }                                   // GameDate.TotalDays when it happened
        public string Location { get; }                           // level name, not the Location object
        public IReadOnlyList<Participant> Participants { get; }   // ids and names, not entities
        public IReadOnlyList<string> Lines { get; }               // "Speaker: text", oldest first

        // The one-line recap from the end-of-conversation decision. Null until that lands,
        // and stays null if the API was down - recall skips transcripts without one.
        public string Summary { get; internal set; }

        public Transcript(int id, int day, string location,
                          IReadOnlyList<Participant> participants, IReadOnlyList<string> lines)
        {
            Id = id;
            Day = day;
            Location = location;
            Participants = participants;
            Lines = lines;
        }
    }
}
