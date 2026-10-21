using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Microsoft.Xna.Framework.Graphics;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Core.Objects
{
    public interface IObject : IWorldDrawable
    {
        int Col { get; }              // grid position — pixels are derived at draw time
        int Row { get; }
        Texture2D Texture { get; }
        int Rotate { get; }

        // A shape can now be several boxes, not one. Boxes are in world units (cells) and
        // derived at collision time; Bounds is their union, kept for cheap broad-phase work.
        IReadOnlyList<RectF> Boxes { get; }
        RectF Bounds { get; }
        // Draw and SortY come from IWorldDrawable. Drawing is shared in GameObject now.
    }
}
