using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Objects
{
    public interface IObject : IDrawable
    {
        int Col { get; }              // grid position — pixels are derived at draw time
        int Row { get; }
        Texture2D Texture { get; }
        int Rotate { get; }
        Rectangle Bounds { get; }
        // Draw and SortY come from IDrawable. Each object implements Draw itself.
        // #pragma warning disable CS0108 // Member hides inherited member; missing new keyword
        // public void Draw(SpriteBatch spriteBatch)
        // {
        //     int cellW = Layout.CellWidth;
        //     int cellH = Layout.CellHeight;
        //     int size  = (int)(Layout.CellSize * 1.5f);

        //     int x = Col * cellW + (cellW - size) / 2;
        //     int y = Row * cellH + (cellH - size);

        //     float radians = MathHelper.ToRadians(Rotate);
        //     spriteBatch.Draw(Texture, new Vector2(x, y), null, Color.White, radians, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
        // }
    }
}