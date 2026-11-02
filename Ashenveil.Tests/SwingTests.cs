using Ashenveil.Core;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// A swing on its own: its phases change at the right times, its hitbox is only live in the
/// active phase, and it sits in front of the attacker's body for each facing. In cells and
/// seconds, with a TestEntity (a 1x1 body at its Position).
/// </summary>
public class SwingTests
{
    // Round numbers: windup 0.1, active 0.2, recovery 0.1, so done at 0.4.
    private static readonly AttackData Data = new(Damage: 10, Reach: 0.6f, Width: 0.8f,
        Windup: 0.1f, Active: 0.2f, Recovery: 0.1f, Knockback: 0.4f, Animation: "attack");

    public SwingTests() => NullContentManager.Install();

    private static Swing NewSwing(Facing facing = Facing.Right) => new(Data, new TestEntity(2, 2), facing);

    [Theory]
    [InlineData(0.05f, Swing.SwingPhase.Windup)]
    [InlineData(0.15f, Swing.SwingPhase.Active)]
    [InlineData(0.35f, Swing.SwingPhase.Recovery)]
    [InlineData(0.45f, Swing.SwingPhase.Done)]
    public void Phases_change_at_the_right_times(float after, Swing.SwingPhase phase)
    {
        var swing = NewSwing();

        swing.Update(after);

        Assert.Equal(phase, swing.Phase);
    }

    [Fact]
    public void The_hitbox_is_only_live_in_the_active_phase()
    {
        var swing = NewSwing();

        Assert.True(swing.Hitbox.IsEmpty);           // not started

        swing.Update(0.05f);                        // windup
        Assert.False(swing.IsLive);
        Assert.True(swing.Hitbox.IsEmpty);

        swing.Update(0.1f);                         // active
        Assert.True(swing.IsLive);
        Assert.False(swing.Hitbox.IsEmpty);

        swing.Update(0.2f);                         // recovery, jumped into from the active phase
        swing.Update(0.01f);
        Assert.False(swing.IsLive);
        Assert.True(swing.Hitbox.IsEmpty);
    }

    [Fact]
    public void A_frame_that_jumps_right_over_the_active_phase_still_counts_as_live()
    {
        // A hitch from 0.05 (windup) to 0.35 (recovery) mustn't lose the hit altogether.
        var swing = NewSwing();
        swing.Update(0.05f);

        swing.Update(0.3f);

        Assert.True(swing.IsLive);
    }

    [Fact]
    public void A_frame_with_no_time_in_it_is_not_live()
    {
        var swing = NewSwing();
        swing.Update(0.15f);

        swing.Update(0f);

        Assert.False(swing.IsLive);
    }

    [Theory]
    // The body's centre is (2.5, 2.5). Reach 0.6 out from it, width 0.8 across.
    [InlineData(Facing.Right, 2.5f, 2.1f, 0.6f, 0.8f)]
    [InlineData(Facing.Left,  1.9f, 2.1f, 0.6f, 0.8f)]
    [InlineData(Facing.Down,  2.1f, 2.5f, 0.8f, 0.6f)]
    [InlineData(Facing.Up,    2.1f, 1.9f, 0.8f, 0.6f)]
    public void The_hitbox_sits_in_front_of_the_body_for_each_facing(Facing facing, float x, float y, float w, float h)
    {
        var swing = NewSwing(facing);
        swing.Update(0.15f);

        Near.Equal(new RectF(x, y, w, h), swing.Hitbox);
    }

    [Fact]
    public void Each_target_is_hit_once_per_swing()
    {
        var swing = NewSwing();
        var target = new TestEntity(3, 2);

        Assert.True(swing.MarkHit(target));
        Assert.False(swing.MarkHit(target));
        Assert.True(swing.HasHit(target));
    }

    [Fact]
    public void Duration_is_all_three_phases()
    {
        Assert.Equal(0.4f, Data.Duration, 4);
    }
}
