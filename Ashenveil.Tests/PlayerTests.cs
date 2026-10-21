using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// The player reads AshenveilGame.Input instead of the real keyboard, so a test can
/// hold keys down and step time by hand. Speed is 1.4 cells/s: half a second is 0.7 cells.
/// Positions are in cells, so the window size never comes into it.
/// </summary>
public class PlayerTests : IDisposable
{
    private readonly FakeInput _input = new();
    private readonly Player _player;

    public PlayerTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = _input;
        _player = new Player(new Vector2(5, 5), id: 0);
    }

    // Hand the real keyboard back so no other test inherits held keys.
    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private void Step(double seconds) =>
        _player.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(Keys.Right, 5.7f, 5)]
    [InlineData(Keys.Left,  4.3f, 5)]
    [InlineData(Keys.Up,    5, 4.3f)]
    [InlineData(Keys.Down,  5, 5.7f)]
    public void Arrow_key_moves_the_player_at_Speed_in_that_direction(Keys key, float x, float y)
    {
        _input.Hold(key);

        Step(0.5);

        Near.Equal(new Vector2(x, y), _player.Position);
    }

    [Fact]
    public void No_keys_held_means_no_movement()
    {
        Step(0.5);

        Assert.Equal(new Vector2(5, 5), _player.Position);
    }

    [Theory]
    [InlineData(Keys.W)]
    [InlineData(Keys.Space)]
    [InlineData(Keys.E)]
    public void Keys_that_are_not_arrows_do_not_move_the_player(Keys key)
    {
        _input.Hold(key);

        Step(0.5);

        Assert.Equal(new Vector2(5, 5), _player.Position);
    }

    [Theory]
    [InlineData(Keys.Left, Keys.Right)]
    [InlineData(Keys.Up, Keys.Down)]
    public void Opposite_keys_cancel_out(Keys a, Keys b)
    {
        _input.Hold(a, b);

        Step(0.5);

        Assert.Equal(new Vector2(5, 5), _player.Position);
    }

    [Fact]
    public void Two_keys_move_on_both_axes_at_full_speed_each()
    {
        // Not normalised: a diagonal covers 0.7 cells on X AND on Y, so ~0.99 overall.
        // If diagonals are ever slowed to match straight lines, this is the test to change.
        _input.Hold(Keys.Right, Keys.Down);

        Step(0.5);

        Near.Equal(new Vector2(5.7f, 5.7f), _player.Position);
    }

    [Fact]
    public void Movement_stops_the_frame_the_key_is_released()
    {
        _input.Hold(Keys.Right);
        Step(0.5);

        _input.Hold();
        Step(0.5);

        Near.Equal(new Vector2(5.7f, 5), _player.Position);
    }

    [Fact]
    public void Distance_depends_on_time_not_on_how_many_frames_it_took()
    {
        _input.Hold(Keys.Right);

        Step(0.25);
        Step(0.25);

        Near.Equal(new Vector2(5.7f, 5), _player.Position);
    }

    [Fact]
    public void A_frame_with_no_elapsed_time_does_not_move_the_player()
    {
        _input.Hold(Keys.Right);

        Step(0);

        Assert.Equal(new Vector2(5, 5), _player.Position);
    }

    [Fact]
    public void Movement_scales_with_Speed()
    {
        _player.Speed = 0.5f;
        _input.Hold(Keys.Right);

        Step(1);

        Assert.Equal(new Vector2(5.5f, 5), _player.Position);
    }

    [Fact]
    public void The_same_walk_covers_the_same_cells_at_any_window_size()
    {
        // Pixel speeds made the player faster in a small window (fewer pixels per cell).
        // In cells/s the window doesn't matter.
        var small = new Player(new Vector2(5, 5), id: 0);
        var large = new Player(new Vector2(5, 5), id: 0);
        _input.Hold(Keys.Right);

        Ashenveil.Core.Graphics.Layout.Update(500);
        small.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)));
        Ashenveil.Core.Graphics.Layout.Update(1080);
        large.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)));

        Assert.Equal(small.Position, large.Position);
        Near.Equal(new Vector2(6.4f, 5), large.Position);
    }

    [Fact]
    public void Direction_is_ignored_because_the_keyboard_drives_the_player()
    {
        _player.Direction = new Vector2(1, 0);

        Step(0.5);

        Assert.Equal(new Vector2(5, 5), _player.Position);
    }
}
