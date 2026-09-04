using System.Collections.Generic;
using merder.Core.Levels;
using merder.Core.Objects;
using merder.Core.Tiles;

namespace merder.Core.Screens{
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