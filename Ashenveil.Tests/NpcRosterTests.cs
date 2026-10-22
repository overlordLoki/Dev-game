using Ashenveil.Core.Entities;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Entities.brains.Task;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Graphics;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// The NpcRoster on its own: which NPCs exist and which level each is in. Spawning uses the
/// real shipped Medows level (copied into the test output), since it's the one level with a
/// starting NPC - the knight. Positions are in cells.
/// </summary>
public class NpcRosterTests
{
    private readonly NpcRoster _roster = new();

    public NpcRosterTests()
    {
        NullContentManager.Install();
        Layout.Update(500);
    }

    // ------------------------------------------------------------------ levels

    [Fact]
    public void A_level_never_visited_has_no_NPCs()
    {
        Assert.Empty(_roster.In("Nowhere"));
    }

    [Fact]
    public void An_NPC_is_only_in_the_level_it_was_added_to()
    {
        var npc = new TestNpc(0, 0);

        _roster.Add(new Location("Medows"), npc);

        Assert.Contains(npc, _roster.In("Medows"));
        Assert.Empty(_roster.In("Castle2"));
    }

    [Fact]
    public void Adding_an_NPC_tells_it_which_level_its_in()
    {
        var npc = new TestNpc(0, 0);
        var medows = new Location("Medows");

        _roster.Add(medows, npc);

        Assert.Same(medows, npc.Location);
    }

    // ------------------------------------------------------------------ finding

    [Fact]
    public void Find_searches_every_level_not_just_one()
    {
        var a = new TestNpc(0, 0, 7);
        var b = new TestNpc(0, 0, 8);
        _roster.Add(new Location("Medows"), a);
        _roster.Add(new Location("Castle2"), b);

        Assert.Same(b, _roster.Find(8));
        Assert.Equal(2, _roster.All.Count());
    }

    [Fact]
    public void Find_an_unknown_id_is_null()
    {
        Assert.Null(_roster.Find(99));
    }

    [Fact]
    public void Ids_are_never_handed_out_twice()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => _roster.NextId()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(0, ids);   // 0 is the player's
    }

    // ------------------------------------------------------------------ spawning

    [Fact]
    public void Populating_Medows_brings_in_its_knight_on_his_spawn_point()
    {
        var medows = new Location("Medows");

        _roster.Populate(medows);

        var knight = Assert.IsType<Knight>(Assert.Single(_roster.In("Medows")));
        // Feet on the point. His frame is 1.2 cells tall with the body in its bottom 40%, so
        // centring the frame (the old rule) left his feet below the cell.
        Near.Equal(medows.Poi("Knight_spawn").StandingSpot(), knight.Feet);
    }

    [Fact]
    public void Populating_the_same_level_again_does_not_respawn()
    {
        var medows = new Location("Medows");

        _roster.Populate(medows);
        _roster.Populate(medows);   // e.g. walking out and back in

        Assert.Single(_roster.In("Medows"));
    }

    [Fact]
    public void A_level_with_no_starting_NPCs_stays_empty()
    {
        var castle = new Location("Castle2");

        _roster.Populate(castle);

        Assert.Empty(_roster.In("Castle2"));
    }

    // -------------------------------------------------------------- navigation

    // Keeps the context it was run with, so a test can see what the brain handed it.
    private sealed class ProbeTask : ITask
    {
        public TaskState State { get; set; }
        public TaskContext Seen { get; private set; }
        public void Execute(TaskContext context) => Seen = context;
    }

    [Fact]
    public void A_level_has_a_nav_grid_the_size_of_its_tile_map()
    {
        var medows = new Location("Medows");

        Assert.Equal(medows.tileMap.Cols, medows.Nav.Cols);
        Assert.Equal(medows.tileMap.Rows, medows.Nav.Rows);
    }

    [Fact]
    public void Tasks_get_the_nav_grid_of_the_NPCs_level()
    {
        var brain = new WanderBrain();
        var npc = new TestNpc(0, 0, brain);
        var medows = new Location("Medows");
        _roster.Add(medows, npc);
        var probe = new ProbeTask();
        npc.GiveTask(probe);

        brain.Update(new GameTime());

        Assert.Same(medows.Nav, probe.Seen.Nav);
    }

    [Fact]
    public void Tasks_get_no_nav_grid_when_the_NPC_isnt_in_a_level()
    {
        var brain = new WanderBrain();
        var npc = new TestNpc(0, 0, brain);
        var probe = new ProbeTask();
        npc.GiveTask(probe);

        brain.Update(new GameTime());

        Assert.Null(probe.Seen.Nav);
    }
}
