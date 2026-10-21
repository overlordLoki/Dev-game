using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Levels;
using Ashenveil.Core.Objects;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Tiles;
using Ashenveil.Core.Graphics;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld.Locations
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

        // How far outside an exit cell still counts as touching it, in pixels. Collision leaves
        // the body flush against a solid door, so this is leeway, not reach.
        private const int ExitLeeway = 4;

        /// <summary>
        /// The doorway this body is touching, or null. A door is usually set into something solid
        /// (Medows' castle door is on the castle's own cell), so collision never lets the feet
        /// stand *in* the exit cell - pressed up against it has to count. Checks every cell the
        /// body overlaps, give or take <see cref="ExitLeeway"/> pixels.
        /// </summary>
        public ExitData ExitTouching(Rectangle body)
        {
            body.Inflate(ExitLeeway, ExitLeeway);
            int w = Layout.CellWidth, h = Layout.CellHeight;

            for (int row = body.Top / h; row <= (body.Bottom - 1) / h; row++)
                for (int col = body.Left / w; col <= (body.Right - 1) / w; col++)
                    if (ExitAt(col, row) is { } exit) return exit;
            return null;
        }

        /// <summary>The named point with this name, or null if the level has none (or no name is given).</summary>
        public PointOfInterest Poi(string name) =>
            name != null && pointsOfInterest.TryGetValue(name, out var p) ? p : null;

        /// <summary>
        /// Stands an entity centred on the named point - or on "default" if this level doesn't
        /// have one by that name, so a missing spawn point never leaves something at (0, 0).
        /// Centred rather than corner-aligned, so it doesn't start half inside the next cell.
        /// </summary>
        public void PlaceAt(Entity entity, string poiName)
        {
            PointOfInterest poi = Poi(poiName) ?? Poi("default");
            entity.Position = poi.ToPixel() - new Vector2(entity.Width / 2f, entity.Height / 2f);
        }
    }
}
