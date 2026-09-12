using System;

namespace Ashenveil.Core
{
    /// <summary>
    /// Single source of truth for tile-cell size. Everything that sits on the
    /// ground — tiles, player, NPCs, objects — reads its size from here so they
    /// all share one scale.
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

        public static int CellWidth  { get; private set; } = 1;
        public static int CellHeight { get; private set; } = 1;

        // Convenience for square things (player/objects): the smaller of the two,
        // so a square sprite always fits inside a cell without stretching.
        public static int CellSize => Math.Min(CellWidth, CellHeight);

        public static void Update(int viewportHeight)
        {
            if (viewportHeight <= 0) return;   // guard against divide-by-zero
            CellWidth = CellHeight = Math.Max(1, viewportHeight / VisibleRows);
        }
    }
}
