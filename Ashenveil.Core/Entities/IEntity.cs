using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// What the world needs from anything that moves around in it. Contract only - the
    /// shared behaviour (movement, collision, animated drawing) lives in the Entity base
    /// class, where subclasses can override it properly instead of hiding a default
    /// interface method.
    /// </summary>
    public interface IEntity : IDrawable
    {
        int Id { get; }
        Vector2 Position { get; set; }
        float Speed { get; set; }
        Vector2 Direction { get; set; }
        int Width { get; }
        int Height { get; }
        IReadOnlyList<Rectangle> Boxes { get; }   // tight collision boxes (not the whole sprite)
        Rectangle Bounds { get; }                 // union of Boxes, the mover's body box

        // Draw and SortY come from IDrawable
        void Update(GameTime gameTime);
        void CheckEntityCollision(IEntity other);
        void ResolveCollision(IReadOnlyList<Rectangle> others);
        void ResolveCollision(Rectangle other);
    }
}
