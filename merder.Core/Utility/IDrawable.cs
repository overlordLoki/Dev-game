using Microsoft.Xna.Framework.Graphics;

namespace merder.Core
{
    public interface IDrawable
    {
        int SortY { get; }                    // the base/feet Y — higher = drawn in front
        void Draw(SpriteBatch spriteBatch);
    }
}