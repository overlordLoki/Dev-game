using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Navigation
{
    /// <summary>
    /// Plans the waypoints from some feet to a goal, in cells, routing round obstacles with
    /// the <see cref="Pathfinder"/>. Knows nothing about NPCs or jobs, so anything that walks
    /// (a job, a combat chase) can use it.
    /// </summary>
    public class Route
    {
        // The cell centres along the route, then the exact goal, smoothed into straight
        // runs; null if there's no way through. Without a grid it's just the goal: the old
        // straight-line walk.
        public List<Vector2> Plan(NavGrid nav, Vector2 feet, Vector2 goal)
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