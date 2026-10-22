using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// A straight step costs 10 and a diagonal 14, so every expected number here can be
/// worked out on paper: go diagonally min(dx, dy) times, then straight for the rest.
/// </summary>
public class PathfinderTests
{
    // --------------------------------------------------------------- Heuristic

    [Theory]
    // from      to        cost   why
    [InlineData(0, 0,     0, 0,      0)]   // same cell
    [InlineData(0, 0,     3, 0,     30)]   // 3 straight across
    [InlineData(0, 0,     0, 4,     40)]   // 4 straight down
    [InlineData(0, 0,     3, 3,     42)]   // 3 diagonal (3 x 14)
    [InlineData(0, 0,     5, 2,     58)]   // 2 diagonal + 3 straight (28 + 30)
    [InlineData(5, 2,     0, 0,     58)]   // backwards is the same distance
    [InlineData(-2, -1,   3, 1,     58)]   // negative cells work too (dx 5, dy 2)
    public void Heuristic_is_the_octile_distance(int ax, int ay, int bx, int by, int expected)
    {
        int cost = Pathfinder.Heuristic(new Point(ax, ay), new Point(bx, by));

        Assert.Equal(expected, cost);
    }

    // ---------------------------------------------------------------- FindPath

    // A grid drawn as text: '#' is blocked, anything else is walkable.
    private static NavGrid Grid(params string[] rows)
    {
        var grid = new NavGrid(rows[0].Length, rows.Length);
        for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < rows[row].Length; col++)
                if (rows[row][col] == '#')
                    grid.Block(new Point(col, row));
        return grid;
    }

    [Fact]
    public void Start_equals_goal_is_just_that_cell()
    {
        var path = Pathfinder.FindPath(Grid("..."), new Point(1, 0), new Point(1, 0));

        Assert.Equal(new[] { new Point(1, 0) }, path);
    }

    [Fact]
    public void Open_grid_goes_straight_diagonally()
    {
        var path = Pathfinder.FindPath(Grid("....", "....", "....", "...."), new Point(0, 0), new Point(3, 3));

        Assert.Equal(new[] { new Point(0, 0), new Point(1, 1), new Point(2, 2), new Point(3, 3) }, path);
    }

    [Fact]
    public void Wall_with_a_gap_goes_through_the_gap()
    {
        var grid = Grid(
            "..#..",
            "..#..",
            ".....");   // the gap is (2, 2)

        var path = Pathfinder.FindPath(grid, new Point(0, 0), new Point(4, 0));

        Assert.Contains(new Point(2, 2), path);
        Assert.Equal(new Point(4, 0), path[^1]);
    }

    [Fact]
    public void Diagonals_dont_cut_a_blocked_corner()
    {
        var grid = Grid(
            ".#",
            "..");

        var path = Pathfinder.FindPath(grid, new Point(0, 0), new Point(1, 1));

        // Not the diagonal (0,0) -> (1,1): it would clip the blocked (1,0).
        Assert.Equal(new[] { new Point(0, 0), new Point(0, 1), new Point(1, 1) }, path);
    }

    [Fact]
    public void Walled_off_goal_is_null()
    {
        var grid = Grid(
            ".....",
            "..###",
            "..#..",
            "..###");

        Assert.Null(Pathfinder.FindPath(grid, new Point(0, 0), new Point(3, 2)));
    }

    [Fact]
    public void Blocked_goal_ends_next_to_it()
    {
        var path = Pathfinder.FindPath(Grid("...#"), new Point(0, 0), new Point(3, 0));

        Assert.Equal(new[] { new Point(0, 0), new Point(1, 0), new Point(2, 0) }, path);
    }

    [Fact]
    public void Already_next_to_a_blocked_goal_is_just_the_start()
    {
        var grid = Grid(
            "..",
            ".#");

        // (0, 0) is diagonally next to the blocked (1, 1), so there's nowhere to go.
        var path = Pathfinder.FindPath(grid, new Point(0, 0), new Point(1, 1));

        Assert.Equal(new[] { new Point(0, 0) }, path);
    }

    [Fact]
    public void Blocked_goal_ends_on_a_reachable_side_when_the_nearest_side_is_walled_off()
    {
        // T at (2, 3) is the blocked goal. Its left side (1, 3) is nearest to the start as
        // the crow flies, but it's in a sealed pocket; the path has to end at (3, 3).
        var grid = Grid(
            ".....",
            ".....",
            "####.",
            "..#..",
            ".##..");

        var path = Pathfinder.FindPath(grid, new Point(0, 0), new Point(2, 3));

        Assert.NotNull(path);
        Assert.Equal(new Point(3, 3), path[^1]);
    }
}
