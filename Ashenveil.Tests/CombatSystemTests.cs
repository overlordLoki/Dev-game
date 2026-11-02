using Ashenveil.Core;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Hit resolution with no World, like ConversationSystemTests: a few TestEntitys (1x1 bodies at
/// their Position), a swing, and a CombatSystem told who's fighting. The attacker stands at
/// (2, 2) facing right, so its hitbox runs from x 2.5 to 3.1, y 2.1 to 2.9.
/// </summary>
public class CombatSystemTests
{
    private static readonly AttackData Data = new(Damage: 10, Reach: 0.6f, Width: 0.8f,
        Windup: 0.1f, Active: 0.2f, Recovery: 0.1f, Knockback: 0.4f, Animation: "attack");

    private readonly CombatSystem _combat = new();
    private readonly TestEntity _attacker = Hurtable(2, 2, id: 1);

    public CombatSystemTests()
    {
        NullContentManager.Install();
        _attacker.Turn(Facing.Right);
    }

    private static TestEntity Hurtable(float x, float y, int id = 0, int max = 50)
    {
        var entity = new TestEntity(x, y, id);
        entity.SetHealth(max);
        return entity;
    }

    private static void Step(Entity entity, double seconds) =>
        entity.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    // Starts the swing and steps the attacker into its active phase, then resolves.
    private void SwingAt(params Entity[] others)
    {
        _attacker.StartSwing(Data);
        Step(_attacker, 0.15);
        Resolve(others);
    }

    private void Resolve(params Entity[] others) => _combat.Update(new List<Entity>(others) { _attacker });

    [Fact]
    public void A_swing_hits_someone_in_front_and_not_someone_behind()
    {
        var front = Hurtable(3, 2);
        var behind = Hurtable(1, 2);

        SwingAt(front, behind);

        Assert.Equal(40, front.Health.Current);
        Assert.Equal(50, behind.Health.Current);
    }

    [Fact]
    public void Out_of_reach_is_a_miss()
    {
        var far = Hurtable(3.2f, 2);

        SwingAt(far);

        Assert.Equal(50, far.Health.Current);
    }

    [Fact]
    public void Nothing_lands_during_the_windup()
    {
        var front = Hurtable(3, 2);
        _attacker.StartSwing(Data);
        Step(_attacker, 0.05);

        Resolve(front);

        Assert.Equal(50, front.Health.Current);
    }

    [Fact]
    public void One_swing_hits_a_target_once_over_several_frames()
    {
        var front = Hurtable(3, 2);
        int hits = 0;
        _combat.Hit += (_, _, _) => hits++;

        SwingAt(front);
        Step(_attacker, 0.05);
        Resolve(front);
        Step(_attacker, 0.05);
        Resolve(front);

        Assert.Equal(1, hits);
        Assert.Equal(40, front.Health.Current);
    }

    [Fact]
    public void A_second_swing_hits_again_once_invulnerability_has_worn_off()
    {
        var front = Hurtable(3, 2);
        SwingAt(front);
        Step(_attacker, 0.3);            // the first swing ends
        Step(front, CombatSystem.InvulnerableFor);
        front.Position = new Vector2(3, 2);   // undo the knockback

        SwingAt(front);

        Assert.Equal(30, front.Health.Current);
    }

    [Fact]
    public void Two_targets_in_the_hitbox_are_both_hit()
    {
        var a = Hurtable(2.8f, 1.6f);
        var b = Hurtable(2.8f, 2.6f);

        SwingAt(a, b);

        Assert.Equal(40, a.Health.Current);
        Assert.Equal(40, b.Health.Current);
    }

    [Fact]
    public void The_attacker_never_hits_themselves()
    {
        // Its hitbox starts at its own centre, so it always overlaps its own body.
        SwingAt();

        Assert.Equal(50, _attacker.Health.Current);
    }

    [Fact]
    public void The_dead_and_things_with_no_health_are_not_hit()
    {
        var dead = Hurtable(3, 2);
        dead.Health.Kill();
        var post = new TestEntity(3, 2);   // no health
        int hits = 0;
        _combat.Hit += (_, _, _) => hits++;

        SwingAt(dead, post);

        Assert.Equal(0, hits);
    }

    [Fact]
    public void Hit_and_Died_are_raised_once_each_with_who_did_it()
    {
        var front = Hurtable(3, 2, max: 10);
        var hits = new List<(Entity, Entity, int)>();
        var deaths = new List<(Entity, Entity)>();
        _combat.Hit += (attacker, victim, damage) => hits.Add((attacker, victim, damage));
        _combat.Died += (victim, killer) => deaths.Add((victim, killer));

        SwingAt(front);
        Step(_attacker, 0.05);
        Resolve(front);

        Assert.Equal((_attacker, front, 10), Assert.Single(hits));
        Assert.Equal((front, _attacker), Assert.Single(deaths));
    }

    [Fact]
    public void Knockback_moves_the_victim_away_from_the_attacker_by_its_distance()
    {
        var front = Hurtable(3, 2);

        SwingAt(front);
        Step(front, 0.05);
        Step(front, 0.05);
        Step(front, 0.2);   // longer than the knockback: it stops at the full distance

        Near.Equal(new Vector2(3.4f, 2), front.Position);
    }

    [Fact]
    public void A_hit_makes_the_victim_flinch()
    {
        var front = Hurtable(3, 2);

        SwingAt(front);

        Assert.True(front.IsFlinching);
        Step(front, Entity.FlinchSeconds);
        Assert.False(front.IsFlinching);
    }

    [Fact]
    public void A_hit_in_god_mode_doesnt_land_or_knock_back()
    {
        var front = Hurtable(3, 2);
        front.Health.God = true;

        SwingAt(front);
        Step(front, 0.2);

        Assert.Equal(new Vector2(3, 2), front.Position);
        Assert.False(front.IsFlinching);
    }

    [Fact]
    public void A_swinging_entity_cant_start_another_swing_or_move()
    {
        _attacker.Speed = 1;
        _attacker.Direction = new Vector2(1, 0);

        Assert.True(_attacker.StartSwing(Data));
        Assert.False(_attacker.StartSwing(Data));
        Step(_attacker, 0.2);

        Assert.Equal(new Vector2(2, 2), _attacker.Position);
    }

    [Fact]
    public void Once_the_swing_is_done_it_can_move_and_swing_again()
    {
        _attacker.StartSwing(Data);
        Step(_attacker, Data.Duration + 0.01);

        Assert.Null(_attacker.Swing);
        Assert.True(_attacker.StartSwing(Data));
    }

    [Fact]
    public void Dying_mid_swing_cancels_it()
    {
        _attacker.StartSwing(Data);

        _attacker.Health.Kill();

        Assert.Null(_attacker.Swing);
        Assert.False(_attacker.StartSwing(Data));
    }
}
