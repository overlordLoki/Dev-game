using System.Collections.Generic;

namespace merder.Core.Levels
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
    }

    public class ObjectData
    {
        public string type { get; set; }
        public int variety { get; set; }
        public int col { get; set; }
        public int row { get; set; }
    }
}
