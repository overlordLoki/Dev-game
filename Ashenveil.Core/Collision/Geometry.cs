using System.Collections.Generic;

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
    }
}
