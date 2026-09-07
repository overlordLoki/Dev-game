
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Tiles
{
    public interface Tile
    {
        int Width { get; }
        int Height { get; }
        string ImgName { get; }
        Texture2D texture2D { get; set; }

        public void Draw(SpriteBatch spriteBatch);
    }
}