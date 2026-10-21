using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Collision
{
    /// <summary>
    /// A rectangle in world units (cells), with float edges. MonoGame's Rectangle is whole
    /// pixels only, which is fine for drawing but not for a world measured in cells: a
    /// player's feet are 0.13 cells tall. Collision, bounds and exits all use this; only
    /// drawing turns it into a pixel Rectangle (Layout.ToScreen).
    ///
    /// Edges work like Rectangle's: Right and Bottom are one past the inside, so two
    /// rectangles that only touch (a.Right == b.Left) don't intersect.
    /// </summary>
    public readonly record struct RectF(float X, float Y, float Width, float Height)
    {
        public static readonly RectF Empty = default;

        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;
        public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);

        /// <summary>No area. What Geometry.Union gives for an entity with no collision boxes.</summary>
        public bool IsEmpty => Width <= 0f || Height <= 0f;

        /// <summary>True when they overlap. Only touching edges doesn't count.</summary>
        public bool Intersects(RectF other) =>
            other.Left < Right && Left < other.Right && other.Top < Bottom && Top < other.Bottom;

        /// <summary>Grown by <paramref name="dx"/> on the left and right, <paramref name="dy"/> on the top and bottom.</summary>
        public RectF Inflate(float dx, float dy) => new(X - dx, Y - dy, Width + dx * 2, Height + dy * 2);

        /// <summary>The overlap of two rectangles, or Empty if they don't overlap.</summary>
        public static RectF Intersect(RectF a, RectF b)
        {
            if (!a.Intersects(b)) return Empty;
            float left = Math.Max(a.Left, b.Left), top = Math.Max(a.Top, b.Top);
            return new RectF(left, top, Math.Min(a.Right, b.Right) - left, Math.Min(a.Bottom, b.Bottom) - top);
        }

        /// <summary>The smallest rectangle covering both.</summary>
        public static RectF Union(RectF a, RectF b)
        {
            float left = Math.Min(a.Left, b.Left), top = Math.Min(a.Top, b.Top);
            return new RectF(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
        }
    }
}
