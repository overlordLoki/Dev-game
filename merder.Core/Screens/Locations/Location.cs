using System.Collections.Generic;
using merder.Core.Objects;
using merder.Core.Tiles;
using Microsoft.Xna.Framework.Graphics;

namespace merder.Core.Screens
{
    public interface Location
    {
        //TileMap
        TileMap tileMap { get; set; }
        List<IObject> objects { get; set; }
        void Draw(SpriteBatch spriteBatch)
        {
            tileMap.Draw(spriteBatch);
            // objects sit on top of the ground
            foreach (var obj in objects)
                obj.Draw(spriteBatch);
        }
    }
}