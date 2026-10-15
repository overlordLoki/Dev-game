using Ashenveil.Core;
using Ashenveil.Core.Entities.brains.Task;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Tasks are pure logic: they read the owner's position/time and set Direction/State.
/// No movement happens here (Entity.Move does that during Update) and no graphics are
/// touched, so we can test the decision each task makes in isolation.
/// </summary>
public class GoToTaskTests
{
    public GoToTaskTests() => NullContentManager.Install();   // TestNpc construction needs Textures ready

    private static TaskContext Ctx(TestNpc npc, float dt = 0f) =>
        new() { Owner = npc, Blackboard = new Blackboard(), DeltaTime = dt };

    [Fact]
    public void Heads_straight_at_a_target_to_the_right()
    {
        var npc = new TestNpc(0, 0);
        var centre = npc.Position + new Vector2(npc.Width / 2f, npc.Height / 2f);
        var task = new GoToTask(centre + new Vector2(100, 0));   // 100px right of the npc's centre

        task.Execute(Ctx(npc));

        Assert.Equal(new Vector2(1, 0), npc.Direction);          // unit heading toward target
        Assert.NotEqual(TaskState.Completed, task.State);  // still travelling
    }

    [Fact]
    public void Heading_is_normalised_on_a_diagonal()
    {
        var npc = new TestNpc(0, 0);
        var centre = npc.Position + new Vector2(npc.Width / 2f, npc.Height / 2f);
        var task = new GoToTask(centre + new Vector2(3, 4));   // length 5 from centre, outside the radius

        task.Execute(Ctx(npc));

        Assert.Equal(1f, npc.Direction.Length(), 3);  // unit length (speed is Entity's job, not direction's)
        Assert.True(npc.Direction.X > 0 && npc.Direction.Y > 0);
    }

    [Fact]
    public void Completes_and_stops_once_within_the_arrive_radius()
    {
        var npc = new TestNpc(0, 0);
        var centre = npc.Position + new Vector2(npc.Width / 2f, npc.Height / 2f);
        var task = new GoToTask(centre + new Vector2(2, 0));   // 2px from centre, inside the 4px radius

        task.Execute(Ctx(npc));

        Assert.Equal(TaskState.Completed, task.State);
        Assert.Equal(Vector2.Zero, npc.Direction);    // stopped, not still steering
    }
}

public class WaitTaskTests
{
    public WaitTaskTests() => NullContentManager.Install();

    private static TaskContext Ctx(TestNpc npc, float dt) =>
        new() { Owner = npc, Blackboard = new Blackboard(), DeltaTime = dt };

    [Fact]
    public void Does_not_complete_before_its_duration()
    {
        var npc = new TestNpc(0, 0);
        var task = new WaitTask(1f);

        task.Execute(Ctx(npc, 0.5f));

        Assert.NotEqual(TaskState.Completed, task.State);
    }

    [Fact]
    public void Completes_once_the_elapsed_time_reaches_the_duration()
    {
        var npc = new TestNpc(0, 0);
        var task = new WaitTask(1f);

        task.Execute(Ctx(npc, 0.5f));   // 0.5s
        task.Execute(Ctx(npc, 0.5f));   // 1.0s total

        Assert.Equal(TaskState.Completed, task.State);
    }

    [Fact]
    public void Completes_when_a_single_long_tick_exceeds_the_duration()
    {
        var npc = new TestNpc(0, 0);
        var task = new WaitTask(1f);

        task.Execute(Ctx(npc, 2f));

        Assert.Equal(TaskState.Completed, task.State);
    }

    [Fact]
    public void Stands_still_while_waiting()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 0) };
        var task = new WaitTask(1f);

        task.Execute(Ctx(npc, 0.1f));

        Assert.Equal(Vector2.Zero, npc.Direction);
    }
}
