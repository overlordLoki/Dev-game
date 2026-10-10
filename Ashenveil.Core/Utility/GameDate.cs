
namespace Ashenveil.Core.Utility
{
    public class GameDate
    {
        public int Year { get; private set; } = 1;
        public int Day { get; private set; } = 1; // 1-30
        public int Season { get; private set; } = 0; // 0-3 (Spring, Summer, Fall, Winter)
        public int TotalDays { get; private set; } = 1; // Days since game start

        private const int DaysPerSeason = 30;
        private const int SeasonsPerYear = 4;

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