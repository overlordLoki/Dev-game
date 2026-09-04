
using Microsoft.Xna.Framework.Graphics;

namespace merder.Core.Tiles
{
    public enum TileType { Grass, Dirt, DirtRoad, DirtRoadCross }
    public interface Tile
    {
        int Width { get; }
        int Height { get; }
        string ImgName { get; }
        Texture2D texture2D { get; set; }

        public void Draw(SpriteBatch spriteBatch);
    }
}