using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Levels;
using Ashenveil.Core.Objects;
using Ashenveil.Core.Screens.Locations;
using Ashenveil.Core.Tiles;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// One playable area, built straight from its level file. This used to be an
    /// interface with a hand-written class per map; every new room would have meant
    /// another near-empty .cs file, so the name of the level is now just a constructor
    /// argument and the data does the rest.
    /// </summary>
    public class Location
    {
        public string Name { get; }
        public TileMap tileMap { get; }
        public List<IObject> objects { get; }
        public List<ExitData> exits { get; }
        public Dictionary<string, PointOfInterest> pointsOfInterest { get; }

        public Location(string name)
        {
            Name = name;
            var loaded = MapLoader.Load(name);   // reads Content/levels/{name}.json
            tileMap = new TileMap(loaded.Grid, loaded.Rotations);
            objects = loaded.Objects;
            exits = loaded.Exits;
            pointsOfInterest = loaded.PointsOfInterest;
            //add a default spawn point for fallback
            PointOfInterest defaultSpawn = new PointOfInterest("default", 1, 1);
            pointsOfInterest["default"] = defaultSpawn;
        }

        /// <summary>The doorway on this cell, or null if it's an ordinary tile.</summary>
        public ExitData ExitAt(int col, int row) =>
            exits.FirstOrDefault(e => e.col == col && e.row == row);

        /// <summary>The named point with this name, or null if the level has none.</summary>
        public PointOfInterest Poi(string name) =>
            pointsOfInterest.TryGetValue(name, out var p) ? p : null;
    }
}
