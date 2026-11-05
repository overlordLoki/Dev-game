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
        public List<NpcSpawnData> npcs { get; set; }  // optional; who the level starts with, spawned on the first visit
    }

    /// <summary>
    /// One NPC a level starts with, spawned the first time it's entered (NpcRoster.Populate).
    /// Only <see cref="type"/> and <see cref="at"/> are needed; the rest fall back to the
    /// type's defaults (NpcFactory).
    /// </summary>
    public class NpcSpawnData
    {
        public string type { get; set; }              // NpcFactory's type: "Knight", "Bandit"
        public string name { get; set; }              // optional; the type's default name if missing
        public string at { get; set; }                // the point it stands on ("default" if missing or unknown)
        public List<string> factions { get; set; }    // optional; the type's default factions if missing
        public List<SpawnTaskData> tasks { get; set; } // optional; its first errands, in order
    }

    /// <summary>
    /// A starting errand, in the decision schema's words: go_to a point (target), wait
    /// (seconds), or patrol points (targets, pausing seconds at each). A point the level
    /// doesn't have is skipped, like a decision's.
    /// </summary>
    public class SpawnTaskData
    {
        public string type { get; set; }
        public string target { get; set; }
        public float seconds { get; set; }
        public List<string> targets { get; set; }
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
