using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities;
using Ashenveil.Core.GameWorld;

namespace Ashenveil.Tests;

/// <summary>
/// Fights into events with no World: blows are reported straight to the recorder (as
/// CombatSystem's Hit and Blocked would), and what it posts is collected. Everyone is in one
/// level unless a test moves them.
/// </summary>
public class FightRecorderTests
{
    private readonly List<GameEvent> _posted = new();
    private readonly Dictionary<Entity, string> _levels = new();
    private readonly FightRecorder _fights;

    private readonly TestNpc _loki = Person(1, "Loki");
    private readonly TestNpc _lancelot = Person(2, "Sir Lancelot");

    public FightRecorderTests()
    {
        NullContentManager.Install();
        _fights = new FightRecorder(who => _levels.TryGetValue(who, out var level) ? level : "field",
                                    e => { _posted.Add(e); return e; });
    }

    private static TestNpc Person(int id, string name)
    {
        var npc = new TestNpc(0, 0, id, name, new WanderBrain());
        npc.SetHealth(50);
        return npc;
    }

    private GameEvent PostedOne() => Assert.Single(_posted);

    // ------------------------------------------------------------------ one event per fight

    [Fact]
    public void A_whole_exchange_of_blows_is_one_event_posted_once_it_goes_quiet()
    {
        _fights.Landed(_loki, _lancelot, 8);
        _fights.Landed(_lancelot, _loki, 8);
        _fights.Landed(_loki, _lancelot, 8);
        _fights.Update(FightRecorder.QuietSeconds - 1);
        _fights.Landed(_lancelot, _loki, 12);
        _fights.Landed(_loki, _lancelot, 8);
        _fights.Blocked(_loki, _lancelot);

        _fights.Update(FightRecorder.QuietSeconds - 1);
        Assert.Empty(_posted);   // the gap before the last blows didn't split it

        _fights.Update(1);
        var e = PostedOne();
        Assert.Equal(EventType.Attacked, e.Type);
        Assert.Equal("Loki attacked Sir Lancelot, who fought back. Loki landed 3 blows, Sir Lancelot 2. Sir Lancelot blocked 1.", e.Text);
        Assert.Empty(_fights.Open);
    }

    [Fact]
    public void Whoever_threw_the_first_blow_is_the_attacker_even_if_they_came_off_worse()
    {
        _fights.Landed(_loki, _lancelot, 1);
        for (int i = 0; i < 4; i++) _fights.Landed(_lancelot, _loki, 10);

        _fights.EndAll();

        var e = PostedOne();
        Assert.Equal(_loki.Id, e.ActorId);
        Assert.Equal(_lancelot.Id, e.TargetId);
    }

    [Fact]
    public void A_single_hit_and_run_still_becomes_an_event()
    {
        _fights.Landed(_loki, _lancelot, 8);

        _fights.Update(FightRecorder.QuietSeconds);

        Assert.Equal("Loki attacked Sir Lancelot, landing 1 blow.", PostedOne().Text);
    }

    [Fact]
    public void A_fight_that_was_all_blocks_is_still_an_attack()
    {
        _fights.Blocked(_loki, _lancelot);
        _fights.Blocked(_loki, _lancelot);

        _fights.EndAll();

        var e = PostedOne();
        Assert.Equal(EventType.Attacked, e.Type);
        Assert.Equal(_loki.Id, e.ActorId);
        Assert.Equal("Loki attacked Sir Lancelot, who blocked every blow.", e.Text);
    }

    [Fact]
    public void Two_pairs_fighting_are_two_fights()
    {
        var bandit = Person(3, "Bandit");
        _fights.Landed(_loki, _lancelot, 8);
        _fights.Landed(bandit, _loki, 8);

        _fights.EndAll();

        Assert.Equal(2, _posted.Count);
        Assert.Contains(_posted, e => e.ActorId == _loki.Id && e.TargetId == _lancelot.Id);
        Assert.Contains(_posted, e => e.ActorId == bandit.Id && e.TargetId == _loki.Id);
    }

    [Fact]
    public void The_event_is_where_the_first_blow_landed()
    {
        _lancelot.StandAt(new Microsoft.Xna.Framework.Vector2(3, 4));
        _levels[_lancelot] = _levels[_loki] = "village";
        _fights.Landed(_loki, _lancelot, 8);
        _lancelot.StandAt(new Microsoft.Xna.Framework.Vector2(9, 9));   // knocked about since

        _fights.EndAll();

        var e = PostedOne();
        Assert.Equal("village", e.Level);
        Assert.Equal(new Microsoft.Xna.Framework.Vector2(3, 4), e.Where);
    }

    // ------------------------------------------------------------------ how fights end

    [Fact]
    public void A_killing_blow_ends_the_fight_straight_away_as_a_killing()
    {
        _lancelot.Mortal = true;
        _fights.Landed(_loki, _lancelot, 8);
        _lancelot.Health.Kill();

        _fights.Update(0.01f);

        var e = PostedOne();
        Assert.Equal(EventType.Killed, e.Type);
        Assert.Equal(_loki.Id, e.ActorId);
        Assert.Equal(_lancelot.Id, e.TargetId);
        Assert.EndsWith("Sir Lancelot was killed.", e.Text);
    }

    [Fact]
    public void Killing_whoever_started_it_names_the_defender_as_the_killer()
    {
        _loki.Mortal = true;
        _fights.Landed(_loki, _lancelot, 8);
        _fights.Landed(_lancelot, _loki, 8);
        _loki.Health.Kill();

        _fights.Update(0.01f);

        var e = PostedOne();
        Assert.Equal(EventType.Killed, e.Type);
        Assert.Equal(_lancelot.Id, e.ActorId);
        Assert.Equal(_loki.Id, e.TargetId);
        Assert.StartsWith("Loki attacked Sir Lancelot", e.Text);   // who began it is still in the words
    }

    [Fact]
    public void Downing_a_named_NPC_is_a_knock_out_not_a_killing()
    {
        _fights.Landed(_loki, _lancelot, 8);
        _lancelot.Health.Kill();

        _fights.Update(0.01f);

        var e = PostedOne();
        Assert.Equal(EventType.KnockedOut, e.Type);
        Assert.Equal(_loki.Id, e.ActorId);
        Assert.Equal(_lancelot.Id, e.TargetId);
        Assert.EndsWith("Sir Lancelot was knocked out.", e.Text);
    }

    [Fact]
    public void Downing_the_player_is_a_knock_out_too_since_they_respawn()
    {
        var player = new Player(Microsoft.Xna.Framework.Vector2.Zero, 0, "Loki");
        _fights.Landed(_lancelot, player, 8);
        player.Health.Kill();

        _fights.Update(0.01f);

        var e = PostedOne();
        Assert.Equal(EventType.KnockedOut, e.Type);
        Assert.Equal(_lancelot.Id, e.ActorId);
        Assert.EndsWith("Loki was knocked out.", e.Text);
    }

    [Fact]
    public void Blows_after_a_fight_ended_start_a_new_one()
    {
        _fights.Landed(_loki, _lancelot, 8);
        _fights.Update(FightRecorder.QuietSeconds);

        _fights.Landed(_lancelot, _loki, 8);
        _fights.EndAll();

        Assert.Equal(2, _posted.Count);
        Assert.Equal(_lancelot.Id, _posted[1].ActorId);   // this time Lancelot started it
    }

    [Fact]
    public void A_fight_ends_when_they_are_no_longer_in_the_same_level()
    {
        _fights.Landed(_loki, _lancelot, 8);

        _levels[_loki] = "cave";
        _fights.Update(0.01f);

        Assert.Single(_posted);
    }

    [Fact]
    public void Ending_them_all_posts_every_open_fight_and_leaves_none()
    {
        _fights.Landed(_loki, _lancelot, 8);
        _fights.Landed(Person(3, "Bandit"), _loki, 8);

        _fights.EndAll();
        _fights.EndAll();

        Assert.Equal(2, _posted.Count);
        Assert.Empty(_fights.Open);
    }
}
