using Ashenveil.Core;
using Ashenveil.Core.Collision;
using Ashenveil.Core.GameWorld.Locations;
using Microsoft.Xna.Framework;
using Ashenveil.Core.Graphics;

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
            { "col": 3, "row": 2, "to": "Medows",  "arriveAt": "entry" },
            { "col": 0, "row": 1, "to": "Castle2" }
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
        Assert.Equal("entry", exit.arriveAt);
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

    // ---------------------------------------------------------------- ExitTouching
    // In cells: the Castle2 exit at (0,1) covers x 0..1, y 1..2.

    [Fact]
    public void ExitTouching_finds_a_body_standing_in_the_exit_cell()
    {
        var exit = TestLevels.Location(Level).ExitTouching(new RectF(0.3f, 1.3f, 0.4f, 0.4f));

        Assert.Equal("Castle2", exit?.to);
    }

    [Fact]
    public void ExitTouching_counts_a_body_pressed_against_the_exit_from_outside()
    {
        // The feet can't get into a door set in something solid: the body stops flush below it.
        var exit = TestLevels.Location(Level).ExitTouching(new RectF(0.3f, 2.02f, 0.4f, 0.2f));   // 0.02 below

        Assert.Equal("Castle2", exit?.to);
    }

    [Fact]
    public void ExitTouching_counts_a_body_that_only_overlaps_the_exit_cell_a_little()
    {
        // Mostly in the cell below, its top edge just inside the exit cell.
        var exit = TestLevels.Location(Level).ExitTouching(new RectF(0.3f, 1.9f, 0.4f, 0.4f));

        Assert.Equal("Castle2", exit?.to);
    }

    [Fact]
    public void ExitTouching_is_null_a_little_further_away()
    {
        Assert.Null(TestLevels.Location(Level).ExitTouching(new RectF(0.3f, 2.1f, 0.4f, 0.2f)));   // 0.1 below
    }

    [Fact]
    public void ExitTouching_is_null_in_a_level_with_no_exits()
    {
        Assert.Null(TestLevels.Location(BareLevel).ExitTouching(new RectF(0, 0, 1, 1)));
    }

    // --------------------------------------------------------- PointOfInterest.ToWorld / StandingSpot

    [Fact]
    public void StandingSpot_is_centred_across_the_cell_and_near_its_bottom()
    {
        Near.Equal(new Vector2(2.5f, 3.9f), new PointOfInterest("p", 2, 3).StandingSpot());
    }

    [Fact]
    public void Feet_on_a_standing_spot_dont_reach_a_door_in_the_cell_below()
    {
        // Standing on cell (0,0), right above the Castle2 door at (0,1). Feet at y = 0.9 plus the
        // 0.03 door leeway is still short of the door's top edge (y = 1) - which is why the
        // spot isn't the very bottom of the cell.
        var feetOnSpot   = new RectF(0.3f, 0.5f, 0.4f, PointOfInterest.FeetDown - 0.5f);   // bottom at 0.9
        var feetOnTheEdge = new RectF(0.3f, 0.5f, 0.4f, 0.5f);                            // bottom at 1.0

        Assert.Null(TestLevels.Location(Level).ExitTouching(feetOnSpot));
        Assert.Equal("Castle2", TestLevels.Location(Level).ExitTouching(feetOnTheEdge)?.to);
    }

    [Theory]
    [InlineData(0, 0, 0.5f, 0.5f)]
    [InlineData(2, 3, 2.5f, 3.5f)]
    [InlineData(7, 0, 7.5f, 0.5f)]
    public void ToWorld_is_the_centre_of_the_cell(int col, int row, float x, float y)
    {
        Assert.Equal(new Vector2(x, y), new PointOfInterest("p", col, row).ToWorld());
    }

    [Fact]
    public void ToWorld_does_not_change_when_the_window_resizes()
    {
        // The old pixel version doubled when the window did; a world position mustn't move.
        var poi = new PointOfInterest("p", 2, 3);

        Layout.Update(500);
        var small = poi.ToWorld();
        Layout.Update(1000);
        var large = poi.ToWorld();

        Assert.Equal(small, large);
    }

    // --------------------------------------------------------------------- PlaceAt

    [Fact]
    public void PlaceAt_stands_an_entity_with_its_feet_on_the_named_point()
    {
        var entity = new TestEntity(0, 0);                    // 1x1 cell, box = whole sprite

        TestLevels.Location(Level).PlaceAt(entity, "spawn");  // cell (2,1) -> standing spot (2.5, 1.9)

        Near.Equal(new Vector2(2.5f, 1.9f), entity.Feet);
        Near.Equal(new Vector2(2, 0.9f), entity.Position);    // so the body mostly fills its own cell
    }

    // Its box (Content/bounds.json) spans y 25%..75% of the sprite: a quarter of padding below
    // the feet, like a real sprite (the knight's frame is mostly padding above his body).
    private class PaddedEntity : Ashenveil.Core.Entities.Entity
    {
        public PaddedEntity() : base(Vector2.Zero, 0) { }
        protected override float SizeInCells => 1f;
        protected override string BoundsKey => "TestPaddedEntity";
    }

    [Fact]
    public void PlaceAt_puts_a_padded_sprites_feet_on_the_point_not_its_frame_centre()
    {
        var entity = new PaddedEntity();

        TestLevels.Location(Level).PlaceAt(entity, "spawn");   // cell (2,1) -> standing spot (2.5, 1.9)

        Near.Equal(new Vector2(2.5f, 1.9f), entity.Feet);
        // The feet are 0.75 down the frame, so the frame starts 0.75 above them. Centring the
        // frame on the spot instead (an older rule) would have put the feet 0.25 lower.
        Near.Equal(new Vector2(2, 1.15f), entity.Position);
    }

    [Fact]
    public void PlaceAt_an_unknown_point_falls_back_to_default()
    {
        var entity = new TestEntity(0, 0);

        TestLevels.Location(Level).PlaceAt(entity, "nowhere");  // "default" is cell (1,1) -> standing spot (1.5, 1.9)

        Near.Equal(new Vector2(1.5f, 1.9f), entity.Feet);
    }

    [Fact]
    public void PlaceAt_with_no_name_falls_back_to_default()
    {
        // An exit with no arriveAt passes null; that must land on "default", not throw.
        var entity = new TestEntity(0, 0);

        TestLevels.Location(Level).PlaceAt(entity, null);

        Near.Equal(new Vector2(1.5f, 1.9f), entity.Feet);
    }
}
