using System.Collections.Generic;

namespace Ashenveil.Core.Levels
{
    /// <summary>
    /// Mirrors the level JSON produced by the level editor. Property names must
    /// match the JSON keys exactly so System.Text.Json can fill them in.
    /// </summary>
    public class MapData
    {
        public int cols { get; set; }
        public int rows { get; set; }
        public string[][] tiles { get; set; }        // jagged: tiles[row][col] = tile id
        public int[][] rotations { get; set; }        // optional, parallel to tiles; degrees per cell
        public List<ObjectData> objects { get; set; }
        public List<ExitData> exits { get; set; }     // optional; doorways to other levels
    }

    public class ObjectData
    {
        public string type { get; set; }
        public int variety { get; set; }
        public int col { get; set; }
        public int row { get; set; }
    }

    /// <summary>
    /// A doorway: stand on (col, row) and the level swaps to <see cref="to"/>, landing
    /// the player on (spawnCol, spawnRow) there. Deliberately a tile coordinate rather
    /// than an object, so a door needs no sprite of its own - it is the walkable cell
    /// in front of whatever building art is already standing there. The destination
    /// level normally carries the matching exit back.
    /// </summary>
    public class ExitData
    {
        public int col { get; set; }
        public int row { get; set; }
        public string to { get; set; }
        public int spawnCol { get; set; }
        public int spawnRow { get; set; }

        /// <summary>
        /// Which edge of the cell the door faces, clockwise from the top: 0 top,
        /// 90 right, 180 bottom, 270 left. The editor draws a bar on that side so a
        /// door reads as a threshold rather than a highlighted square. Nothing in the
        /// game uses it yet - it is here so the field survives a load/save round trip
        /// instead of being dropped the way object rotation is.
        /// </summary>
        public int rotation { get; set; }
    }
}
