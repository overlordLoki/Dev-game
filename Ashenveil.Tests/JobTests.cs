using Ashenveil.Core;
using Ashenveil.Core.Entities.brains.Jobs;
using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Tasks are pure logic: they read the owner's position/time and set Direction/State.
/// No movement happens here (Entity.Move does that during Update) and no graphics are
/// touched, so we can test the decision each task makes in isolation.
/// </summary>
public class GoToJobTests
{
    public GoToJobTests() => NullContentManager.Install();   // TestNpc construction needs Textures ready

    private static JobContext Ctx(TestNpc npc, float dt = 0f) =>
        new() { Owner = npc, Blackboard = new Blackboard(), DeltaTime = dt };

    [Fact]
    public void Heads_straight_at_a_target_to_the_right()
    {
        var npc = new TestNpc(0, 0);
        var task = new GoToJob(npc.Feet + new Vector2(1, 0));   // a cell right of the npc's feet

        task.Execute(Ctx(npc));

        Assert.Equal(new Vector2(1, 0), npc.Direction);          // unit heading toward target
        Assert.NotEqual(JobState.Completed, task.State);  // still travelling
    }

    [Fact]
    public void Heading_is_normalised_on_a_diagonal()
    {
        var npc = new TestNpc(0, 0);
        var task = new GoToJob(npc.Feet + new Vector2(3, 4));   // length 5 from the feet, outside the radius

        task.Execute(Ctx(npc));

        Assert.Equal(1f, npc.Direction.Length(), 3);  // unit length (speed is Entity's job, not direction's)
        Assert.True(npc.Direction.X > 0 && npc.Direction.Y > 0);
    }

    [Fact]
    public void Completes_and_stops_once_within_the_arrive_radius()
    {
        var npc = new TestNpc(0, 0);
        var task = new GoToJob(npc.Feet + new Vector2(0.02f, 0));   // 0.02 cells from the feet, inside the 0.03 radius

        task.Execute(Ctx(npc));

        Assert.Equal(JobState.Completed, task.State);
        Assert.Equal(Vector2.Zero, npc.Direction);    // stopped, not still steering
    }

    [Fact]
    public void Keeps_going_just_outside_the_arrive_radius()
    {
        var npc = new TestNpc(0, 0);
        var task = new GoToJob(npc.Feet + new Vector2(0.05f, 0));   // ~7px at 720p: not there yet

        task.Execute(Ctx(npc));

        Assert.NotEqual(JobState.Completed, task.State);
    }

    [Fact]
    public void Aims_the_feet_not_the_sprite_centre()
    {
        // A target level with the sprite's centre but half a cell below it is where the feet
        // are already (TestNpc is 1x1 with a full-frame box) - so it has arrived, not "go down".
        var npc = new TestNpc(0, 0);
        var task = new GoToJob(new Vector2(0.5f, 1));

        task.Execute(Ctx(npc));

        Assert.Equal(JobState.Completed, task.State);
    }

    // ------------------------------------------------------------ with a nav grid

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

    // Ticks the task until it completes, stepping the NPC 0.05 cells along Direction each
    // tick. There's no collision here, so only the route keeps the feet out of blocked
    // cells. Returns every cell the feet were in along the way.
    private static List<Point> Walk(TestNpc npc, GoToJob task, NavGrid nav)
    {
        var cells = new List<Point> { NavGrid.CellAt(npc.Feet) };
        for (int tick = 0; tick < 1000 && task.State != JobState.Completed; tick++)
        {
            task.Execute(new JobContext { Owner = npc, Blackboard = new Blackboard(), Nav = nav });
            npc.Position += npc.Direction * 0.05f;
            cells.Add(NavGrid.CellAt(npc.Feet));
        }
        return cells;
    }

    [Fact]
    public void Walks_round_a_tree_and_ends_on_the_exact_target()
    {
        var nav = Grid(
            ".....",
            "..#..",
            ".....");
        var npc = new TestNpc(0, 0);
        npc.StandAt(new Vector2(0.5f, 1.9f));        // feet low in cell (0, 1), like a POI's standing spot
        var target = new Vector2(4.5f, 1.9f);        // cell (4, 1): straight through the tree

        var cells = Walk(npc, new GoToJob(target), nav);

        Assert.DoesNotContain(new Point(2, 1), cells);
        Assert.True(Vector2.Distance(target, npc.Feet) <= 0.03f);   // within the arrive radius
    }

    [Fact]
    public void Without_a_grid_it_still_walks_straight_through()
    {
        var nav = Grid(
            ".....",
            "..#..",
            ".....");
        var npc = new TestNpc(0, 0);
        npc.StandAt(new Vector2(0.5f, 1.9f));
        var task = new GoToJob(new Vector2(4.5f, 1.9f));

        var cells = Walk(npc, task, nav: null);

        Assert.Contains(new Point(2, 1), cells);     // the old straight line, tree or not
        Assert.Equal(JobState.Completed, task.State);
    }

    [Fact]
    public void Plans_from_where_the_NPC_is_when_it_starts_not_when_the_task_was_made()
    {
        var nav = Grid("......");
        var npc = new TestNpc(0, 0);
        npc.StandAt(NavGrid.CentreOf(new Point(0, 0)));
        var task = new GoToJob(NavGrid.CentreOf(new Point(2, 0)));

        npc.StandAt(NavGrid.CentreOf(new Point(5, 0)));   // moved while the task waited its turn
        task.Execute(new JobContext { Owner = npc, Blackboard = new Blackboard(), Nav = nav });

        Assert.Equal(new Vector2(-1, 0), npc.Direction);  // heads left, back towards the target
    }

    [Fact]
    public void A_target_on_a_blocked_cell_stops_next_to_it()
    {
        var nav = Grid("....#");
        var npc = new TestNpc(0, 0);
        npc.StandAt(NavGrid.CentreOf(new Point(0, 0)));
        var task = new GoToJob(NavGrid.CentreOf(new Point(4, 0)));   // a POI on a tree

        var cells = Walk(npc, task, nav);

        Assert.Equal(JobState.Completed, task.State);
        Assert.DoesNotContain(new Point(4, 0), cells);
        Assert.True(Vector2.Distance(NavGrid.CentreOf(new Point(3, 0)), npc.Feet) <= 0.03f);
    }

    // ------------------------------------------------------- getting stuck and failing

    private static JobContext NavCtx(TestNpc npc, NavGrid nav, float dt) =>
        new() { Owner = npc, Blackboard = new Blackboard(), Nav = nav, DeltaTime = dt };

    [Fact]
    public void A_target_with_no_way_through_fails_at_once()
    {
        var nav = Grid(".#.");
        var npc = new TestNpc(0, 0);
        npc.StandAt(NavGrid.CentreOf(new Point(0, 0)));
        var task = new GoToJob(NavGrid.CentreOf(new Point(2, 0)));

        task.Execute(NavCtx(npc, nav, 0.25f));

        Assert.Equal(JobState.Failed, task.State);
        Assert.Equal(Vector2.Zero, npc.Direction);
    }

    [Fact]
    public void An_NPC_that_never_gets_closer_gives_up_after_its_replans()
    {
        // Something not in the grid (the player, another NPC) is in the way: the NPC is
        // never moved, as if collision pushed it back every tick.
        var nav = Grid(".....");
        var npc = new TestNpc(0, 0);
        npc.StandAt(NavGrid.CentreOf(new Point(0, 0)));
        var task = new GoToJob(NavGrid.CentreOf(new Point(4, 0)));

        // 1s stuck per try, and 3 replans: still trying after 3s...
        for (int tick = 0; tick < 12; tick++) task.Execute(NavCtx(npc, nav, 0.25f));
        Assert.NotEqual(JobState.Failed, task.State);

        // ...given up well before 8s.
        for (int tick = 0; tick < 20 && task.State != JobState.Failed; tick++) task.Execute(NavCtx(npc, nav, 0.25f));
        Assert.Equal(JobState.Failed, task.State);
        Assert.Equal(Vector2.Zero, npc.Direction);
    }

    [Fact]
    public void Slow_but_steady_progress_isnt_stuck()
    {
        var nav = Grid("......");
        var npc = new TestNpc(0, 0);
        npc.StandAt(NavGrid.CentreOf(new Point(0, 0)));
        var task = new GoToJob(NavGrid.CentreOf(new Point(5, 0)));

        // 0.02 cells every quarter second: about a minute for the walk, never stalled.
        for (int tick = 0; tick < 1000 && task.State != JobState.Completed; tick++)
        {
            task.Execute(NavCtx(npc, nav, 0.25f));
            npc.Position += npc.Direction * 0.02f;
        }

        Assert.Equal(JobState.Completed, task.State);
    }

    [Fact]
    public void When_stuck_it_plans_a_new_route()
    {
        var nav = Grid(
            "......",
            "......");
        var npc = new TestNpc(0, 0);
        npc.StandAt(NavGrid.CentreOf(new Point(0, 0)));
        var target = NavGrid.CentreOf(new Point(5, 0));
        var task = new GoToJob(target);
        task.Execute(NavCtx(npc, nav, 0.25f));        // an open row: straight at the target
        Assert.Equal(new[] { target }, task.RemainingWaypoints);

        nav.Block(new Point(3, 0));                    // now something's in the way
        for (int tick = 0; tick < 4; tick++)           // no closer for 1s
            task.Execute(NavCtx(npc, nav, 0.25f));

        // The new route goes round through row 1, so it has a corner before the target.
        Assert.True(task.RemainingWaypoints.Count > 1);
        Assert.Equal(target, task.RemainingWaypoints[^1]);
    }
}

public class WaitJobTests
{
    public WaitJobTests() => NullContentManager.Install();

    private static JobContext Ctx(TestNpc npc, float dt) =>
        new() { Owner = npc, Blackboard = new Blackboard(), DeltaTime = dt };

    [Fact]
    public void Does_not_complete_before_its_duration()
    {
        var npc = new TestNpc(0, 0);
        var task = new WaitJob(1f);

        task.Execute(Ctx(npc, 0.5f));

        Assert.NotEqual(JobState.Completed, task.State);
    }

    [Fact]
    public void Completes_once_the_elapsed_time_reaches_the_duration()
    {
        var npc = new TestNpc(0, 0);
        var task = new WaitJob(1f);

        task.Execute(Ctx(npc, 0.5f));   // 0.5s
        task.Execute(Ctx(npc, 0.5f));   // 1.0s total

        Assert.Equal(JobState.Completed, task.State);
    }

    [Fact]
    public void Completes_when_a_single_long_tick_exceeds_the_duration()
    {
        var npc = new TestNpc(0, 0);
        var task = new WaitJob(1f);

        task.Execute(Ctx(npc, 2f));

        Assert.Equal(JobState.Completed, task.State);
    }

    [Fact]
    public void Stands_still_while_waiting()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 0) };
        var task = new WaitJob(1f);

        task.Execute(Ctx(npc, 0.1f));

        Assert.Equal(Vector2.Zero, npc.Direction);
    }
}
