using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Screens
{
    public interface IScreen
    {
        void Update(GameTime gameTime);
        void Draw(SpriteBatch spriteBatch, Texture2D pixel);

        /// The matrix the screen's sprite batch is begun with. Menus and HUDs draw in
        /// screen space and want the default; the world overrides it with its camera
        /// so it can keep drawing in world coordinates.
        Matrix Transform => Matrix.Identity;
    }
}