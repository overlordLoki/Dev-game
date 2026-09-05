using System.Collections.Generic;
using Ashenveil.Core.Levels;
using Ashenveil.Core.Objects;
using Ashenveil.Core.Tiles;

namespace Ashenveil.Core.Screens{
    public class Medows : Location
    {
        public TileMap tileMap { get; set; }
        public List<IObject> objects { get; set; } = new();

        public Medows()
        {
            var loaded = MapLoader.Load("Medows");   // reads Content/levels/Medows.json
            tileMap = new TileMap(loaded.Grid, loaded.Rotations);
            objects = loaded.Objects;
        }
    }
}