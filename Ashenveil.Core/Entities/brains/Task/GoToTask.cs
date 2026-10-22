using System.Collections.Generic;
using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Task
{
    /// <summary>
    /// Walks the NPC's feet to a point. With a <see cref="NavGrid"/> it routes round
    /// obstacles with the <see cref="Pathfinder"/>, one waypoint at a time; without one
    /// (tests, or a level with no grid) it heads straight at the target.
    /// </summary>
    public class GoToTask : ITask
    {
        public TaskState State { get; set; } = TaskState.Inactive;
        private readonly Vector2 _target;            // in cells
        private const float ArriveRadius = 0.03f;    // "close enough", in cells (~4px at 720p) — stops the jitter
        // Looser for the waypoints on the way: the NPC turns for the next one a little early
        // rather than stopping dead on every cell centre.
        private const float WaypointRadius = 0.1f;

        // Where the feet go, in order, in cells. Null until the first Execute plans the route.
        private List<Vector2> _waypoints;
        private int _next;                           // index of the waypoint being walked to

        public GoToTask(Vector2 target) => _target = target;

        /// <summary>
        /// The waypoints still to walk, in cells, starting with the one being headed for.
        /// Empty until the first Execute plans the route. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints =>
            _waypoints == null ? System.Array.Empty<Vector2>() : _waypoints.GetRange(_next, _waypoints.Count - _next);

        public void Execute(TaskContext context)
        {
            var npc = (NPC)context.Owner;

            // Plan on the first tick, not in the constructor: the task may wait in the queue
            // behind others, so where the NPC is now is only known once it starts.
            _waypoints ??= Plan(context.Nav, npc.Feet);

            // Walk the feet to each waypoint - the same point Location.PlaceAt stands an entity
            // on a POI by, so walking to a point ends exactly where spawning on it would.
            bool last = _next == _waypoints.Count - 1;
            Vector2 toWaypoint = _waypoints[_next] - npc.Feet;

            if (toWaypoint.Length() <= (last ? ArriveRadius : WaypointRadius))
            {
                if (last)
                {
                    npc.Direction = Vector2.Zero;      // stop
                    State = TaskState.Completed;        // tell the queue we're done
                    return;
                }

                // Reached a waypoint on the way: steer at the next one this same tick.
                _next++;
                toWaypoint = _waypoints[_next] - npc.Feet;
            }

            npc.Direction = Vector2.Normalize(toWaypoint);  // Entity.Move steps
        }

        // The cell centres along the route, then the exact target. Without a grid, or with
        // no way through, it's just the target: the old straight-line walk.
        // TODO (pathfinding step 4): no way through should fail the task, not walk at a wall.
        private List<Vector2> Plan(NavGrid nav, Vector2 feet)
        {
            var straight = new List<Vector2> { _target };
            if (nav == null) return straight;

            Point goal = NavGrid.CellAt(_target);
            List<Point> path = Pathfinder.FindPath(nav, NavGrid.CellAt(feet), goal);
            if (path == null) return straight;

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

            return waypoints;
        }
    }
}
