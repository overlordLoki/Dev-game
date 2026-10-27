namespace Ashenveil.Core.Entities.brains.Memorys
{
    /// <summary>
    /// One day in an NPC's diary: the first-person entry the day-end reflection wrote about it
    /// ("I waited at the castle door; the traveller arrived from the south."). Cold tier, next
    /// to transcripts: it stands in for that day's raw events once they leave the hot list.
    /// </summary>
    public record DayEntry(int Day, string Summary);
}
