using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Collision
{
    public static class Geometry
    {
        /// <summary>
        /// The smallest rectangle covering every box, or RectF.Empty when there are
        /// none. Used as a cheap broad-phase / depth-sort stand-in for a multi-box shape.
        /// </summary>
        public static RectF Union(IReadOnlyList<RectF> boxes)
        {
            if (boxes == null || boxes.Count == 0) return RectF.Empty;

            RectF u = boxes[0];
            for (int i = 1; i < boxes.Count; i++)
                u = RectF.Union(u, boxes[i]);
            return u;
        }

        /// <summary>
        /// Every cell the rectangle overlaps, row by row. Right and Bottom are one past the
        /// inside, so the last cell is the one just before them: a rectangle ending exactly
        /// on a cell edge doesn't reach the next cell. Cells can be off the map; callers check.
        /// </summary>
        public static IEnumerable<Point> CellsCovered(RectF rect)
        {
            int firstCol = (int)MathF.Floor(rect.Left), lastCol = (int)MathF.Ceiling(rect.Right) - 1;
            int firstRow = (int)MathF.Floor(rect.Top),  lastRow = (int)MathF.Ceiling(rect.Bottom) - 1;

            for (int row = firstRow; row <= lastRow; row++)
                for (int col = firstCol; col <= lastCol; col++)
                    yield return new Point(col, row);
        }
    }
}
