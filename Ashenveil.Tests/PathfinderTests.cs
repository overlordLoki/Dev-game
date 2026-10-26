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

    // ---------------------------------------------------------- CanWalkStraight

    [Fact]
    public void Can_walk_straight_across_an_open_grid()
    {
        var grid = Grid("....", "....", "....");

        Assert.True(Pathfinder.CanWalkStraight(grid, new Vector2(0.5f, 0.5f), new Vector2(3.5f, 2.5f)));
    }

    [Fact]
    public void Cant_walk_straight_through_a_blocked_cell()
    {
        var grid = Grid("..#..");

        Assert.False(Pathfinder.CanWalkStraight(grid, new Vector2(0.5f, 0.5f), new Vector2(4.5f, 0.5f)));
    }

    [Fact]
    public void Cant_squeeze_diagonally_between_two_blocked_corners()
    {
        var grid = Grid(
            ".#",
            "#.");

        Assert.False(Pathfinder.CanWalkStraight(grid, new Vector2(0.5f, 0.5f), new Vector2(1.5f, 1.5f)));
    }

    [Fact]
    public void Cant_skim_the_edge_of_a_blocked_cell()
    {
        // The centre line runs along row 0, only 0.1 above the blocked cell below it.
        var grid = Grid(
            "...",
            ".#.");

        Assert.False(Pathfinder.CanWalkStraight(grid, new Vector2(0.5f, 0.9f), new Vector2(2.5f, 0.9f)));
    }

    // ------------------------------------------------------------------- Smooth

    private static List<Vector2> Centres(List<Point> path) => path.ConvertAll(NavGrid.CentreOf);

    [Fact]
    public void A_path_across_an_open_grid_smooths_to_just_its_ends()
    {
        var grid = Grid("......", "......", "......");
        var path = Pathfinder.FindPath(grid, new Point(0, 0), new Point(5, 2));   // diagonal, then straight

        var smoothed = Pathfinder.Smooth(grid, Centres(path));

        Assert.Equal(new[] { new Vector2(0.5f, 0.5f), new Vector2(5.5f, 2.5f) }, smoothed);
    }

    [Fact]
    public void Smoothing_keeps_the_corner_round_a_wall()
    {
        var grid = Grid(
            "....",
            "###.",
            "....");
        var path = Pathfinder.FindPath(grid, new Point(0, 0), new Point(0, 2));

        var smoothed = Pathfinder.Smooth(grid, Centres(path));

        // Round the end of the wall, so more than just the two ends...
        Assert.True(smoothed.Count > 2);
        // ...and every straight run in it really can be walked.
        for (int i = 1; i < smoothed.Count; i++)
            Assert.True(Pathfinder.CanWalkStraight(grid, smoothed[i - 1], smoothed[i]));
    }

    [Fact]
    public void Smoothing_a_short_path_keeps_it_as_it_is()
    {
        var grid = Grid("..");
        var one = new List<Vector2> { new(0.5f, 0.5f) };
        var two = new List<Vector2> { new(0.5f, 0.5f), new(1.5f, 0.5f) };

        Assert.Equal(one, Pathfinder.Smooth(grid, one));
        Assert.Equal(two, Pathfinder.Smooth(grid, two));
    }
}
