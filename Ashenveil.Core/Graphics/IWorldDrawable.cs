using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Graphics
{
    /// <summary>
    /// Anything drawn in the world and depth-sorted by its base (player, NPCs, trees).
    /// Deliberately not called IDrawable: MonoGame has its own Microsoft.Xna.Framework.IDrawable,
    /// and any file with `using Microsoft.Xna.Framework;` would silently pick that one up.
    /// </summary>
    public interface IWorldDrawable
    {
        int SortY { get; }                    // the base/feet Y — higher = drawn in front
        void Draw(SpriteBatch spriteBatch);
    }
}