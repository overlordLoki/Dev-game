using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Collision
{
    /// <summary>
    /// One collision box, stored as fractions of a base rectangle so it holds at any
    /// zoom. For objects the base is a grid cell; for entities it's the sprite's own
    /// Width x Height. X/Y are the top-left corner, measured from the base's top-left,
    /// so a box can sit anywhere - the old "centred + shift" model was just one case
    /// of this and couldn't describe a second box off to the side.
    /// </summary>
    public readonly struct Box
    {
        public readonly float X, Y, W, H;

        public Box(float x, float y, float w, float h)
        {
            X = x; Y = y; W = w; H = h;
        }

        /// <summary>Turns the fractions into a pixel rectangle against a placed base.</summary>
        public Rectangle ToRectangle(int originX, int originY, int baseW, int baseH) =>
            new Rectangle(
                originX + (int)(baseW * X),
                originY + (int)(baseH * Y),
                (int)(baseW * W),
                (int)(baseH * H));
    }
}
