using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core
{
    public interface IDrawable
    {
        int SortY { get; }                    // the base/feet Y — higher = drawn in front
        void Draw(SpriteBatch spriteBatch);
    }
}