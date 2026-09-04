using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace merder.Core.Screens
{
    public interface IScreen
    {
        void Update(GameTime gameTime);
        void Draw(SpriteBatch spriteBatch, Texture2D pixel);

        
    }
}