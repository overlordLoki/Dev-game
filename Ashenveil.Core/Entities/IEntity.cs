using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// What the world needs from anything that moves around in it. Contract only - the
    /// shared behaviour (movement, collision, animated drawing) lives in the Entity base
    /// class, where subclasses can override it properly instead of hiding a default
    /// interface method. Everything here is in world units (cells), not pixels.
    /// </summary>
    public interface IEntity : IWorldDrawable
    {
        int Id { get; }
        Vector2 Position { get; set; }            // the sprite's top-left, in cells
        float Speed { get; set; }                 // cells per second
        Vector2 Direction { get; set; }
        float Width { get; }                      // the drawn sprite's size, in cells
        float Height { get; }
        IReadOnlyList<RectF> Boxes { get; }       // tight collision boxes (not the whole sprite)
        RectF Bounds { get; }                     // union of Boxes, the mover's body box

        // Draw and SortY come from IWorldDrawable
        void Update(GameTime gameTime);
        void CheckEntityCollision(IEntity other);
        void ResolveCollision(IReadOnlyList<RectF> others);
        void ResolveCollision(RectF other);
        /// <summary>Something stopped this entity - another entity, or the edge of the map.</summary>
        void OnBlocked(bool horizontal);
    }
}
