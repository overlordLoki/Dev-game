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

        // A shape can now be several boxes, not one. Boxes are world-space and derived
        // at draw/collision time; Bounds is their union, kept for cheap broad-phase work.
        IReadOnlyList<Rectangle> Boxes { get; }
        Rectangle Bounds { get; }
        // Draw and SortY come from IDrawable. Drawing is shared in GameObject now.
    }
}
