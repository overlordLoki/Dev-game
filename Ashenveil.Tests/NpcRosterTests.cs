using Ashenveil.Core.Entities;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Graphics;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// The NpcRoster on its own: which NPCs exist and which level each is in. Spawning uses the
/// real shipped Medows level (copied into the test output), since it's the one level with a
/// starting NPC - the knight. With Layout.Update(500) a cell is 100px.
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

        _roster.Add("Medows", npc);

        Assert.Contains(npc, _roster.In("Medows"));
        Assert.Empty(_roster.In("Castle2"));
    }

    // ------------------------------------------------------------------ finding

    [Fact]
    public void Find_searches_every_level_not_just_one()
    {
        var a = new TestNpc(0, 0, 7);
        var b = new TestNpc(0, 0, 8);
        _roster.Add("Medows", a);
        _roster.Add("Castle2", b);

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
        var centre = knight.Position + new Vector2(knight.Width / 2f, knight.Height / 2f);
        Assert.Equal(medows.Poi("Knight_spawn").ToPixel(), centre);
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
}
