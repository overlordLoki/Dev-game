using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Navigation
{
    /// <summary>What a <see cref="Route.Step"/> found.</summary>
    public enum RouteStep
    {
        Walking,   // keep going: steer along the heading
        Arrived,   // the feet are on the goal
        GaveUp,    // no way through, or stuck too many times
    }

    /// <summary>
    /// Walks some feet to a goal, in cells: plans the waypoints with the <see cref="Pathfinder"/>,
    /// steps along them one at a time, and replans when stuck. Knows nothing about NPCs or
    /// jobs - it hands back a heading and a <see cref="RouteStep"/>, and the caller decides
    /// what to do with them - so anything that walks (a job, a combat chase) can use it.
    /// </summary>
    public class Route
    {
        private const float ArriveRadius = 0.03f;    // "close enough", in cells (~4px at 720p) — stops the jitter
        // Looser for the waypoints on the way: the walker turns for the next one a little early
        // rather than stopping dead on every cell centre.
        private const float WaypointRadius = 0.1f;

        // Getting stuck: no closer to the waypoint for StuckSeconds means replan; more than
        // MaxReplans replans means give up. Closer by less than ProgressEpsilon cells
        // doesn't count, so jittering against something isn't mistaken for progress.
        private const float StuckSeconds = 1f;
        private const int MaxReplans = 3;
        private const float ProgressEpsilon = 0.01f;

        private NavGrid _nav;                        // kept from Plan, for replanning when stuck
        private Vector2 _goal;                       // in cells

        // Where the feet go, in order, in cells. Null until planned, or if there's no way through.
        private List<Vector2> _waypoints;
        private int _next;                           // index of the waypoint being walked to

        private float _bestDistance = float.MaxValue;   // closest the feet have got to the waypoint
        private float _stuckFor;                        // seconds since they last got closer
        private int _replans;                           // over the whole route, not reset by Plan

        /// <summary>True once <see cref="Plan"/> has found a way through.</summary>
        public bool IsPlanned => _waypoints != null;

        /// <summary>
        /// The waypoints still to walk, in cells, starting with the one being headed for.
        /// Empty until planned. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> Remaining =>
            _waypoints == null ? System.Array.Empty<Vector2>() : _waypoints.GetRange(_next, _waypoints.Count - _next);

        /// <summary>
        /// Plans a route from <paramref name="feet"/> to <paramref name="goal"/> and starts
        /// walking it from the first waypoint. Without a grid it's a straight line. False if
        /// there's no way through. Calling it again (a moving goal) starts afresh, but doesn't
        /// reset the stuck count.
        /// </summary>
        public bool Plan(NavGrid nav, Vector2 feet, Vector2 goal)
        {
            _nav = nav;
            _goal = goal;
            _waypoints = Build(nav, feet, goal);
            _next = 0;
            ResetProgress();
            return _waypoints != null;
        }

        /// <summary>
        /// One tick along the route from where the <paramref name="feet"/> are now. Walking
        /// sets <paramref name="heading"/> (a unit vector); Arrived and GaveUp leave it zero.
        /// GaveUp too if there's no route planned.
        /// </summary>
        public RouteStep Step(Vector2 feet, float dt, out Vector2 heading)
        {
            heading = Vector2.Zero;
            if (_waypoints == null) return RouteStep.GaveUp;

            bool last = _next == _waypoints.Count - 1;
            Vector2 toWaypoint = _waypoints[_next] - feet;

            if (toWaypoint.Length() <= (last ? ArriveRadius : WaypointRadius))
            {
                if (last) return RouteStep.Arrived;

                // Reached a waypoint on the way: steer at the next one this same tick.
                _next++;
                ResetProgress();
                toWaypoint = _waypoints[_next] - feet;
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
            else if ((_stuckFor += dt) >= StuckSeconds)
            {
                if (++_replans > MaxReplans) return RouteStep.GaveUp;
                if (!Plan(_nav, feet, _goal)) return RouteStep.GaveUp;
                toWaypoint = _waypoints[_next] - feet;
            }

            heading = Vector2.Normalize(toWaypoint);
            return RouteStep.Walking;
        }

        // A new waypoint to head for: start measuring progress towards it afresh.
        private void ResetProgress()
        {
            _bestDistance = float.MaxValue;
            _stuckFor = 0f;
        }

        // The cell centres along the route, then the exact goal, smoothed into straight
        // runs; null if there's no way through. Without a grid it's just the goal: the old
        // straight-line walk.
        private static List<Vector2> Build(NavGrid nav, Vector2 feet, Vector2 goal)
        {
            if (nav == null) return new List<Vector2> { goal };

            Point goalCell = NavGrid.CellAt(goal);
            List<Point> path = Pathfinder.FindPath(nav, NavGrid.CellAt(feet), goalCell);
            if (path == null) return null;

            // Skip path[0]: the feet are already in that cell, and heading for its centre
            // first could mean stepping backwards.
            var waypoints = new List<Vector2>();
            for (int i = 1; i < path.Count; i++)
                waypoints.Add(NavGrid.CentreOf(path[i]));

            // The path ends on the goal cell: finish on the exact goal, not its centre.
            // If the goal was blocked (a POI on a tree), the path stops next to it, and the
            // centre of that cell is as close as the feet can get.
            if (path[^1] == goalCell)
            {
                if (waypoints.Count > 0) waypoints[^1] = goal;
                else waypoints.Add(goal);   // already in the goal cell
            }
            else if (waypoints.Count == 0)
            {
                waypoints.Add(NavGrid.CentreOf(path[0]));   // already next to a blocked goal
            }

            // Smooth the zig-zags out, starting from the feet themselves (not their cell's
            // centre), then drop the feet again: they're where the walker is, not somewhere to go.
            waypoints.Insert(0, feet);
            List<Vector2> smoothed = Pathfinder.Smooth(nav, waypoints);
            smoothed.RemoveAt(0);
            return smoothed;
        }
    }
}