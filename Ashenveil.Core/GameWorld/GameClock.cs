using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld
{
    public class GameClock
    {
        private const float SecondsPerDay = 600f;
        
        // Time tracking: 10 real minutes = 1 game day
        private float _elapsedSeconds;
        public GameDate CurrentDate { get; private set; } = new GameDate();
        // How far into today, in real seconds. Read by saving, so a load lands at the same time of day.
        public float SecondsIntoDay => _elapsedSeconds;

        public int TotalDays => CurrentDate.TotalDays;
        public int Year => CurrentDate.Year;
        public int Day => CurrentDate.Day;
        public int Season => CurrentDate.Season;
        public string SeasonName => CurrentDate.SeasonName;
        public string DateDisplay => CurrentDate.Display;

        /// <summary>Raised when a new day begins, with the new TotalDays.</summary>
        public event Action<int> DayStarted;
        
        /// <summary>
        /// Sets the clock to a saved time: that day, that far into it. Doesn't raise DayStarted -
        /// no day has started, the game has just carried on from where it was.
        /// </summary>
        public void Restore(int totalDays, float secondsIntoDay)
        {
            CurrentDate = GameDate.OnDay(totalDays);
            // Kept inside one day, so a hand-edited value can't skip days on the first frame.
            _elapsedSeconds = Math.Clamp(secondsIntoDay, 0f, SecondsPerDay - 0.001f);
        }

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