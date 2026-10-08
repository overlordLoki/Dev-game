using Ashenveil.Core;
using Ashenveil.Core.Screens.Locations;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

public class LocationTests
{
    private const string Level = """
        {
          "cols": 4,
          "rows": 3,
          "tiles": [
            ["grass", "grass", "grass", "grass"],
            ["grass", "grass", "grass", "grass"],
            ["grass", "grass", "grass", "grass"]
          ],
          "exits": [
            { "col": 3, "row": 2, "to": "Medows",  "spawnCol": 5, "spawnRow": 6 },
            { "col": 0, "row": 1, "to": "Castle2", "spawnCol": 1, "spawnRow": 1 }
          ],
          "pois": [
            { "name": "spawn", "x": 2, "y": 1 },
            { "name": "shop",  "x": 3, "y": 0 }
          ]
        }
        """;

    private const string BareLevel = """
        { "cols": 1, "rows": 1, "tiles": [["grass"]] }
        """;

    // ------------------------------------------------------------------------- Poi

    [Fact]
    public void Poi_finds_a_named_point()
    {
        var shop = TestLevels.Location(Level).Poi("shop");

        Assert.NotNull(shop);
        Assert.Equal("shop", shop.Name);
        Assert.Equal((3, 0), (shop.X, shop.Y));
    }

    [Fact]
    public void Poi_default_exists_even_when_the_level_declares_no_points()
    {
        var fallback = TestLevels.Location(BareLevel).Poi("default");

        Assert.NotNull(fallback);
        Assert.Equal((1, 1), (fallback.X, fallback.Y));
    }

    [Fact]
    public void Poi_default_sits_alongside_the_levels_own_points()
    {
        var location = TestLevels.Location(Level);

        Assert.NotNull(location.Poi("default"));
        Assert.NotNull(location.Poi("spawn"));
        Assert.Equal(3, location.pointsOfInterest.Count);
    }

    [Theory]
    [InlineData("nowhere")]
    [InlineData("Shop")]   // names are case-sensitive
    [InlineData("")]
    public void Poi_unknown_name_is_null_not_the_default(string name)
    {
        Assert.Null(TestLevels.Location(Level).Poi(name));
    }

    // ---------------------------------------------------------------------- ExitAt

    [Fact]
    public void ExitAt_returns_the_doorway_on_that_cell()
    {
        var exit = TestLevels.Location(Level).ExitAt(3, 2);

        Assert.NotNull(exit);
        Assert.Equal("Medows", exit.to);
        Assert.Equal((5, 6), (exit.spawnCol, exit.spawnRow));
    }

    [Fact]
    public void ExitAt_tells_two_doorways_apart()
    {
        var location = TestLevels.Location(Level);

        Assert.Equal("Medows", location.ExitAt(3, 2).to);
        Assert.Equal("Castle2", location.ExitAt(0, 1).to);
    }

    [Theory]
    [InlineData(2, 3)]    // col and row swapped
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(99, 99)]
    public void ExitAt_is_null_on_an_ordinary_cell(int col, int row)
    {
        Assert.Null(TestLevels.Location(Level).ExitAt(col, row));
    }

    [Fact]
    public void ExitAt_is_null_in_a_level_with_no_exits()
    {
        Assert.Null(TestLevels.Location(BareLevel).ExitAt(0, 0));
    }

    // --------------------------------------------------------- PointOfInterest.ToPixel

    [Theory]
    [InlineData(0, 0,  50f,  50f)]
    [InlineData(2, 3, 250f, 350f)]
    [InlineData(7, 0, 750f,  50f)]
    public void ToPixel_is_the_centre_of_the_cell(int col, int row, float x, float y)
    {
        Layout.Update(500);   // 100px cells

        Assert.Equal(new Vector2(x, y), new PointOfInterest("p", col, row).ToPixel());
    }

    [Fact]
    public void ToPixel_keeps_the_half_pixel_on_an_odd_sized_cell()
    {
        Layout.Update(505);   // 101px cells

        Assert.Equal(new Vector2(151.5f, 50.5f), new PointOfInterest("p", 1, 0).ToPixel());
    }

    [Fact]
    public void ToPixel_follows_the_cell_size_when_the_window_resizes()
    {
        var poi = new PointOfInterest("p", 2, 3);

        Layout.Update(500);
        var small = poi.ToPixel();
        Layout.Update(1000);
        var large = poi.ToPixel();

        Assert.Equal(small * 2, large);
    }
}
