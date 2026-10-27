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
        public void Update(GameTime gameTime) => Advance((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>The time of day on a 24-hour clock, from 0 (midnight) up to 24.</summary>
        public float HourOfDay => _elapsedSeconds / SecondsPerDay * 24f;

        /// <summary>
        /// Jumps to the very start of the day <paramref name="days"/> from now (the console's
        /// `time +1d`), raising DayStarted for every day passed, as if they'd been played.
        /// </summary>
        public void SkipDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                _elapsedSeconds = 0f;
                StartNextDay();
            }
        }

        /// <summary>Moves the clock on by game hours, raising DayStarted if midnight is passed.</summary>
        public void SkipHours(float hours)
        {
            if (hours <= 0) return;
            Advance(hours / 24f * SecondsPerDay);
        }

        // Moves time on by real seconds; every midnight crossed starts a day.
        private void Advance(float seconds)
        {
            _elapsedSeconds += seconds;
            while (_elapsedSeconds >= SecondsPerDay)
            {
                _elapsedSeconds -= SecondsPerDay;
                StartNextDay();
            }
        }

        private void StartNextDay()
        {
            CurrentDate.AdvanceDay();
            DayStarted?.Invoke(TotalDays);
        }

    }
}