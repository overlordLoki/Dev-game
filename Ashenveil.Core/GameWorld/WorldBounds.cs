using Ashenveil.Core.Collision;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Tiles;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// The edges of the playable map, in world units (cells). Replaces the old Gamebox, which
    /// fenced entities inside a fixed inset of the *screen* - fine before there were
    /// tiles, wrong the moment the world is bigger than the window or a camera moves.
    ///
    /// The area is the whole tile grid, (0,0) to (Cols, Rows). Being in cells, it doesn't
    /// change when the window is resized.
    ///
    /// It only keeps things in. How something reacts to hitting the edge is up to it
    /// (IEntity.OnBlocked): the player just stops, an NPC asks its brain.
    /// </summary>
    public class WorldBounds
    {
        private readonly TileMap _map;

        public WorldBounds(TileMap map) => _map = map;

        /// <summary>The whole map, in cells.</summary>
        public RectF Area => new RectF(0, 0, _map.Cols, _map.Rows);

        /// <summary>
        /// Pushes an entity back inside the map if its body has crossed an edge, then tells it
        /// which way it was stopped. Measures the collision box (the feet and body), not the
        /// sprite - sprites have transparent padding, which would stop entities short of the
        /// edge. An entity with no collision boxes falls back to its sprite.
        /// </summary>
        public void Clamp(IEntity entity)
        {
            RectF area = Area;
            RectF body = entity.Bounds;
            if (body.IsEmpty)
                body = new RectF(entity.Position.X, entity.Position.Y, entity.Width, entity.Height);

            // How far to move the entity to get its body back inside, on each axis.
            float pushX = body.Left < area.Left   ? area.Left - body.Left
                        : body.Right > area.Right ? area.Right - body.Right
                        : 0f;
            float pushY = body.Top < area.Top       ? area.Top - body.Top
                        : body.Bottom > area.Bottom ? area.Bottom - body.Bottom
                        : 0f;

            if (pushX == 0f && pushY == 0f) return;   // inside: nothing to do

            entity.Position += new Vector2(pushX, pushY);
            if (pushX != 0f) entity.OnBlocked(horizontal: true);
            if (pushY != 0f) entity.OnBlocked(horizontal: false);
        }
    }
}
