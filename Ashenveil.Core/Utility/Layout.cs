using System;

namespace Ashenveil.Core
{
    /// <summary>
    /// Single source of truth for tile-cell size. Derived from the current
    /// window (viewport) and the active grid's dimensions. Everything that
    /// sits on the ground — tiles, player, NPCs, objects — reads its size
    /// from here so they all share one scale.
    /// </summary>
    public static class Layout
    {
        public static int CellWidth  { get; private set; } = 1;
        public static int CellHeight { get; private set; } = 1;

        // Convenience for square things (player/objects): the smaller of the two,
        // so a square sprite always fits inside a cell without stretching.
        public static int CellSize => Math.Min(CellWidth, CellHeight);

        public static void Update(int viewportWidth, int viewportHeight, int cols, int rows)
        {
            if (cols <= 0 || rows <= 0) return;   // guard against divide-by-zero
            CellWidth  = viewportWidth  / cols;
            CellHeight = viewportHeight / rows;
        }
    }
}
