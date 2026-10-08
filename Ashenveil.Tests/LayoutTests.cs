using Ashenveil.Core;

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
