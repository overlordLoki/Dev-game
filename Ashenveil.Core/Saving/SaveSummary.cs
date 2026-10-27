using System;

namespace Ashenveil.Core.Saving
{
    /// <summary>
    /// What a row on the save slots screen shows about one save file, read by
    /// SaveStore.ReadSummary without loading the save. A file that can't be read still gets a
    /// summary, with <see cref="Problem"/> saying why, so the player can see it and delete it.
    /// Plain data, like the save classes.
    /// </summary>
    public class SaveSummary
    {
        public string FilePath { get; init; }        // which file: what loading, overwriting or deleting needs
        public string PlayerName { get; init; }      // the save's owner
        public string Level { get; init; }           // where the owner is
        public int TotalDays { get; init; }          // the in-game day (GameDate.OnDay turns it into a date)
        public DateTime SavedAt { get; init; }       // UTC

        // Why the file can't be read, or null if it can. Only FilePath is set when it can't.
        public string Problem { get; init; }
        public bool IsReadable => Problem == null;

        public static SaveSummary Unreadable(string filePath, string problem) =>
            new() { FilePath = filePath, Problem = problem };
    }
}
