using Ashenveil.Core;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Graphics;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

public class LayoutTests
{
    [Theory]
    [InlineData( 500, 100)]
    [InlineData( 720, 144)]   // 720p
    [InlineData(1080, 216)]   // 1080p
    [InlineData( 504, 100)]   // integer division: the remainder is dropped, not rounded
    [InlineData(   5,   1)]
    public void Update_sizes_a_cell_to_fit_VisibleRows_in_the_viewport(int viewportHeight, int expected)
    {
        Layout.Update(viewportHeight);

        Assert.Equal(expected, Layout.CellSize);
        Assert.Equal(expected, Layout.CellWidth);
        Assert.Equal(expected, Layout.CellHeight);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]   // fewer pixels than rows would be a 0px cell
    public void Update_never_makes_a_cell_smaller_than_one_pixel(int viewportHeight)
    {
        Layout.Update(viewportHeight);

        Assert.Equal(1, Layout.CellSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-720)]
    public void Update_ignores_a_viewport_with_no_height(int viewportHeight)
    {
        // A minimised window reports 0; the last good size has to survive it.
        Layout.Update(500);

        Layout.Update(viewportHeight);

        Assert.Equal(100, Layout.CellSize);
    }

    // ------------------------------------------------------------ world <-> screen

    [Theory]
    [InlineData(500)]
    [InlineData(720)]
    [InlineData(1080)]
    public void ToWorld_undoes_ToScreen_at_any_cell_size(int viewportHeight)
    {
        Layout.Update(viewportHeight);
        var world = new Vector2(5.5f, 3.25f);

        Assert.Equal(world, Layout.ToWorld(Layout.ToScreen(world)));
    }

    [Fact]
    public void ToScreen_scales_a_position_by_the_cell_size()
    {
        Layout.Update(720);   // 144px cells

        Assert.Equal(new Vector2(792, 468), Layout.ToScreen(new Vector2(5.5f, 3.25f)));
    }

    [Fact]
    public void ToScreen_of_a_rectangle_rounds_each_edge_to_a_whole_pixel()
    {
        Layout.Update(720);   // 144px cells

        // x 0.3..1.3 -> 43.2..187.2 px -> 43..187; y 2..2.5 -> 288..360.
        Assert.Equal(new Rectangle(43, 288, 144, 72), Layout.ToScreen(new RectF(0.3f, 2, 1, 0.5f)));
    }

    [Fact]
    public void Rectangles_that_touch_in_the_world_still_touch_on_screen()
    {
        // Rounding each edge (not x and width separately) means no 1px gaps or overlaps.
        Layout.Update(720);

        var left  = Layout.ToScreen(new RectF(0.3f, 0, 0.7f, 1));
        var right = Layout.ToScreen(new RectF(1.0f, 0, 0.45f, 1));

        Assert.Equal(left.Right, right.Left);
    }

    [Fact]
    public void A_resize_changes_how_big_things_are_drawn_not_where_they_are()
    {
        var world = new Vector2(2, 3);

        Layout.Update(500);
        var small = Layout.ToScreen(world);
        Layout.Update(1000);
        var large = Layout.ToScreen(world);

        Assert.Equal(small * 2, large);   // the same place, drawn twice as big
    }

    [Fact]
    public void VisibleRows_cells_never_overflow_the_viewport()
    {
        for (int height = 1; height <= 2000; height++)
        {
            Layout.Update(height);
            if (height >= Layout.VisibleRows)
                Assert.True(Layout.CellHeight * Layout.VisibleRows <= height, $"height {height}");
        }
    }
}
