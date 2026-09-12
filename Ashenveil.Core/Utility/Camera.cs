using System;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Utility
{
    /// <summary>
    /// A scrolling view onto the world. Holds the world-space point that sits at the
    /// window's top-left corner and hands out the matrix that shifts everything by it,
    /// so world-space draw calls land in the right screen pixels without any sprite
    /// having to know the camera exists.
    /// </summary>
    public class Camera
    {
        /// Top-left of the view, in world space.
        public Vector2 Position { get; private set; }

        /// Feed this to SpriteBatch.Begin(transformMatrix:).
        public Matrix View => Matrix.CreateTranslation(-Position.X, -Position.Y, 0f);

        /// <summary>
        /// Centres the view on <paramref name="target"/> but never scrolls past the
        /// edges of <paramref name="world"/>. If the world is smaller than the window
        /// on an axis it is centred instead - clamping there would have no valid range.
        /// </summary>
        public void Follow(Vector2 target, int viewWidth, int viewHeight, Rectangle world)
        {
            float x = target.X - viewWidth / 2f;
            float y = target.Y - viewHeight / 2f;

            x = world.Width <= viewWidth
                ? world.Left + (world.Width - viewWidth) / 2f
                : MathHelper.Clamp(x, world.Left, world.Right - viewWidth);

            y = world.Height <= viewHeight
                ? world.Top + (world.Height - viewHeight) / 2f
                : MathHelper.Clamp(y, world.Top, world.Bottom - viewHeight);

            // Whole pixels only: a fractional offset makes neighbouring tiles sample
            // half a pixel apart and show seams as the view moves.
            Position = new Vector2(MathF.Round(x), MathF.Round(y));
        }

        /// Screen pixel (e.g. a mouse position) -> world position. Needed by anything
        /// that reads input in screen space but acts on the world.
        public Vector2 ToWorld(Vector2 screenPosition) => screenPosition + Position;
    }
}
