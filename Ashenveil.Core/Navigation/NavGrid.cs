using System;
using Ashenveil.Core.Collision;
using Ashenveil.Core.GameWorld.Locations;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Navigation
{
    /// <summary>
    /// Which cells of a level an NPC can stand in. Built once per level from its objects'
    /// collision boxes, and searched by the <see cref="Pathfinder"/> to route around them.
    /// </summary>
    public class NavGrid
    {
        // Stored as "blocked" rather than "walkable" because a new bool[,] starts all
        // false, so a fresh grid is already all walkable with no setup loop.
        // Indexed [row, col], the same order as TileMap's grid.
        private readonly bool[,] _blocked;

        public int Cols { get; }
        public int Rows { get; }

        // An all-walkable grid of this size, with no level behind it (the Pathfinder
        // tests shape their grids with this and Block).
        public NavGrid(int cols, int rows)
        {
            Cols = cols;
            Rows = rows;
            _blocked = new bool[rows, cols];
        }

        // A level's grid: the size comes from its tile map (the ": this(...)" runs the
        // constructor above first), then every cell an object sits in the middle of is blocked.
        public NavGrid(Location location) : this(location.tileMap.Cols, location.tileMap.Rows)
        {
            foreach (var obj in location.objects)
            {
                foreach (var box in obj.Boxes)
                {
                    foreach (var cell in Geometry.CellsCovered(box))
                    {
                        if (!OnMap(cell)) continue;   // a box can hang over the map's edge

                        // Only block the cell if the box covers its middle half, so a
                        // trunk that just clips the cell's edge leaves it walkable.
                        var middle = new RectF(cell.X + 0.25f, cell.Y + 0.25f, 0.5f, 0.5f);
                        if (box.Intersects(middle))
                            Block(cell);
                    }
                }
            }
        }

        public void Block(Point cell)
        {
            // Off the map is a caller bug, so say which cell instead of a bare IndexOutOfRange.
            if (!OnMap(cell))
                throw new ArgumentOutOfRangeException(nameof(cell), cell, $"Cell is off the {Cols}x{Rows} grid.");

            // Point is (X = col, Y = row), so the index is [cell.Y, cell.X].
            _blocked[cell.Y, cell.X] = true;
        }

        // False off the map, as well as on a blocked cell. The && stops the array being
        // read for an off-map cell, so there's no IndexOutOfRangeException.
        public bool IsWalkable(Point cell) => OnMap(cell) && !_blocked[cell.Y, cell.X];

        private bool OnMap(Point cell) =>
            cell.X >= 0 && cell.X < Cols && cell.Y >= 0 && cell.Y < Rows;

        // Which cell a world point is in. MathF.Floor, not a plain (int) cast:
        // (int)-0.5f is 0, but a point left of the map is in cell -1.
        public static Point CellAt(Vector2 world) =>
            new Point((int)MathF.Floor(world.X), (int)MathF.Floor(world.Y));

        // The middle of a cell, in world units - the same spot as PointOfInterest.ToWorld().
        public static Vector2 CentreOf(Point cell) =>
            new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
    }
}
