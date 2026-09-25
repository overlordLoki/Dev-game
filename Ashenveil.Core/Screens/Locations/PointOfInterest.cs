using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Screens.Locations
{
    /// <summary>
    /// A named point on a location's grid — a spawn cell, a shop counter, where an NPC
    /// starts. Placed in the editor and looked up by name in code
    /// (see <see cref="Location.Poi"/>). A class rather than a bare Vector2 so it can
    /// grow later: a facing direction, the tile it sits on, and so on.
    ///
    /// X and Y are grid cells (col, row), not pixels, so a point stays on its tile when
    /// the window resizes. Call <see cref="ToPixel"/> to get a world position to spawn at.
    /// </summary>
    public class PointOfInterest
    {
        public string Name { get; }
        public int X { get; }   // column
        public int Y { get; }   // row

        public PointOfInterest(string name, int x, int y)
        {
            Name = name;
            X = x;
            Y = y;
        }

        /// <summary>The centre of this cell in world pixels — where an entity spawns.</summary>
        public Vector2 ToPixel() =>
            new Vector2(X * Layout.CellWidth  + Layout.CellWidth  / 2f,
                        Y * Layout.CellHeight + Layout.CellHeight / 2f);
    }
}
