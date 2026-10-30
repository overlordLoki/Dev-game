using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    /// <summary>
    /// Walks a loop of stops, round and round: a <see cref="GoToJob"/> to each in turn, with
    /// an optional pause at each. Never completes on its own: it runs until something replaces
    /// it (see <see cref="NpcBrain.Enqueue"/>). A stop it can't reach is skipped; it fails only
    /// when it can't reach any of them, one after another.
    /// </summary>
    public class PatrolJob : IEndlessJob
    {
        public JobState State { get; set; } = JobState.Inactive;
        private readonly List<Vector2> _stops;       // in cells, in walking order
        private readonly float _pause;               // seconds to stand at each stop
        private int _next;                           // index of the stop being walked to
        private GoToJob _leg;                        // the walk to it; null between legs
        private float _paused;                       // seconds stood at the last stop so far
        private int _failedInARow;                   // stops skipped since one was last reached

        public PatrolJob(IEnumerable<Vector2> stops, float pause = 0f)
        {
            _stops = stops.ToList();
            _pause = pause;
            _paused = pause;                         // no pause before the first leg
        }

        /// <summary>
        /// The stops, in cells, starting with the one being walked to - so saving keeps where
        /// the patrol was up to without an index, and a load carries on to the same stop.
        /// </summary>
        public IReadOnlyList<Vector2> StopsFromNext => _stops.Skip(_next).Concat(_stops.Take(_next)).ToList();

        /// <summary>Seconds to stand at each stop. Saved.</summary>
        public float Pause => _pause;

        /// <summary>
        /// The waypoints still to walk to the next stop, in cells. Empty while pausing, or
        /// before the first Execute. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints => _leg?.RemainingWaypoints ?? new List<Vector2>();

        public void Execute(JobContext context)
        {
            var npc = (NPC)context.Owner;
            if (_stops.Count == 0)
            {
                Fail(npc);
                return;
            }

            // Standing at the stop just reached, before setting off for the next.
            if (_paused < _pause)
            {
                npc.Direction = Vector2.Zero;
                _paused += context.DeltaTime;
                return;
            }

            _leg ??= new GoToJob(_stops[_next]);
            _leg.Execute(context);

            if (_leg.State == JobState.Completed)
            {
                _failedInARow = 0;
                _paused = 0f;
                NextStop();
            }
            else if (_leg.State == JobState.Failed)
            {
                // Can't get there: skip it, unless that's every stop in turn.
                if (++_failedInARow >= _stops.Count) Fail(npc);
                else NextStop();
            }
        }

        private void NextStop()
        {
            _next = (_next + 1) % _stops.Count;
            _leg = null;
        }

        private void Fail(NPC npc)
        {
            npc.Direction = Vector2.Zero;
            State = JobState.Failed;
        }
    }
}
