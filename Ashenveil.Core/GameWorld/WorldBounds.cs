using Ashenveil.Core.Entities;
using Ashenveil.Core.Tiles;
using Microsoft.Xna.Framework;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// The edges of the playable map, in world space. Replaces the old Gamebox, which
    /// fenced entities inside a fixed inset of the *screen* - fine before there were
    /// tiles, wrong the moment the world is bigger than the window or a camera moves.
    ///
    /// The area is the whole tile grid (0,0) to (Cols*CellWidth, Rows*CellHeight),
    /// recomputed from Layout each time it's asked so it tracks the current cell size -
    /// no need to rebuild this on a window resize the way Gamebox did.
    ///
    /// It only keeps things in. How something reacts to hitting the edge is up to it
    /// (IEntity.OnBlocked): the player just stops, an NPC asks its brain.
    /// </summary>
    public class WorldBounds
    {
        private readonly TileMap _map;

        public WorldBounds(TileMap map) => _map = map;

        /// <summary>The whole map, in pixels.</summary>
        public Rectangle Area => new Rectangle(
            0, 0,
            _map.Cols * Layout.CellWidth,
            _map.Rows * Layout.CellHeight);

        /// <summary>
        /// Pushes an entity back inside the map if its body has crossed an edge, then tells it
        /// which way it was stopped. Measures the collision box (the feet and body), not the
        /// sprite - sprites have transparent padding, which would stop entities short of the
        /// edge. An entity with no collision boxes falls back to its sprite.
        /// </summary>
        public void Clamp(IEntity entity)
        {
            Rectangle area = Area;
            Rectangle body = entity.Bounds;
            if (body.IsEmpty)
                body = new Rectangle((int)entity.Position.X, (int)entity.Position.Y, entity.Width, entity.Height);

            // How far to move the entity to get its body back inside, on each axis.
            int pushX = body.Left < area.Left   ? area.Left - body.Left
                      : body.Right > area.Right ? area.Right - body.Right
                      : 0;
            int pushY = body.Top < area.Top       ? area.Top - body.Top
                      : body.Bottom > area.Bottom ? area.Bottom - body.Bottom
                      : 0;

            if (pushX == 0 && pushY == 0) return;   // inside: nothing to do

            entity.Position += new Vector2(pushX, pushY);
            if (pushX != 0) entity.OnBlocked(horizontal: true);
            if (pushY != 0) entity.OnBlocked(horizontal: false);
        }
    }
}
