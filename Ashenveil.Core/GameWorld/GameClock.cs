using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld
{
    public class GameClock
    {
        private static readonly string[] Seasons = { "Spring", "Summer", "Fall", "Winter" };
        private const float SecondsPerDay = 600f;
        
        // Time tracking: 10 real minutes = 1 game day
        private float _elapsedSeconds;
        public GameDate CurrentDate { get; private set; } = new GameDate();

        public int TotalDays => CurrentDate.TotalDays;
        public int Year => CurrentDate.Year;
        public int Day => CurrentDate.Day;
        public int Season => CurrentDate.Season;
        public string SeasonName => Seasons[Season];
        public string DateDisplay => $"{SeasonName} {Day}, Year {Year}";

        /// <summary>Raised when a new day begins, with the new TotalDays.</summary>
        public event Action<int> DayStarted;
        
        // Called every frame: turns real seconds into game days (10 real minutes = 1 day).
        public void Update(GameTime gameTime)
        {
            _elapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
            while (_elapsedSeconds >= SecondsPerDay)
            {
                _elapsedSeconds -= SecondsPerDay;
                CurrentDate.AdvanceDay();
                DayStarted?.Invoke(TotalDays);
            }
        }

    }
}