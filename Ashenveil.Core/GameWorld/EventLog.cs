using System;
using System.Collections.Generic;
using System.Linq;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// Everything that's happened, oldest first. The cold store for events: the day-end
    /// reflection reads from it, and later gossip and recall search it. It keeps every event,
    /// and stays small because only meaningful things are events.
    ///
    /// <see cref="Post"/> is the only way in. It stamps the event with an id and today's day,
    /// stores it, hands it to perception (who saw it - the World's business), then raises
    /// <see cref="Posted"/>.
    /// </summary>
    public class EventLog
    {
        private readonly List<GameEvent> _events = new();
        private readonly Func<int> _today;
        private readonly Action<GameEvent> _perceive;

        // Hands out event ids. Only counts up, so a Memory.SourceId always points at the right event.
        private int _nextId = 1;

        /// <param name="today">The day an event is posted on (GameClock.TotalDays).</param>
        /// <param name="perceive">Gives the event to whoever saw it. Null: nobody does (tests of the log itself).</param>
        public EventLog(Func<int> today, Action<GameEvent> perceive = null)
        {
            _today = today;
            _perceive = perceive;
        }

        /// <summary>Raised once per event, after it's stored and perceived. The console shows these.</summary>
        public event Action<GameEvent> Posted;

        /// <summary>Every event, oldest first.</summary>
        public IReadOnlyList<GameEvent> All => _events;

        /// <summary>The id the next event will get. For saving.</summary>
        public int PeekNextId => _nextId;

        /// <summary>The event with this id, or null.</summary>
        public GameEvent Find(int id) => _events.FirstOrDefault(e => e.Id == id);

        /// <summary>The last <paramref name="count"/> events, oldest first.</summary>
        public IEnumerable<GameEvent> Recent(int count) => _events.TakeLast(count);

        /// <summary>
        /// Records that something happened. Whatever id and day <paramref name="e"/> has are
        /// replaced: the log hands out ids, and it happened today. Returns the stored event.
        /// </summary>
        public GameEvent Post(GameEvent e)
        {
            var posted = e with { Id = _nextId++, Day = _today() };
            _events.Add(posted);
            _perceive?.Invoke(posted);
            Posted?.Invoke(posted);
            return posted;
        }

        /// <summary>
        /// Carries on a saved game's log: its events (oldest first) and the next id, so a new
        /// event never gets the id of a saved one. Nothing is perceived or raised again.
        /// </summary>
        public void Restore(int nextId, IEnumerable<GameEvent> events)
        {
            _events.Clear();
            _events.AddRange(events);
            _nextId = nextId;
        }
    }
}
