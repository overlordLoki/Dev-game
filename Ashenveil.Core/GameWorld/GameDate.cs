namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// Represents the in-game date, including year, season, day, and total days since the start of the game. Provides functionality to advance the date by one day.
    /// </summary>
    public class GameDate
    {
        public int Year { get; private set; } = 1;
        public int Day { get; private set; } = 1; // 1-30
        public int Season { get; private set; } = 0; // 0-3 (Spring, Summer, Fall, Winter)
        public int TotalDays { get; private set; } = 1; // Days since game start

        private const int DaysPerSeason = 30;
        private const int SeasonsPerYear = 4;
        private static readonly string[] SeasonNames = { "Spring", "Summer", "Fall", "Winter" };

        public string SeasonName => SeasonNames[Season];
        // "Summer 11, Year 1". Here rather than on GameClock so a save's date can be shown
        // without a clock (the save slots screen builds one with OnDay).
        public string Display => $"{SeasonName} {Day}, Year {Year}";

        /// <summary>
        /// The date <paramref name="totalDays"/> days into the game (1 is the first day), for
        /// loading a save. Walks there a day at a time, so the calendar rules live only in
        /// AdvanceDay.
        /// </summary>
        public static GameDate OnDay(int totalDays)
        {
            var date = new GameDate();
            while (date.TotalDays < totalDays) date.AdvanceDay();
            return date;
        }

        public void AdvanceDay()
        {
            TotalDays++;
            Day++;

            if (Day > DaysPerSeason)
            {
                Day = 1;
                Season++;

                if (Season >= SeasonsPerYear)
                {
                    Season = 0;
                    Year++;
                }
            }
        }
    }
}