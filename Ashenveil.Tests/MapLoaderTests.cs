using Ashenveil.Core.Levels;
using Ashenveil.Core.Objects;
using Ashenveil.Core.Tiles;

namespace Ashenveil.Tests;

public class MapLoaderTests
{
    // Written the way the editor writes it. If the editor's key names and MapData's
    // property names ever drift apart, these are the tests that notice.
    private const string FullLevel = """
        {
          "cols": 3,
          "rows": 2,
          "tiles": [
            ["grass",      "dirt",        "dirt_road"],
            ["dirt_cross", "not_a_tile",  "dirt_single"]
          ],
          "rotations": [
            [0,   90, 180],
            [270,  0,   0]
          ],
          "objects": [
            { "type": "tree",   "variety": 1, "col": 1, "row": 0 },
            { "type": "bush",   "variety": 3, "col": 2, "row": 1 },
            { "type": "dragon", "variety": 1, "col": 0, "row": 0 },
            { "type": "tree",   "variety": 9, "col": 0, "row": 0 }
          ],
          "exits": [
            { "col": 2, "row": 1, "to": "CastleInterior", "spawnCol": 4, "spawnRow": 5, "rotation": 180 }
          ],
          "pois": [
            { "name": "spawn", "x": 1, "y": 1 },
            { "name": "shop",  "x": 2, "y": 0 }
          ]
        }
        """;

    private const string BareLevel = """
        { "cols": 2, "rows": 1, "tiles": [["dirt", "grass"]] }
        """;

    // ----------------------------------------------------------------------- tiles

    [Fact]
    public void Grid_is_indexed_row_then_column()
    {
        var map = TestLevels.Load(FullLevel);

        Assert.Equal(2, map.Grid.GetLength(0));   // rows
        Assert.Equal(3, map.Grid.GetLength(1));   // cols
        Assert.Equal(TileType.Grass,          map.Grid[0, 0]);
        Assert.Equal(TileType.Dirt,           map.Grid[0, 1]);
        Assert.Equal(TileType.DirtRoad,       map.Grid[0, 2]);
        Assert.Equal(TileType.DirtRoadCross,  map.Grid[1, 0]);
        Assert.Equal(TileType.SingleDirtRoad, map.Grid[1, 2]);
    }

    [Fact]
    public void Unknown_tile_id_becomes_grass()
    {
        Assert.Equal(TileType.Grass, TestLevels.Load(FullLevel).Grid[1, 1]);
    }

    [Fact]
    public void Every_tile_id_in_the_catalog_survives_a_round_trip()
    {
        foreach (var def in TileCatalog.All)
        {
            var map = TestLevels.Load($$"""{ "cols": 1, "rows": 1, "tiles": [["{{def.Id}}"]] }""");

            Assert.Equal(def.Type, map.Grid[0, 0]);
            Assert.Equal(def.Id, TileCatalog.IdFor(def.Type));
        }
    }

    // ------------------------------------------------------------------- rotations

    [Fact]
    public void Rotations_are_read_per_cell()
    {
        var map = TestLevels.Load(FullLevel);

        Assert.Equal(new[,] { { 0, 90, 180 }, { 270, 0, 0 } }, map.Rotations);
    }

    [Fact]
    public void Missing_rotations_default_to_zero_for_the_whole_grid()
    {
        var map = TestLevels.Load(BareLevel);

        Assert.Equal(new[,] { { 0, 0 } }, map.Rotations);
    }

    // --------------------------------------------------------------------- objects

    [Fact]
    public void Objects_become_the_class_for_their_type_and_variety_on_their_cell()
    {
        var objects = TestLevels.Load(FullLevel).Objects;

        var tree = Assert.IsType<TreeSmall>(objects[0]);
        Assert.Equal((1, 0), (tree.Col, tree.Row));

        var bush = Assert.IsType<BushLarge>(objects[1]);
        Assert.Equal((2, 1), (bush.Col, bush.Row));
    }

    [Fact]
    public void Objects_with_an_unknown_type_or_variety_are_skipped()
    {
        // 4 rows in the file: the "dragon" and the variety-9 tree don't exist.
        Assert.Equal(2, TestLevels.Load(FullLevel).Objects.Count);
    }

    [Theory]
    [InlineData("tree",   1, typeof(TreeSmall))]
    [InlineData("tree",   2, typeof(TreeMedium))]
    [InlineData("tree",   3, typeof(TreeLarge))]
    [InlineData("bush",   1, typeof(BushSmall))]
    [InlineData("bush",   2, typeof(BushMedium))]
    [InlineData("bush",   3, typeof(BushLarge))]
    [InlineData("well",   1, typeof(Well))]
    [InlineData("castle", 1, typeof(Castle_Square))]
    public void Every_placeable_the_editor_can_write_is_built(string type, int variety, Type expected)
    {
        var map = TestLevels.Load($$"""
            {
              "cols": 1, "rows": 1, "tiles": [["grass"]],
              "objects": [{ "type": "{{type}}", "variety": {{variety}}, "col": 0, "row": 0 }]
            }
            """);

        Assert.IsType(expected, Assert.Single(map.Objects));
    }

    // ----------------------------------------------------------------------- exits

    [Fact]
    public void Exits_keep_every_field()
    {
        var exit = Assert.Single(TestLevels.Load(FullLevel).Exits);

        Assert.Equal((2, 1), (exit.col, exit.row));
        Assert.Equal("CastleInterior", exit.to);
        Assert.Equal((4, 5), (exit.spawnCol, exit.spawnRow));
        Assert.Equal(180, exit.rotation);
    }

    // ------------------------------------------------------------------------ pois

    [Fact]
    public void Pois_are_mapped_to_PointOfInterest_and_keyed_by_name()
    {
        var pois = TestLevels.Load(FullLevel).PointsOfInterest;

        Assert.Equal(2, pois.Count);
        Assert.Equal("spawn", pois["spawn"].Name);
        Assert.Equal((1, 1), (pois["spawn"].X, pois["spawn"].Y));
        Assert.Equal((2, 0), (pois["shop"].X, pois["shop"].Y));
    }

    [Fact]
    public void Pois_are_only_read_from_the_key_the_editor_writes()
    {
        // The bug that already bit once: same data under the wrong key loads as nothing.
        var map = TestLevels.Load("""
            {
              "cols": 1, "rows": 1, "tiles": [["grass"]],
              "pointsOfInterest": [{ "name": "spawn", "x": 0, "y": 0 }]
            }
            """);

        Assert.Empty(map.PointsOfInterest);
    }

    [Fact]
    public void Two_pois_with_the_same_name_keep_the_last_one()
    {
        var map = TestLevels.Load("""
            {
              "cols": 1, "rows": 1, "tiles": [["grass"]],
              "pois": [{ "name": "spawn", "x": 1, "y": 2 }, { "name": "spawn", "x": 3, "y": 4 }]
            }
            """);

        var spawn = Assert.Single(map.PointsOfInterest).Value;
        Assert.Equal((3, 4), (spawn.X, spawn.Y));
    }

    // ------------------------------------------------------------ optional sections

    [Fact]
    public void A_level_with_only_tiles_loads_with_empty_lists_not_nulls()
    {
        var map = TestLevels.Load(BareLevel);

        Assert.Empty(map.Objects);
        Assert.Empty(map.Exits);
        Assert.Empty(map.PointsOfInterest);
    }

    // ----------------------------------------------------------- the shipped levels

    [Fact]
    public void Every_shipped_level_loads()
    {
        NullContentManager.Install();
        var names = TestLevels.ShippedNames();
        Assert.NotEmpty(names);

        foreach (var name in names)
        {
            var map = MapLoader.Load(name);
            Assert.True(map.Grid.Length > 0, $"{name} has no tiles");
        }
    }

    [Fact]
    public void Every_exit_in_a_shipped_level_is_on_the_grid_and_leads_somewhere_real()
    {
        NullContentManager.Install();
        var levels = TestLevels.ShippedNames().ToDictionary(n => n, MapLoader.Load);

        foreach (var (name, map) in levels)
        {
            foreach (var exit in map.Exits)
            {
                string door = $"{name} exit at ({exit.col},{exit.row}) -> {exit.to}";

                Assert.True(InGrid(map, exit.col, exit.row), $"{door}: the doorway is off the grid");
                Assert.True(levels.ContainsKey(exit.to), $"{door}: no such level");
                Assert.True(InGrid(levels[exit.to], exit.spawnCol, exit.spawnRow),
                    $"{door}: spawn ({exit.spawnCol},{exit.spawnRow}) is off the destination grid");
            }
        }
    }

    [Fact]
    public void Every_poi_in_a_shipped_level_is_on_the_grid()
    {
        NullContentManager.Install();

        foreach (var name in TestLevels.ShippedNames())
        {
            var map = MapLoader.Load(name);
            foreach (var poi in map.PointsOfInterest.Values)
                Assert.True(InGrid(map, poi.X, poi.Y), $"{name} poi '{poi.Name}' at ({poi.X},{poi.Y}) is off the grid");
        }
    }

    private static bool InGrid(LoadedMap map, int col, int row) =>
        row >= 0 && row < map.Grid.GetLength(0) && col >= 0 && col < map.Grid.GetLength(1);
}
