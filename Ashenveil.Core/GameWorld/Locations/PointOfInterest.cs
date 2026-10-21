using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld.Locations
{
    /// <summary>
    /// A named point on a location's grid — a spawn cell, a shop counter, where an NPC
    /// starts. Placed in the editor and looked up by name in code
    /// (see <see cref="Location.Poi"/>). A class rather than a bare Vector2 so it can
    /// grow later: a facing direction, the tile it sits on, and so on.
    ///
    /// X and Y are grid cells (col, row). <see cref="StandingSpot"/> is where something stands
    /// on it (spawning, walking to it); <see cref="ToWorld"/> is the cell's exact centre.
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

        /// <summary>
        /// How far down a cell an entity's feet go when it stands on that cell: near the bottom,
        /// so its body fills the cell rather than sitting in the top half and poking into the
        /// cell above. Not the very bottom (1.0): there the feet would be on the edge of the
        /// next row, and a door's touch leeway would reach a door in the cell below.
        /// </summary>
        public const float FeetDown = 0.9f;

        /// <summary>The centre of this cell in world units (cells), e.g. for a path's waypoints.</summary>
        public Vector2 ToWorld() => new Vector2(X + 0.5f, Y + 0.5f);

        /// <summary>
        /// Where an entity's feet go to stand on this cell, in cells: centred across it, near
        /// the bottom. What spawning (Location.PlaceAt) and walking to a point (GoToTask) aim at.
        /// </summary>
        public Vector2 StandingSpot() => new Vector2(X + 0.5f, Y + FeetDown);
    }
}
