using System.Collections.Generic;
using Ashenveil.Core.GameWorld.Locations;

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
        public List<PoiData> pois { get; set; }       // optional; named spawn points (matches the editor's "pois")
    }

    /// <summary>
    /// A named point on the grid — a spawn cell, an NPC's start. x/y are grid cells
    /// (col, row), not pixels. A plain DTO like ObjectData/ExitData; the loader maps
    /// it to the domain <see cref="PointOfInterest"/>.
    /// </summary>
    public class PoiData
    {
        public string name { get; set; }
        public int x { get; set; }   // column
        public int y { get; set; }   // row
    }

    public class ObjectData
    {
        public string type { get; set; }
        public int variety { get; set; }
        public int col { get; set; }
        public int row { get; set; }
    }

    /// <summary>
    /// A doorway: touch (col, row) and press E, and the level swaps to <see cref="to"/>,
    /// landing the player on the point named <see cref="arriveAt"/> there. Deliberately a
    /// tile coordinate rather than an object, so a door needs no sprite of its own - it can
    /// sit on the building's own cell (see Location.ExitTouching). The destination level
    /// normally carries the matching exit back.
    /// </summary>
    public class ExitData
    {
        public int col { get; set; }
        public int row { get; set; }
        public string to { get; set; }

        /// <summary>
        /// The point of interest in <see cref="to"/> the player arrives on - a name, not a cell,
        /// so each level owns its own layout: move the destination's door and its arrival point
        /// moves with it, with no other level to fix. Missing or unknown -> the destination's
        /// "default" point (Location.PlaceAt). Replaced spawnCol/spawnRow, which hard-coded a
        /// cell of the *destination* into this level's file.
        /// </summary>
        public string arriveAt { get; set; }

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
