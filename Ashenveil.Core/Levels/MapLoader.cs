using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Ashenveil.Core.Objects;
using Ashenveil.Core.Tiles;

namespace Ashenveil.Core.Levels
{
    // The game-ready result of loading a level file.
    public class LoadedMap
    {
        public TileType[,] Grid;
        public int[,] Rotations;
        public List<IObject> Objects;
    }

    public static class MapLoader
    {
        public static LoadedMap Load(string name)
        {
            // AppContext.BaseDirectory = the exe's folder, so this works regardless
            // of the current working directory.
            string path = Path.Combine(AppContext.BaseDirectory, "Content", "levels", name + ".json");
            string text = File.ReadAllText(path);
            MapData data = JsonSerializer.Deserialize<MapData>(text);

            // tiles: string names -> TileType[,]
            var grid = new TileType[data.rows, data.cols];
            for (int y = 0; y < data.rows; y++)
                for (int x = 0; x < data.cols; x++)
                    grid[y, x] = ParseTile(data.tiles[y][x]);

            // rotations: optional. If the file has none, everything stays at 0.
            var rotations = new int[data.rows, data.cols];
            if (data.rotations != null)
                for (int y = 0; y < data.rows; y++)
                    for (int x = 0; x < data.cols; x++)
                        rotations[y, x] = data.rotations[y][x];

            // objects: data -> real object instances
            var objects = new List<IObject>();
            if (data.objects != null)
            {
                foreach (var o in data.objects)
                {
                    switch (o.type)
                    {
                        case "tree":
                            objects.Add(new Tree(o.col, o.row, o.variety));
                            break;
                        // add new object types here as the game grows
                    }
                }
            }

            return new LoadedMap { Grid = grid, Rotations = rotations, Objects = objects };
        }

        // JSON tile name -> TileType. Must match the ids used in the level editor.
        private static TileType ParseTile(string id) => id switch
        {
            "grass"      => TileType.Grass,
            "dirt"       => TileType.Dirt,
            "dirt_road"  => TileType.DirtRoad,
            "dirt_cross" => TileType.DirtRoadCross,
            _            => TileType.Grass,   // unknown -> grass fallback
        };
    }
}
