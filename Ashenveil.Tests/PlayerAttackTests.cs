using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// The player's sword on Space: one press is one swing, it roots them until it's done, and the
/// clip follows (attack, then hurt over everything when hit).
/// </summary>
public class PlayerAttackTests : IDisposable
{
    private readonly FakeInput _input = new();
    private readonly Player _player;

    public PlayerAttackTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = _input;
        _player = new Player(new Vector2(5, 5), id: 0);
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private void Step(double seconds = 0.05) =>
        _player.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Space_starts_a_swing_in_the_way_the_player_faces()
    {
        _input.Hold(Keys.Up);
        Step();

        _input.Hold(Keys.Space);
        Step();

        Assert.NotNull(_player.Swing);
        Assert.Equal(Facing.Up, _player.Swing.Facing);
        Assert.Equal(Player.PlayerState.Attacking, _player.State);
        Assert.Equal("attack_up", _player.CurrentAnim);
    }

    [Fact]
    public void Swinging_roots_the_player_until_the_swing_is_done()
    {
        _input.Hold(Keys.Space, Keys.Right);
        Step();
        var where = _player.Position;

        _input.Hold(Keys.Right);
        Step(Player.Sword.Duration / 2);
        Assert.Equal(where, _player.Position);

        Step(Player.Sword.Duration);     // done now
        Step();
        Assert.True(_player.Position.X > where.X);
        Assert.Equal("walk_side", _player.CurrentAnim);
    }

    [Fact]
    public void Holding_Space_swings_once_not_over_and_over()
    {
        _input.Hold(Keys.Space);
        Step();
        Step(Player.Sword.Duration + 0.01);   // the first swing ends, Space still held

        Step();

        Assert.Null(_player.Swing);
    }

    [Fact]
    public void Pressing_Space_again_after_a_swing_swings_again()
    {
        _input.Hold(Keys.Space);
        Step();
        _input.Hold();
        Step(Player.Sword.Duration + 0.01);

        _input.Hold(Keys.Space);
        Step();

        Assert.NotNull(_player.Swing);
    }

    [Fact]
    public void The_dead_cant_swing()
    {
        _player.Health.Kill();
        _input.Hold(Keys.Space);

        Step();

        Assert.Null(_player.Swing);
    }

    [Fact]
    public void A_flinch_plays_the_hurt_clip_then_goes_back_to_idle()
    {
        _player.StartFlinch();
        Step();
        Assert.Equal("hurt_down", _player.CurrentAnim);

        Step(Entity.FlinchSeconds);
        Assert.Equal("idle_down", _player.CurrentAnim);
    }

    [Fact]
    public void Being_knocked_back_isnt_walking_and_doesnt_turn_the_player()
    {
        _player.Knock(new Vector2(-1, 0), 0.4f);

        Step();

        Assert.Equal(Facing.Down, _player.Facing);
        Assert.Equal(Player.PlayerState.Idle, _player.State);
        Assert.True(_player.Position.X < 5);
    }
}
