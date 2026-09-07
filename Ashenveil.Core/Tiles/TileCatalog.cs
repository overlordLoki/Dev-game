using System;
using System.Linq;

namespace Ashenveil.Core.Tiles
{
    public enum TileType { Grass, Dirt, DirtRoad, DirtRoadCross , TurnDirtRoad , BendDirtRoad}
    /// <summary>
    /// One tile: the id it has in level JSON, its enum value, and its art.
    ///
    /// The id is deliberately not the enum name. It is a saved file format - levels
    /// on disk contain "dirt_cross", so renaming TileType.DirtRoadCross must not
    /// invalidate every level ever saved.
    /// </summary>
    public readonly record struct TileDef(string Id, TileType Type, string Asset);

    /// <summary>
    /// The single place a tile is declared. MapLoader and TileMap both read from here,
    /// so adding a tile is one member on TileType and one line below - not four edits
    /// spread across the codebase, three of which fail silently when forgotten.
    /// </summary>
    public static class TileCatalog
    {
        public static readonly TileDef[] All =
        {
            new("grass",      TileType.Grass,         Assets.Grass),
            new("dirt",       TileType.Dirt,          Assets.Dirt),
            new("dirt_road",  TileType.DirtRoad,      Assets.DirtRoad),
            new("dirt_cross", TileType.DirtRoadCross, Assets.DirtRoadCross),
            new("dirt_turn",  TileType.TurnDirtRoad,  Assets.TurnDirtRoad),
            new("dirt_bend",  TileType.BendDirtRoad,  Assets.BendDirtRoad),
        };

        /// <summary>
        /// Every TileType must appear in All. Without this a forgotten entry just makes
        /// the tile behave like grass forever; with it, the game refuses to start and
        /// says which one is missing.
        /// </summary>
        static TileCatalog()
        {
            foreach (TileType type in Enum.GetValues<TileType>())
                if (!All.Any(t => t.Type == type))
                    throw new InvalidOperationException(
                        $"TileType.{type} has no entry in TileCatalog.All - add its id and art there.");

            var duplicate = All.GroupBy(t => t.Id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException(
                    $"TileCatalog.All uses the id '{duplicate.Key}' more than once.");
        }

        /// <summary>JSON id -> tile. Unknown ids fall back to grass, as before.</summary>
        public static TileType Parse(string id)
        {
            foreach (var def in All)
                if (def.Id == id) return def.Type;
            return TileType.Grass;
        }

        /// <summary>Tile -> the id to write into level JSON.</summary>
        public static string IdFor(TileType type) => Find(type).Id;

        /// <summary>Tile -> its content asset path.</summary>
        public static string AssetFor(TileType type) => Find(type).Asset;

        private static TileDef Find(TileType type)
        {
            foreach (var def in All)
                if (def.Type == type) return def;

            // Unreachable while the static constructor above holds.
            throw new InvalidOperationException($"TileType.{type} is not in TileCatalog.All.");
        }
    }
}
