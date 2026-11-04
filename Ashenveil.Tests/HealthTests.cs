using Ashenveil.Core;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Jobs;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// Health on its own (damage, healing, invulnerability, god mode), then what dying does to the
/// player and to an NPC: they stop, play their death clip, and get up again when healed.
/// </summary>
public class HealthTests : IDisposable
{
    private readonly FakeInput _input = new();

    public HealthTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = _input;
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private static void Step(Entity entity, double seconds = 0.1) =>
        entity.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    // ------------------------------------------------------------------ the component

    [Fact]
    public void Health_starts_full()
    {
        var health = new Health(80);

        Assert.Equal(80, health.Current);
        Assert.Equal(1f, health.Fraction);
        Assert.False(health.IsDead);
    }

    [Fact]
    public void Damage_lowers_it_and_never_below_zero()
    {
        var health = new Health(50);

        Assert.True(health.TakeDamage(20));
        Assert.Equal(30, health.Current);

        Assert.True(health.TakeDamage(100));
        Assert.Equal(0, health.Current);
        Assert.True(health.IsDead);
    }

    [Fact]
    public void Invulnerability_blocks_a_second_hit_then_wears_off()
    {
        var health = new Health(50);
        health.TakeDamage(10, invulnerableFor: 0.4f);

        Assert.False(health.TakeDamage(10));
        health.Update(0.3f);
        Assert.False(health.TakeDamage(10));
        health.Update(0.1f);
        Assert.True(health.TakeDamage(10));

        Assert.Equal(30, health.Current);
    }

    [Fact]
    public void God_mode_blocks_damage_but_not_kill()
    {
        var health = new Health(50) { God = true };

        Assert.False(health.TakeDamage(10));
        Assert.Equal(50, health.Current);

        health.Kill();
        Assert.True(health.IsDead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Nothing_or_less_is_not_a_hit(int amount)
    {
        var health = new Health(50);

        Assert.False(health.TakeDamage(amount, invulnerableFor: 1f));
        Assert.Equal(50, health.Current);
        Assert.Equal(0f, health.Invulnerable);
    }

    [Fact]
    public void Heal_never_goes_above_Max()
    {
        var health = new Health(50);
        health.TakeDamage(20);

        health.Heal(100);

        Assert.Equal(50, health.Current);
    }

    [Fact]
    public void The_dead_cant_be_hurt_again()
    {
        var health = new Health(50);
        health.Kill();

        Assert.False(health.TakeDamage(10));
    }

    [Fact]
    public void Died_is_raised_once_and_Revived_when_healed_from_zero()
    {
        var health = new Health(50);
        int died = 0, revived = 0;
        health.Died += () => died++;
        health.Revived += () => revived++;

        health.TakeDamage(30);
        health.TakeDamage(30);
        health.Kill();            // already dead: nothing
        health.Heal(10);
        health.Heal(10);          // alive already: no second Revived

        Assert.Equal(1, died);
        Assert.Equal(1, revived);
        Assert.Equal(20, health.Current);
    }

    [Fact]
    public void Max_must_be_above_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Health(0));
    }

    // ------------------------------------------------------------------ who has health

    [Fact]
    public void The_player_and_the_knight_have_health_and_a_plain_entity_doesnt()
    {
        Assert.Equal(Player.MaxHealth, new Player(Vector2.Zero, 0).Health.Max);
        Assert.Equal(80, new Knight(Vector2.Zero, 1, "Sir Test", new WanderBrain()).Health.Max);
        Assert.Null(new TestEntity(0, 0).Health);
        Assert.False(new TestEntity(0, 0).IsDead);
    }

    // ------------------------------------------------------------------ the player dying

    [Fact]
    public void A_dead_player_cant_move_and_plays_the_death_clip_for_their_facing()
    {
        var player = new Player(new Vector2(5, 5), 0);
        _input.Hold(Keys.Up);
        Step(player);
        var where = player.Position;

        player.Health.Kill();
        Step(player);

        Assert.Equal(where, player.Position);
        Assert.Equal(Player.PlayerState.Dead, player.State);
        Assert.Equal("death_up", player.CurrentAnim);
    }

    [Fact]
    public void Healing_a_dead_player_lets_them_walk_again()
    {
        var player = new Player(new Vector2(5, 5), 0);
        player.Health.Kill();

        player.Health.Heal(10);
        _input.Hold(Keys.Right);
        Step(player);

        Assert.Equal(Player.PlayerState.Walking, player.State);
        Assert.True(player.Position.X > 5);
    }

    // ------------------------------------------------------------------ an NPC dying

    [Fact]
    public void A_dead_NPC_stops_where_it_is()
    {
        var npc = new TestNpc(5, 5) { Speed = 1, Direction = new Vector2(1, 0) };
        npc.SetHealth(10);

        npc.Health.Kill();
        Step(npc, 1);

        Assert.Equal(NpcBrain.NPCState.Dead, npc.State);
        Assert.Equal(new Vector2(5, 5), npc.Position);
        Assert.Equal(Vector2.Zero, npc.Direction);
    }

    [Fact]
    public void A_dead_NPC_keeps_its_jobs_and_doesnt_run_them_until_healed()
    {
        var npc = new TestNpc(5, 5);
        npc.SetHealth(10);
        npc.Health.Kill();

        npc.GiveJob(new WaitJob(1));

        Assert.Equal(NpcBrain.NPCState.Dead, npc.State);
        Assert.Single(npc.Jobs);

        npc.Health.Heal(10);
        Assert.Equal(NpcBrain.NPCState.Working, npc.State);
    }

    [Fact]
    public void A_dead_knight_plays_his_death_clip()
    {
        var knight = new Knight(new Vector2(5, 5), 1, "Sir Test", new WanderBrain());

        knight.Health.Kill();
        Step(knight);

        Assert.Equal("death", knight.CurrentAnim);
    }

    // ------------------------------------------------------------------ knock-outs

    [Fact]
    public void A_named_NPC_is_knocked_out_and_gets_up_on_its_own_with_half_its_health()
    {
        var npc = new TestNpc(5, 5);
        npc.SetHealth(80);
        npc.GiveJob(new WaitJob(100));

        npc.Health.Kill();
        Assert.True(npc.IsKnockedOut);
        Near.Equal(NPC.KnockoutSeconds, npc.KnockedOut);

        Step(npc, NPC.KnockoutSeconds - 1);
        Assert.Equal(NpcBrain.NPCState.Dead, npc.State);   // still down

        Step(npc, 1.5);
        Assert.False(npc.IsDead);
        Assert.Equal(40, npc.Health.Current);
        Assert.Equal(0, npc.KnockedOut);
        Assert.Equal(NpcBrain.NPCState.Working, npc.State);   // back to its errand
    }

    [Fact]
    public void Healing_a_knocked_out_NPC_gets_it_up_early()
    {
        var npc = new TestNpc(5, 5);
        npc.SetHealth(80);
        npc.Health.Kill();

        npc.Health.Heal(80);

        Assert.False(npc.IsKnockedOut);
        Assert.Equal(0, npc.KnockedOut);
    }

    [Fact]
    public void An_NPC_that_dies_for_good_stays_down()
    {
        var npc = new TestNpc(5, 5) { Mortal = true };
        npc.SetHealth(80);

        npc.Health.Kill();
        Step(npc, NPC.KnockoutSeconds * 2);

        Assert.True(npc.IsDead);
        Assert.False(npc.IsKnockedOut);
        Assert.Equal(NpcBrain.NPCState.Dead, npc.State);
    }

    [Fact]
    public void A_dead_NPC_neither_pushes_nor_is_pushed()
    {
        var dead = new TestNpc(5, 5);
        dead.SetHealth(10);
        dead.Health.Kill();
        var alive = new TestNpc(5.5f, 5);

        dead.CheckEntityCollision(alive);

        Assert.Equal(new Vector2(5, 5), dead.Position);
    }
}
