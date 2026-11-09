using Ashenveil.Core;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
using Ashenveil.Core.Entities.brains.Jobs;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// NPCs fighting, with no World: a TestNpc (a 1x1 body) with a KnightBrain, which stands still
/// when it isn't doing anything, against a TestEntity. Not in a level, so the chase walks
/// straight and the target is used as given.
/// </summary>
public class FightingTests
{
    // Round numbers: hit live from 0.1 to 0.2, done at 0.3. Reach 0.6 from the centre: 0.1 past
    // a 1x1 body, so a target touching it is in reach and one 0.2 away isn't.
    private static readonly AttackData Jab = new(Damage: 10, Reach: 0.6f, Width: 0.8f,
        Windup: 0.1f, Active: 0.1f, Recovery: 0.1f, Knockback: 0.3f, Animation: "attack");
    private static readonly AttackData Hook = Jab with { Animation = "attack2" };

    public FightingTests() => NullContentManager.Install();

    private static TestNpc Fighter(float x = 2, float y = 2, KnightBrain? brain = null)
    {
        var npc = new TestNpc(x, y, 1, "Fighter", brain ?? new KnightBrain(new Never()));
        npc.SetHealth(50);
        npc.Speed = 1;
        npc.TestAttacks = new[] { Jab };
        return npc;
    }

    private static TestEntity Foe(float x, float y)
    {
        var foe = new TestEntity(x, y, id: 2);
        foe.SetHealth(50);
        return foe;
    }

    private static void Step(Entity entity, double seconds = 0.05) =>
        entity.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    // Random that always (or never) says yes: a knight who always, or never, defends.
    private sealed class Always : Random { public override double NextDouble() => 0; }
    private sealed class Never : Random { public override double NextDouble() => 0.99; }

    // ------------------------------------------------------------------ starting and stopping

    [Fact]
    public void StartFighting_targets_them_and_pauses_the_job_queue()
    {
        var npc = Fighter();
        npc.GiveJob(new WaitJob(100));
        var foe = Foe(6, 2);

        npc.StartFighting(foe);

        Assert.Equal(NpcBrain.NPCState.Fighting, npc.State);
        Assert.Same(foe, npc.FightingWith);
        Assert.Single(npc.Jobs);
    }

    [Fact]
    public void A_job_given_mid_fight_waits_for_the_fight_to_end()
    {
        var npc = Fighter();
        npc.StartFighting(Foe(6, 2));

        npc.GiveJob(new WaitJob(100));

        Assert.Equal(NpcBrain.NPCState.Fighting, npc.State);
    }

    [Fact]
    public void Nobody_fights_themselves_the_dead_or_while_dead()
    {
        var npc = Fighter();
        npc.StartFighting(npc);
        Assert.Null(npc.FightingWith);

        var dead = Foe(3, 2);
        dead.Health.Kill();
        npc.StartFighting(dead);
        Assert.Null(npc.FightingWith);

        npc.Health.Kill();
        npc.StartFighting(Foe(3, 2));
        Assert.Equal(NpcBrain.NPCState.Dead, npc.State);
    }

    [Fact]
    public void When_the_target_dies_it_gives_up_and_resumes_its_queue()
    {
        var npc = Fighter();
        npc.GiveJob(new WaitJob(100));
        var foe = Foe(6, 2);
        npc.StartFighting(foe);

        foe.Health.Kill();
        Step(npc);

        Assert.Equal(NpcBrain.NPCState.Working, npc.State);
        Assert.Null(npc.FightingWith);
    }

    [Fact]
    public void Out_of_range_for_long_enough_it_gives_up()
    {
        var npc = Fighter();
        npc.Speed = 0;   // can't close the gap
        npc.StartFighting(Foe(2 + NpcBrain.GiveUpDistance + 1, 2));

        Step(npc, NpcBrain.GiveUpSeconds - 0.1);
        Assert.Equal(NpcBrain.NPCState.Fighting, npc.State);

        Step(npc, 0.2);
        Assert.NotEqual(NpcBrain.NPCState.Fighting, npc.State);
    }

    [Fact]
    public void Dying_mid_fight_ends_it()
    {
        var npc = Fighter();
        npc.StartFighting(Foe(6, 2));

        npc.Health.Kill();

        Assert.Equal(NpcBrain.NPCState.Dead, npc.State);
        Assert.Null(npc.FightingWith);
    }

    // ------------------------------------------------------------------ chasing and swinging

    [Fact]
    public void Out_of_reach_it_chases_faster_than_it_walks()
    {
        var npc = Fighter();
        npc.StartFighting(Foe(6, 2));

        Step(npc, 0.5);

        // Speed 1, chasing at ChaseSpeedFactor: 0.8 cells in half a second, straight at them.
        Near.Equal(new Vector2(2 + 0.5f * NpcBrain.ChaseSpeedFactor, 2), npc.Position);
        Assert.Null(npc.Swing);
    }

    [Fact]
    public void In_reach_it_faces_them_and_swings()
    {
        var npc = Fighter();
        npc.StartFighting(Foe(2, 3));   // touching, below

        Step(npc);

        Assert.NotNull(npc.Swing);
        Assert.Equal(Facing.Down, npc.Swing.Facing);
        Assert.Equal(new Vector2(2, 2), npc.Position);
    }

    [Fact]
    public void Just_out_of_reach_it_steps_in_instead_of_swinging()
    {
        var npc = Fighter();
        npc.StartFighting(Foe(3.2f, 2));

        Step(npc);

        Assert.Null(npc.Swing);
        Assert.True(npc.Position.X > 2);
    }

    [Fact]
    public void It_waits_out_a_cooldown_between_swings()
    {
        var npc = Fighter();
        npc.StartFighting(Foe(3, 2));
        Step(npc, 0.01);                            // first swing starts
        var first = npc.Swing;

        // Short frames, so a new swing can't start and finish inside one of them unseen.
        double waited = 0;
        while ((npc.Swing == null || npc.Swing == first) && waited < 5)
        {
            Step(npc, 0.01);
            waited += 0.01;
        }

        // The next swing starts a cooldown after the first one ends, not as soon as it ends.
        Assert.NotNull(npc.Swing);
        Assert.NotSame(first, npc.Swing);
        Assert.InRange(waited, Jab.Duration + NpcBrain.AttackCooldown - 0.02, Jab.Duration + NpcBrain.AttackCooldown + 0.02);
    }

    [Fact]
    public void Its_attacks_are_thrown_in_turn()
    {
        var npc = Fighter();
        npc.TestAttacks = new[] { Jab, Hook };
        npc.StartFighting(Foe(3, 2));

        Step(npc);
        Assert.Same(Jab, npc.Swing.Data);

        Step(npc, Jab.Duration + NpcBrain.AttackCooldown + 0.01);
        Step(npc);
        Assert.Same(Hook, npc.Swing.Data);
    }

    [Fact]
    public void An_NPC_with_no_attacks_chases_but_never_swings()
    {
        var npc = Fighter();
        npc.TestAttacks = Array.Empty<AttackData>();
        npc.StartFighting(Foe(3, 2));

        Step(npc);

        Assert.Null(npc.Swing);
    }

    [Fact]
    public void Its_swing_really_hurts_the_target()
    {
        var npc = Fighter();
        var foe = Foe(3, 2);
        var combat = new CombatSystem();
        npc.StartFighting(foe);

        for (int i = 0; i < 6; i++)
        {
            Step(npc);
            combat.Update(new List<Entity> { npc, foe });
        }

        Assert.Equal(40, foe.Health.Current);
    }

    // ------------------------------------------------------------------ defending

    [Fact]
    public void A_knight_who_defends_raises_his_guard_against_a_swing_coming_his_way()
    {
        var npc = Fighter(brain: new KnightBrain(new Always()));
        var foe = Foe(3, 2);
        foe.Turn(Facing.Left);
        npc.StartFighting(foe);

        foe.StartSwing(Jab);
        Step(npc);

        Assert.True(npc.IsGuarding);
        Assert.Equal(Facing.Right, npc.Facing);
        Assert.Null(npc.Swing);
    }

    [Fact]
    public void A_knight_who_doesnt_defend_swings_instead()
    {
        var npc = Fighter();   // never defends
        var foe = Foe(3, 2);
        foe.Turn(Facing.Left);
        npc.StartFighting(foe);

        foe.StartSwing(Jab);
        Step(npc);

        Assert.False(npc.IsGuarding);
        Assert.NotNull(npc.Swing);
    }

    [Fact]
    public void A_swing_that_wouldnt_reach_isnt_defended_against()
    {
        var npc = Fighter(brain: new KnightBrain(new Always()));
        var foe = Foe(3, 2);
        foe.Turn(Facing.Right);   // swinging away from him
        npc.StartFighting(foe);

        foe.StartSwing(Jab);
        Step(npc);

        Assert.False(npc.IsGuarding);
    }

    [Fact]
    public void A_guard_blocks_from_the_front_and_not_from_behind()
    {
        var combat = new CombatSystem();
        var guard = Foe(3, 2);
        guard.Turn(Facing.Left);
        guard.StartGuard(1);
        var front = new TestEntity(2, 2, id: 3) { };
        front.Turn(Facing.Right);
        var behind = new TestEntity(4, 2, id: 4);
        behind.Turn(Facing.Left);
        int blocked = 0;
        combat.Blocked += (_, _) => blocked++;

        front.StartSwing(Jab);
        Step(front, 0.15);
        combat.Update(new List<Entity> { front, guard });
        Assert.Equal(50, guard.Health.Current);
        Assert.Equal(1, blocked);

        behind.StartSwing(Jab);
        Step(behind, 0.15);
        combat.Update(new List<Entity> { behind, guard });
        Assert.Equal(40, guard.Health.Current);
    }

    [Fact]
    public void Guarding_roots_you_to_the_spot()
    {
        var foe = Foe(3, 2);
        foe.Speed = 1;
        foe.Direction = new Vector2(1, 0);
        foe.StartGuard(0.5f);

        Step(foe, 0.2);

        Assert.Equal(new Vector2(3, 2), foe.Position);
    }
}
