using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Navigation
{
    /// <summary>
    /// A* search over a <see cref="NavGrid"/>: the cells an NPC walks through to get from
    /// one cell to another. Pure logic (no NPC, no drawing, no Layout), so it can be
    /// tested on small grids built in code.
    /// </summary>
    public static class Pathfinder
    {
        // Step costs, in whole numbers so the maths stays in ints.
        // A straight step is 10, a diagonal is 14 (√2 x 10, rounded).
        private const int Straight = 10;
        private const int Diagonal = 14;

        // The 8 directions, as (dx, dy) offsets. Point is (X = col, Y = row).
        // Straight ones first, then diagonals: the order doesn't change the result,
        // it just makes the list easy to read.
        private static readonly Point[] Directions =
        {
            new Point(1, 0),   // right
            new Point(-1, 0),  // left
            new Point(0, 1),   // down
            new Point(0, -1),  // up
            new Point(1, 1),   // down-right
            new Point(-1, 1),  // down-left
            new Point(1, -1),  // up-right
            new Point(-1, -1)  // up-left
        };

        // The cells from start to goal, both included; null if there's no way through.
        // If the goal is blocked (a POI placed on a tree), the path ends on the cheapest
        // reachable cell next to it instead.
        public static List<Point> FindPath(NavGrid grid, Point start, Point goal)
        {
            if (start == goal)
                return new List<Point> { start };

            // A blocked goal can never be stood on, so any cell next to it counts as
            // arriving. A blocked START is fine: the NPC may have been pushed half into
            // an object's cell, and only its neighbours need to be walkable.
            bool goalBlocked = !grid.IsWalkable(goal);

            // With a blocked goal the search stops one step short, so the guess is cut by
            // one diagonal step. Otherwise it could overestimate, and A* might miss the
            // shortest route.
            int Guess(Point cell) => goalBlocked
                ? Math.Max(0, Heuristic(cell, goal) - Diagonal)
                : Heuristic(cell, goal);

            // open: the cells to look at next, lowest f = g + h first.
            // cameFrom: "I reached this cell from that one".
            // gScore: the cheapest known cost from start to a cell.
            var open = new PriorityQueue<Point, int>();
            var cameFrom = new Dictionary<Point, Point>();
            var gScore = new Dictionary<Point, int> { [start] = 0 };
            open.Enqueue(start, Guess(start));

            while (open.TryDequeue(out var current, out _))
            {
                if (current == goal || (goalBlocked && IsNextTo(current, goal)))
                    return ReconstructPath(cameFrom, current);

                foreach (var direction in Directions)
                {
                    var next = current + direction;

                    // Off the map counts as not walkable too.
                    if (!grid.IsWalkable(next))
                        continue;

                    // No corner cutting: a diagonal step needs both straight cells beside
                    // it to be walkable, or the NPC would clip the corner of a tree.
                    bool diagonal = direction.X != 0 && direction.Y != 0;
                    if (diagonal &&
                        (!grid.IsWalkable(new Point(next.X, current.Y)) ||
                         !grid.IsWalkable(new Point(current.X, next.Y))))
                        continue;

                    // Skip it if we already know a route to next that's as cheap.
                    int tentativeG = gScore[current] + (diagonal ? Diagonal : Straight);
                    if (gScore.TryGetValue(next, out int oldG) && tentativeG >= oldG)
                        continue;

                    // The best route to next so far. A cell can be queued more than once
                    // (PriorityQueue can't lower a priority); the cheaper copy comes out first.
                    cameFrom[next] = current;
                    gScore[next] = tentativeG;
                    open.Enqueue(next, tentativeG + Guess(next));
                }
            }

            // The queue is empty and the goal was never reached: the goal is walled off.
            return null;
        }

        // Octile distance: the exact cost on an empty 8-direction grid. Go diagonally
        // min(dx, dy) times, then straight for the rest; each diagonal saves
        // 2 * Straight - Diagonal over the two straight steps it replaces. It never
        // overestimates, so the path A* finds is the shortest.
        public static int Heuristic(Point a, Point b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return Straight * (dx + dy) - (2 * Straight - Diagonal) * Math.Min(dx, dy);
        }

        // How far either side of a straight walk must be clear, in cells: room for the feet,
        // so a smoothed line doesn't skim the edge of a tree's cell.
        private const float Clearance = 0.25f;
        // How often CanWalkStraight checks along the line, in cells.
        private const float SampleStep = 0.05f;

        /// <summary>
        /// Can the feet walk from one point to the other in a straight line? Checks points
        /// along the line, and either side of it by <see cref="Clearance"/>, are all on
        /// walkable cells. Points are in cells, like everything else.
        /// </summary>
        public static bool CanWalkStraight(NavGrid grid, Vector2 from, Vector2 to)
        {
            Vector2 line = to - from;
            float length = line.Length();
            if (length == 0f) return grid.IsWalkable(NavGrid.CellAt(from));

            // A quarter-turn of the line's direction points sideways; scale it to the clearance.
            Vector2 side = new Vector2(-line.Y, line.X) / length * Clearance;
            int samples = (int)MathF.Ceiling(length / SampleStep);
            for (int i = 0; i <= samples; i++)
            {
                Vector2 p = from + line * (i / (float)samples);
                if (!grid.IsWalkable(NavGrid.CellAt(p)) ||
                    !grid.IsWalkable(NavGrid.CellAt(p + side)) ||
                    !grid.IsWalkable(NavGrid.CellAt(p - side)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// String-pulling: drops every point the walker can skip by heading straight for a
        /// later one. Grid paths zig-zag (straight, diagonal, straight); this turns them into
        /// long straight runs. Always keeps the first and last points.
        /// </summary>
        public static List<Vector2> Smooth(NavGrid grid, IReadOnlyList<Vector2> points)
        {
            var smoothed = new List<Vector2>();
            if (points.Count == 0) return smoothed;

            // From the last point kept (the anchor), look further and further ahead. When the
            // next point can't be seen, the one before it is a corner: keep it, and look on
            // from there.
            int anchor = 0;
            smoothed.Add(points[0]);
            for (int i = 2; i < points.Count; i++)
            {
                if (!CanWalkStraight(grid, points[anchor], points[i]))
                {
                    anchor = i - 1;
                    smoothed.Add(points[anchor]);
                }
            }
            if (points.Count > 1) smoothed.Add(points[^1]);
            return smoothed;
        }

        // One of the 8 cells around b.
        private static bool IsNextTo(Point a, Point b) =>
            a != b && Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;

        // Walk cameFrom backwards from the end to the start, then flip it round.
        // Start has no entry in cameFrom, so the loop stops there.
        private static List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point current)
        {
            var path = new List<Point> { current };
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                path.Add(current);
            }
            path.Reverse();
            return path;
        }
    }
}
