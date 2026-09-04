using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace merder.Core.Objects
{
    public interface IObject : IDrawable
    {
        int Col { get; }              // grid position — pixels are derived at draw time
        int Row { get; }
        Texture2D Texture { get; }
        int Rotate { get; }
        Rectangle Bounds { get; }
        // Draw and SortY come from IDrawable. Each object implements Draw itself.
    }
}