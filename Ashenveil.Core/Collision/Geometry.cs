using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Collision
{
    public static class Geometry
    {
        /// <summary>
        /// The smallest rectangle covering every box, or Rectangle.Empty when there are
        /// none. Used as a cheap broad-phase / depth-sort stand-in for a multi-box shape.
        /// </summary>
        public static Rectangle Union(IReadOnlyList<Rectangle> boxes)
        {
            if (boxes == null || boxes.Count == 0) return Rectangle.Empty;

            Rectangle u = boxes[0];
            for (int i = 1; i < boxes.Count; i++)
                u = Rectangle.Union(u, boxes[i]);
            return u;
        }
    }
}
