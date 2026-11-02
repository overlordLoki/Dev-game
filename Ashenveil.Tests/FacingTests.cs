using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// Facing comes from movement: whichever axis moved further, kept while standing still.
/// The player turns it into a clip name ("walk_up", "idle_side"...).
/// </summary>
public class FacingTests : IDisposable
{
    private readonly FakeInput _input = new();

    public FacingTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = _input;
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private static void Step(Entity entity, double seconds = 0.1) =>
        entity.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(1, 0, Facing.Right)]
    [InlineData(-1, 0, Facing.Left)]
    [InlineData(0, -1, Facing.Up)]
    [InlineData(0, 1, Facing.Down)]
    [InlineData(1, 0.5f, Facing.Right)]   // mostly sideways
    [InlineData(-0.3f, -1, Facing.Up)]    // mostly up
    public void Movement_faces_the_axis_that_moved_further(float x, float y, Facing expected)
    {
        var entity = new TestEntity(5, 5) { Speed = 1, Direction = new Vector2(x, y) };

        Step(entity);

        Assert.Equal(expected, entity.Facing);
    }

    [Fact]
    public void A_new_entity_faces_down()
    {
        Assert.Equal(Facing.Down, new TestEntity(5, 5).Facing);
    }

    [Fact]
    public void Standing_still_keeps_the_last_facing()
    {
        var entity = new TestEntity(5, 5) { Speed = 1, Direction = new Vector2(-1, 0) };
        Step(entity);

        entity.Direction = Vector2.Zero;
        Step(entity);

        Assert.Equal(Facing.Left, entity.Facing);
    }

    [Fact]
    public void An_exact_diagonal_keeps_a_facing_it_is_already_walking()
    {
        // Walking right, then adding down: still facing right, not flicking to down.
        var player = new Player(new Vector2(5, 5), id: 0);
        _input.Hold(Keys.Right);
        Step(player);

        _input.Hold(Keys.Right, Keys.Down);
        Step(player);

        Assert.Equal(Facing.Right, player.Facing);
    }

    [Fact]
    public void An_exact_diagonal_from_an_unrelated_facing_faces_sideways()
    {
        var player = new Player(new Vector2(5, 5), id: 0);   // faces down
        _input.Hold(Keys.Left, Keys.Up);

        Step(player);

        Assert.Equal(Facing.Left, player.Facing);
    }

    [Theory]
    [InlineData(Keys.Up, "walk_up")]
    [InlineData(Keys.Down, "walk_down")]
    [InlineData(Keys.Left, "walk_side")]
    [InlineData(Keys.Right, "walk_side")]
    public void Walking_plays_the_walk_clip_for_the_facing(Keys key, string clip)
    {
        var player = new Player(new Vector2(5, 5), id: 0);
        _input.Hold(key);

        Step(player);

        Assert.Equal(clip, player.CurrentAnim);
        Assert.Equal(Player.PlayerState.Walking, player.State);
    }

    [Fact]
    public void Stopping_plays_idle_in_the_last_facing()
    {
        var player = new Player(new Vector2(5, 5), id: 0);
        _input.Hold(Keys.Up);
        Step(player);

        _input.Hold();
        Step(player);

        Assert.Equal("idle_up", player.CurrentAnim);
        Assert.Equal(Player.PlayerState.Idle, player.State);
    }

    [Fact]
    public void A_new_player_idles_facing_down()
    {
        var player = new Player(new Vector2(5, 5), id: 0);

        Assert.Equal("idle_down", player.CurrentAnim);
    }

    [Theory]
    [InlineData("attack", Facing.Down, "attack_down")]
    [InlineData("hurt", Facing.Up, "hurt_up")]
    [InlineData("death", Facing.Left, "death_side")]
    [InlineData("idle", Facing.Right, "idle_side")]
    public void Clip_names_an_action_in_a_facing(string action, Facing facing, string clip)
    {
        Assert.Equal(clip, Player.Clip(action, facing));
    }
}
