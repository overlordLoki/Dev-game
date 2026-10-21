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

        /// <summary>Turns the fractions into a world rectangle (in cells) against a placed base.</summary>
        public RectF ToRect(float originX, float originY, float baseW, float baseH) =>
            new RectF(originX + baseW * X, originY + baseH * Y, baseW * W, baseH * H);
    }
}
