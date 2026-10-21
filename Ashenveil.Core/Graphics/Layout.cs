using System;
using Ashenveil.Core.Collision;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Graphics
{
    /// <summary>
    /// Single source of truth for tile-cell size, and the one place world units (cells)
    /// turn into screen pixels.
    ///
    /// The game itself works in cells: positions, sizes, speeds, collision, tasks. A cell is
    /// 1 world unit, so (5.5, 3.25) is 5½ cells right and 3¼ down from the map's top-left -
    /// whatever size the window is. Only drawing, the camera and mouse input need pixels, and
    /// they convert with ToScreen / ToWorld. So resizing the window changes how big things
    /// are drawn, never where they are.
    ///
    /// The cell is sized to show a fixed number of rows, NOT to squeeze the whole
    /// grid into the window: the map is deliberately allowed to be larger than the
    /// view so the Camera has something to scroll over. A bigger window therefore
    /// means bigger tiles, not more of the map.
    /// </summary>
    public static class Layout
    {
        /// How many tile rows fill the window's height. Lower = more zoomed in.
        public const int VisibleRows = 5;

        // Pixels per cell. Cells are always square; CellWidth and CellHeight are the same
        // number, kept as two names because drawing code reads better with them.
        public static int CellWidth  { get; private set; } = 1;
        public static int CellHeight { get; private set; } = 1;
        public static int CellSize => Math.Min(CellWidth, CellHeight);

        public static void Update(int viewportHeight)
        {
            if (viewportHeight <= 0) return;   // guard against divide-by-zero
            CellWidth = CellHeight = Math.Max(1, viewportHeight / VisibleRows);
        }

        // ------------------------------------------------------------ world <-> screen

        /// <summary>A world position (cells) as screen pixels, before the camera is applied.</summary>
        public static Vector2 ToScreen(Vector2 world) => world * CellSize;

        /// <summary>
        /// A world rectangle (cells) as a pixel Rectangle for drawing. Each edge is rounded on
        /// its own, so two rectangles that touch in the world still touch on screen.
        /// </summary>
        public static Rectangle ToScreen(RectF world)
        {
            int left = Px(world.Left), top = Px(world.Top);
            return new Rectangle(left, top, Px(world.Right) - left, Px(world.Bottom) - top);
        }

        /// <summary>Screen pixels (camera already undone) as a world position in cells.</summary>
        public static Vector2 ToWorld(Vector2 screen) => screen / CellSize;

        private static int Px(float cells) => (int)MathF.Round(cells * CellSize);
    }
}
