using Ashenveil.Core.Entities;
using Ashenveil.Core.Tiles;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// The edges of the playable map, in world space. Replaces the old Gamebox, which
    /// fenced entities inside a fixed inset of the *screen* - fine before there were
    /// tiles, wrong the moment the world is bigger than the window or a camera moves.
    ///
    /// The rectangle is the whole tile grid (0,0) to (Cols*CellWidth, Rows*CellHeight),
    /// recomputed from Layout each time it's asked so it tracks the current cell size -
    /// no need to rebuild this on a window resize the way Gamebox did.
    /// </summary>
    public class WorldBounds
    {
        private readonly TileMap _map;

        public WorldBounds(TileMap map) => _map = map;

        public Rectangle Bounds => new Rectangle(
            0, 0,
            _map.Cols * Layout.CellWidth,
            _map.Rows * Layout.CellHeight);

        /// <summary>
        /// Pushes an entity back inside the map if it has crossed an edge. NPCs also flip
        /// their heading so a wanderer turns back rather than pressing into the wall; the
        /// player has no heading to flip, so it simply stops at the edge.
        /// </summary>
        public void Clamp(IEntity entity)
        {
            Rectangle bounds = Bounds;

            // left wall
            if (entity.Position.X < bounds.Left)
            {
                entity.Position = new Vector2(bounds.Left, entity.Position.Y);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(-entity.direction.X, entity.direction.Y);
            }
            // right wall
            if (entity.Position.X + entity.Width > bounds.Right)
            {
                entity.Position = new Vector2(bounds.Right - entity.Width, entity.Position.Y);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(-entity.direction.X, entity.direction.Y);
            }
            // top wall
            if (entity.Position.Y < bounds.Top)
            {
                entity.Position = new Vector2(entity.Position.X, bounds.Top);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(entity.direction.X, -entity.direction.Y);
            }
            // bottom wall
            if (entity.Position.Y + entity.Height > bounds.Bottom)
            {
                entity.Position = new Vector2(entity.Position.X, bounds.Bottom - entity.Height);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(entity.direction.X, -entity.direction.Y);
            }
        }
    }
}
