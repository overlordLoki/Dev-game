using System;
using Ashenveil.Core.Collision;
using Ashenveil.Core.GameWorld.Locations;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Navigation
{
    // TODO: a <summary> saying what this is for: which cells an NPC can stand in,
    // built once per level from its objects, used by the Pathfinder.
    public class NavGrid
    {
        // TODO: a comment on why this is "blocked" and not "walkable":
        // a new bool[,] starts all false, so a new grid is already all walkable.
        // Indexed [row, col], the same order as TileMap's grid.
        private readonly bool[,] _blocked;

        public int Cols { get; }
        public int Rows { get; }
        

        // An all-walkable grid of this size, with no level behind it (the Pathfinder
        // tests shape their grids with this and Block).
        public NavGrid(int cols, int rows)
        {
            // Save Cols and Rows.
            Cols = cols;
            Rows = rows;

            // Create the array: new bool[rows, cols] - rows first, to match TileMap.
            _blocked = new bool[rows, cols];
        }

        // A level's grid: the size comes from its tile map (the ": this(...)" runs the
        // constructor above first), then every cell an object sits in the middle of is blocked.
        public NavGrid(Location location) : this(location.tileMap.Cols, location.tileMap.Rows)
        {
            // For each object in location.objects...
            foreach (var obj in location.objects)
            {
                // ...for each of its Boxes (already in cells)...
                foreach(var box in obj.Boxes)
                {
                    // Work out which cells this box overlaps. Copy the pattern from
                    // Location.ExitTouching: first = Floor(Left / Top),
                    // last = Ceiling(Right / Bottom) - 1 (Right and Bottom are one past the inside).
                    int firstCol = (int)MathF.Floor(box.Left);
                    int lastCol = (int)MathF.Ceiling(box.Right) - 1;
                    int firstRow = (int)MathF.Floor(box.Top);
                    int lastRow = (int)MathF.Ceiling(box.Bottom) - 1;
                    // Loop over those rows and cols...
                    for (int row = firstRow; row <= lastRow; row++)
                    {
                        for (int col = firstCol; col <= lastCol; col++)
                        {
                            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                            {
                                // Out of bounds, so skip it.
                                continue;
                            }
                            // Make the middle half of this cell:
                            // new RectF(col + 0.25f, row + 0.25f, 0.5f, 0.5f)
                            RectF middle = new RectF(col + 0.25f, row + 0.25f, 0.5f, 0.5f);
                            // If the box Intersects the middle, Block this cell.
                            if (!box.Intersects(middle))
                            {
                                // The box doesn't touch the middle of this cell, so skip it.
                                continue;
                            }
                            // (Touching edges don't count, so a trunk that only clips the
                            // cell's edge leaves it walkable.)     
                            if (box.Intersects(middle))
                            {
                                // The box touches the middle of this cell, so Block it.
                                Block(new Point(col, row));
                            }
                        }
                    }


  
                }
            }
        }

        public void Block(Point cell)
        {
            // Mark it blocked. Point is (X = col, Y = row), so the index is [cell.Y, cell.X].
            _blocked[cell.Y, cell.X] = true;
        }

        // TODO: a short comment - false off the map, as well as on a blocked cell.
        public bool IsWalkable(Point cell)
        {
            bool isWalkable = false;
            // First: is it on the map? X from 0 to Cols - 1, Y from 0 to Rows - 1.
            // (Could be its own private OnMap(cell) - the Location constructor wants it too.)
            if (cell.X < 0 || cell.X >= Cols || cell.Y < 0 || cell.Y >= Rows)
            {
                isWalkable = false;
            }
            // Then: is it not blocked?
            if(!_blocked[cell.Y, cell.X])
            {
                isWalkable = true;
            }
            // Join them with && in that order: if it's off the map, && never reads the
            // array, so there's no IndexOutOfRangeException.
            return isWalkable;
        }

        // Which cell a world point is in.
        public static Point CellAt(Vector2 world)
        {
            // Floor x and y, then cast to int. Use MathF.Floor, not a plain (int) cast:
            int x = (int)MathF.Floor(world.X);
            int y = (int)MathF.Floor(world.Y);
            // (int)-0.5f is 0, but a point left of the map is in cell -1.
            return new Point(x, y);
        }

        // The middle of a cell, in world units - the same spot as PointOfInterest.ToWorld().
        public static Vector2 CentreOf(Point cell)
        {
            // Add 0.5 to the col and the row (the 0.5f makes the sum a float).
            float x = cell.X + 0.5f;
            float y = cell.Y + 0.5f;
            return new Vector2(x, y);
        }
    }
}
