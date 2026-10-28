using System.Collections.Generic;
using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    /// <summary>
    /// Walks the NPC's feet to a point. With a <see cref="NavGrid"/> it routes round
    /// obstacles with the <see cref="Pathfinder"/>, one waypoint at a time; without one
    /// (tests, or a level with no grid) it heads straight at the target. Fails if there's
    /// no way through, or if it keeps getting stuck.
    /// </summary>
    public class GoToJob : IJob
    {
        public JobState State { get; set; } = JobState.Inactive;
        private readonly Vector2 _target;            // in cells
        private const float ArriveRadius = 0.03f;    // "close enough", in cells (~4px at 720p) — stops the jitter
        // Looser for the waypoints on the way: the NPC turns for the next one a little early
        // rather than stopping dead on every cell centre.
        private const float WaypointRadius = 0.1f;

        // Where the feet go, in order, in cells. Null until the first Execute plans the route.
        private List<Vector2> _waypoints;
        private int _next;                           // index of the waypoint being walked to

        // Getting stuck: no closer to the waypoint for StuckSeconds means replan; more than
        // MaxReplans replans means give up. Closer by less than ProgressEpsilon cells
        // doesn't count, so jittering against something isn't mistaken for progress.
        private const float StuckSeconds = 1f;
        private const int MaxReplans = 3;
        private const float ProgressEpsilon = 0.01f;
        private float _bestDistance = float.MaxValue;   // closest the feet have got to the waypoint
        private float _stuckFor;                        // seconds since they last got closer
        private int _replans;

        public GoToJob(Vector2 target) => _target = target;

        /// <summary>
        /// Where the feet are headed, in cells. All saving keeps: the route and stuck timers
        /// aren't saved, because the first Execute after loading plans again from wherever the
        /// NPC is, as it does for any job that's just starting.
        /// </summary>
        public Vector2 Target => _target;

        /// <summary>
        /// The waypoints still to walk, in cells, starting with the one being headed for.
        /// Empty until the first Execute plans the route. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints =>
            _waypoints == null ? System.Array.Empty<Vector2>() : _waypoints.GetRange(_next, _waypoints.Count - _next);

        public void Execute(JobContext context)
        {
            var npc = (NPC)context.Owner;

            // Plan on the first tick, not in the constructor: the job may wait in the queue
            // behind others, so where the NPC is now is only known once it starts.
            if (_waypoints == null && !StartRoute(context.Nav, npc)) return;

            // Walk the feet to each waypoint - the same point Location.PlaceAt stands an entity
            // on a POI by, so walking to a point ends exactly where spawning on it would.
            bool last = _next == _waypoints.Count - 1;
            Vector2 toWaypoint = _waypoints[_next] - npc.Feet;

            if (toWaypoint.Length() <= (last ? ArriveRadius : WaypointRadius))
            {
                if (last)
                {
                    npc.Direction = Vector2.Zero;      // stop
                    State = JobState.Completed;        // tell the queue we're done
                    return;
                }

                // Reached a waypoint on the way: steer at the next one this same tick.
                _next++;
                ResetProgress();
                toWaypoint = _waypoints[_next] - npc.Feet;
            }

            // Stuck check. The grid only knows about objects, so the player or another NPC
            // standing on the path can still block it. If the feet haven't got any closer to
            // the waypoint for a while, plan again from here; after a few goes, give up.
            float distance = toWaypoint.Length();
            if (distance < _bestDistance - ProgressEpsilon)
            {
                _bestDistance = distance;
                _stuckFor = 0f;
            }
            else if ((_stuckFor += context.DeltaTime) >= StuckSeconds)
            {
                if (++_replans > MaxReplans)
                {
                    Fail(npc);
                    return;
                }
                if (!StartRoute(context.Nav, npc)) return;
                toWaypoint = _waypoints[_next] - npc.Feet;
            }

            npc.Direction = Vector2.Normalize(toWaypoint);  // Entity.Move steps
        }

        // Plans a route from where the feet are now and starts walking it from the first
        // waypoint. False (and the job Failed) if there's no way through.
        private bool StartRoute(NavGrid nav, NPC npc)
        {
            _waypoints = Plan(nav, npc.Feet);
            _next = 0;
            ResetProgress();
            if (_waypoints != null) return true;

            Fail(npc);
            return false;
        }

        // A new waypoint to head for: start measuring progress towards it afresh.
        private void ResetProgress()
        {
            _bestDistance = float.MaxValue;
            _stuckFor = 0f;
        }

        private void Fail(NPC npc)
        {
            npc.Direction = Vector2.Zero;
            State = JobState.Failed;
        }

        // The cell centres along the route, then the exact target, smoothed into straight
        // runs; null if there's no way through. Without a grid it's just the target: the old
        // straight-line walk.
        private List<Vector2> Plan(NavGrid nav, Vector2 feet)
        {
            if (nav == null) return new List<Vector2> { _target };

            Point goal = NavGrid.CellAt(_target);
            List<Point> path = Pathfinder.FindPath(nav, NavGrid.CellAt(feet), goal);
            if (path == null) return null;

            // Skip path[0]: the feet are already in that cell, and heading for its centre
            // first could mean stepping backwards.
            var waypoints = new List<Vector2>();
            for (int i = 1; i < path.Count; i++)
                waypoints.Add(NavGrid.CentreOf(path[i]));

            // The path ends on the goal cell: finish on the exact target, not its centre.
            // If the goal was blocked (a POI on a tree), the path stops next to it, and the
            // centre of that cell is as close as the NPC can get.
            if (path[^1] == goal)
            {
                if (waypoints.Count > 0) waypoints[^1] = _target;
                else waypoints.Add(_target);   // already in the goal cell
            }
            else if (waypoints.Count == 0)
            {
                waypoints.Add(NavGrid.CentreOf(path[0]));   // already next to a blocked goal
            }

            // Smooth the zig-zags out, starting from the feet themselves (not their cell's
            // centre), then drop the feet again: they're where the NPC is, not somewhere to go.
            waypoints.Insert(0, feet);
            List<Vector2> smoothed = Pathfinder.Smooth(nav, waypoints);
            smoothed.RemoveAt(0);
            return smoothed;
        }
    }
}
