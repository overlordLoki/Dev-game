using Ashenveil.Core;
using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Factions and opinions (docs/factions-plan.md): the table, an NPC's opinions and the lookup,
/// player standing, today's grudges, gossip, attitudes and rumours in the chat prompt,
/// join_faction, and the console commands. A small hand-made table, so the tests don't move
/// when Content/factions.json is tuned - except where the World's own table is the point.
/// </summary>
public class FactionTests : IDisposable
{
    private static readonly FactionTable Table = new(new()
    {
        ["guard"] = new() { joinable = true, views = new() { ["guard"] = 50, ["bandit"] = -80, ["village"] = 40, ["traveller"] = 0 } },
        ["bandit"] = new() { joinable = true, views = new() { ["guard"] = -80, ["bandit"] = 50, ["village"] = -40, ["traveller"] = -30 } },
        ["village"] = new() { joinable = false, views = new() { ["guard"] = 40, ["bandit"] = -60, ["village"] = 40, ["traveller"] = 10 } },
        ["traveller"] = new() { views = new() { ["guard"] = 0, ["bandit"] = -20, ["village"] = 10, ["traveller"] = 10 } },
    });

    private const string Level = """
        {
          "cols": 5, "rows": 5,
          "tiles": [["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"]],
          "pois": [ { "name": "Player_spawn", "x": 2, "y": 2 } ]
        }
        """;

    private readonly List<DecisionRequestDto> _decisionsAsked = new();
    private NpcDecisionDto _decision = new("Met.", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>());
    private readonly List<ConversationRequestDto> _chatsSent = new();

    public FactionTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = new FakeInput();
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private World NewWorld()
    {
        var world = new World(TestLevels.Location(Level),
            sendChat: req => { _chatsSent.Add(req); return Task.FromResult(Array.Empty<ChatMessage>()); },
            requestDecision: req => { _decisionsAsked.Add(req); return Task.FromResult(_decision); },
            requestReflection: _ => Task.FromResult<ReflectionDto>(null!),
            factions: Table);
        world.Init();
        return world;
    }

    // An NPC in some factions, with its faction opinions seeded as a spawned one's are.
    private static TestNpc Npc(int id, string name, params string[] factions)
    {
        var npc = new TestNpc(0, 0, id, name, new WanderBrain());
        npc.Factions.AddRange(factions);
        npc.Opinions.Seed(npc.Factions, Table);
        return npc;
    }

    private static Player NewPlayer()
    {
        var player = new Player(Vector2.Zero, 0);
        player.SeedStanding(Table);
        return player;
    }

    private static void Step(World world) => world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1 / 60.0)));

    // ------------------------------------------------------------------ the table

    [Fact]
    public void The_shipped_table_loads_and_has_the_factions_the_game_uses()
    {
        var table = FactionTable.Default;

        Assert.Equal(new[] { "guard", "bandit", "village", "traveller" }, table.Names);
        Assert.True(table.View("guard", "bandit") <= Attitudes.HostileAt);   // guards hate bandits
        Assert.False(table.IsJoinable(FactionTable.StartingFaction));
        Assert.Equal(0, table.View("guard", "nobody"));
    }

    [Theory]
    [InlineData(-100, Attitude.Hostile)]
    [InlineData(-60, Attitude.Hostile)]
    [InlineData(-59, Attitude.Wary)]
    [InlineData(-21, Attitude.Wary)]
    [InlineData(-20, Attitude.Neutral)]
    [InlineData(19, Attitude.Neutral)]
    [InlineData(20, Attitude.Friendly)]
    [InlineData(100, Attitude.Friendly)]
    public void Thresholds_map_a_score_to_an_attitude(int score, Attitude attitude) =>
        Assert.Equal(attitude, Attitudes.Of(score));

    // ------------------------------------------------------------------ opinions

    [Fact]
    public void An_NPCs_faction_opinions_are_seeded_from_the_table()
    {
        var guard = Npc(1, "Guard", "guard");

        Assert.Equal(-80, guard.Opinions.OfFaction("bandit"));
        Assert.Equal(50, guard.Opinions.OfFaction("guard"));
        Assert.Empty(guard.Opinions.People);   // nobody personal yet
    }

    [Fact]
    public void With_several_factions_the_lowest_view_wins_both_ways()
    {
        var guardAndVillager = Npc(1, "Watchman", "guard", "village");
        var bandit = Npc(2, "Grim", "bandit", "village");

        Assert.Equal(-80, guardAndVillager.Opinions.OfFaction("bandit"));   // seeded: guard -80 beats village -60
        // Judged as a bandit, though he's also a villager.
        Assert.Equal(-80, guardAndVillager.OpinionOf(bandit));
        Assert.Equal(0, Npc(3, "Nobody").OpinionOf(bandit));                 // no factions: no view
    }

    [Fact]
    public void With_no_personal_opinion_the_lookup_follows_the_faction_view_as_it_changes()
    {
        var guard = Npc(1, "Guard", "guard");
        var player = NewPlayer();
        Assert.Equal(0, guard.OpinionOf(player));   // the guard's standing with a new traveller

        player.ChangeStanding("guard", -25);

        Assert.Equal(-25, guard.OpinionOf(player));
        Assert.Empty(guard.Opinions.People);        // still no entry of its own
    }

    [Fact]
    public void The_first_change_starts_from_the_faction_view_and_then_stops_following_it()
    {
        var guard = Npc(1, "Guard", "guard");
        var player = NewPlayer();
        player.ChangeStanding("guard", -10);

        Assert.Equal(-40, guard.ChangeOpinionOf(player, -30, "he shoved me"));
        player.ChangeStanding("guard", +50);        // the guard as a whole forgives...
        guard.Opinions.SetFaction("traveller", 80);

        Assert.Equal(-40, guard.OpinionOf(player)); // ...this guard doesn't
        Assert.Equal("he shoved me", guard.Opinions.People[0].Why);
    }

    [Fact]
    public void A_guard_can_come_to_like_one_bandit_and_it_holds()
    {
        var guard = Npc(1, "Guard", "guard");
        var bandit = Npc(2, "Grim", "bandit");

        guard.ChangeOpinionOf(bandit, +100);
        guard.Opinions.ChangeFaction("bandit", -20);

        Assert.Equal(20, guard.OpinionOf(bandit));  // -80 + 100, and the faction hit didn't touch it
        Assert.Equal(-100, guard.Opinions.OfFaction("bandit"));
    }

    [Fact]
    public void Scores_stay_within_a_hundred_either_way()
    {
        var guard = Npc(1, "Guard", "guard");

        guard.Opinions.Change(5, 0, 500);
        guard.Opinions.ChangeFaction("bandit", -500);

        Assert.Equal(100, guard.Opinions.People[5].Score);
        Assert.Equal(-100, guard.Opinions.OfFaction("bandit"));
    }

    // ------------------------------------------------------------------ standing

    [Fact]
    public void A_new_player_is_a_traveller_with_standings_from_the_table()
    {
        var player = NewPlayer();

        Assert.Equal(new[] { "traveller" }, player.Factions);
        Assert.Equal(0, player.StandingWith("guard"));
        Assert.Equal(-30, player.StandingWith("bandit"));   // bandits are wary of travellers
        Assert.Equal(10, player.StandingWith("village"));
    }

    [Fact]
    public void Joining_raises_standing_with_it_and_shifts_the_rest_without_a_reset()
    {
        var player = NewPlayer();
        player.ChangeStanding("guard", 15);           // earned before joining

        Assert.True(player.Join("guard", Table));

        Assert.Contains("guard", player.Factions);
        Assert.Equal(15 + Player.JoinBonus, player.StandingWith("guard"));
        Assert.Equal(-30 - 80 / Player.ShiftShare, player.StandingWith("bandit"));    // bandits like you less
        Assert.Equal(10 + 40 / Player.ShiftShare, player.StandingWith("village"));    // villagers like you more
        Assert.False(player.Join("guard", Table));    // once
    }

    [Fact]
    public void Leaving_costs_standing_with_the_faction_left()
    {
        var player = NewPlayer();
        player.Join("guard", Table);
        int before = player.StandingWith("guard");

        Assert.True(player.Leave("guard", Table));

        Assert.DoesNotContain("guard", player.Factions);
        Assert.Equal(before - Player.LeavePenalty, player.StandingWith("guard"));
        Assert.Equal(-30, player.StandingWith("bandit"));   // the join's shift undone
        Assert.False(player.Leave("guard", Table));
    }

    // ------------------------------------------------------------------ grudges

    [Fact]
    public void An_attack_makes_the_victim_hostile_at_once_and_reflection_clears_the_grudge()
    {
        var world = NewWorld();
        var guard = Npc(10, "Guard", "guard");
        world.AddNpc(guard);
        Assert.Equal(Attitude.Neutral, Relations.AttitudeTo(guard, world.player, world.Events, 1));

        world.Events.Post(GameEvent.Of(EventType.Attacked, world.location.Name, guard.Feet, world.player.Id, guard.Id, "Traveller attacked Guard"));

        Assert.Equal(Attitude.Hostile, Relations.AttitudeTo(guard, world.player, world.Events, 1));
        Assert.Equal(Attitude.Hostile, Relations.AttitudeTo(guard, world.player, world.Events, 2));   // yesterday, not yet reflected
        Assert.Equal(0, guard.OpinionOf(world.player));                                              // the opinion itself waits for day end

        guard.Journal.LastReflectedDay = 1;   // the reflection on day 1 landed

        Assert.Equal(Attitude.Neutral, Relations.AttitudeTo(guard, world.player, world.Events, 2));
    }

    [Fact]
    public void Only_attacks_on_this_NPC_count_as_its_grudges()
    {
        var world = NewWorld();
        var guard = Npc(10, "Guard", "guard");
        var other = Npc(11, "Other", "guard");
        world.AddNpc(guard);
        world.AddNpc(other);

        world.Events.Post(GameEvent.Of(EventType.Attacked, world.location.Name, other.Feet, world.player.Id, other.Id, "x"));
        world.Events.Post(GameEvent.Of(EventType.Talked, world.location.Name, guard.Feet, world.player.Id, guard.Id, "y"));

        Assert.Empty(Relations.Grudges(world.Events, guard, 1));
        Assert.Equal(new[] { 0 }, Relations.Grudges(world.Events, other, 1));
    }

    // ------------------------------------------------------------------ gossip

    private static Memory Saw(int eventId, int day = 1) =>
        new("The traveller robbed the miller", MemoryKind.Event) { SourceId = eventId, Day = day };

    private static IEnumerable<Memory> Rumours(NPC npc) => npc.Journal.Memories.Where(m => m.Kind == MemoryKind.Rumour);

    [Fact]
    public void Friendly_NPCs_near_each_other_share_what_they_saw_as_rumours()
    {
        var marta = Npc(1, "Marta", "village");
        var hans = Npc(2, "Hans", "village");      // villagers like villagers (40)
        hans.StandAt(marta.Feet + new Vector2(1, 0));
        marta.Journal.Add(Saw(7));

        Assert.Equal(1, Gossip.Step(new[] { new NPC[] { marta, hans } }, today: 1));

        var rumour = Assert.Single(Rumours(hans));
        Assert.Equal((7, (int?)1, 1, 1), (rumour.SourceId!.Value, rumour.TellerId, rumour.Hops, rumour.Day));
        Assert.Equal("Marta told you: The traveller robbed the miller", NPC.Hearsay(rumour, id => id == 1 ? "Marta" : null));
    }

    [Fact]
    public void NPCs_out_of_range_or_who_do_not_like_each_other_share_nothing()
    {
        var marta = Npc(1, "Marta", "village");
        var far = Npc(2, "Far", "village");
        far.StandAt(marta.Feet + new Vector2(Gossip.Range + 1, 0));
        var bandit = Npc(3, "Grim", "bandit");     // next to her, but bandits and villagers don't get on
        bandit.StandAt(marta.Feet);
        marta.Journal.Add(Saw(7));

        Assert.Equal(0, Gossip.Step(new[] { new NPC[] { marta, far, bandit } }, today: 1));
    }

    [Fact]
    public void Someone_who_dislikes_the_teller_ignores_them()
    {
        var marta = Npc(1, "Marta", "village");
        var hans = Npc(2, "Hans", "village");
        hans.Opinions.SetPerson(marta.Id, -30);    // Hans doesn't trust Marta, though she likes him
        marta.Journal.Add(Saw(7));

        Assert.Equal(0, Gossip.Share(marta, hans, today: 1));
    }

    [Fact]
    public void A_rumour_stops_at_the_hop_limit()
    {
        var a = Npc(1, "A", "village");
        var b = Npc(2, "B", "village");
        var c = Npc(3, "C", "village");
        var d = Npc(4, "D", "village");
        a.Journal.Add(Saw(7));

        Gossip.Share(a, b, 1);   // hop 1
        Gossip.Share(b, c, 1);   // hop 2: the last
        Gossip.Share(c, d, 1);   // not passed on

        Assert.Equal(2, Rumours(c).Single().Hops);
        Assert.Empty(Rumours(d));
    }

    [Fact]
    public void An_NPC_keeps_an_event_once_however_many_tell_it()
    {
        var a = Npc(1, "A", "village");
        var b = Npc(2, "B", "village");
        var hearer = Npc(3, "Hearer", "village");
        a.Journal.Add(Saw(7));
        b.Journal.Add(Saw(7));
        var witness = Npc(4, "Witness", "village");
        witness.Journal.Add(Saw(7));

        Gossip.Share(a, hearer, 1);
        Gossip.Share(b, hearer, 1);
        Gossip.Share(a, witness, 1);   // saw it themselves

        Assert.Single(Rumours(hearer));
        Assert.Empty(Rumours(witness));
    }

    [Fact]
    public void An_NPC_is_not_told_an_event_again_after_reflecting_on_it()
    {
        var marta = Npc(1, "Marta", "village");
        var hans = Npc(2, "Hans", "village");
        marta.Journal.Add(Saw(7));               // Marta hasn't reflected on day 1 yet
        var seen = Saw(7);
        hans.Journal.Add(seen);
        hans.Journal.Forget(new[] { seen });     // Hans has: the memory left his hot tier

        Assert.Equal(0, Gossip.Share(marta, hans, today: 2));
        Assert.Empty(Rumours(hans));
    }

    [Fact]
    public void Known_events_are_only_kept_while_gossip_could_bring_them_round()
    {
        var journal = new Journal();
        journal.Add(Saw(7, day: 1));
        journal.Add(Saw(8, day: 1 + Journal.KnownEventDays));
        Assert.True(journal.Knows(7));

        journal.Add(Saw(9, day: 2 + Journal.KnownEventDays));

        Assert.False(journal.Knows(7));
        Assert.True(journal.Knows(8));
    }

    [Fact]
    public void Gossip_runs_in_the_world_every_few_seconds()
    {
        var world = NewWorld();
        var marta = Npc(10, "Marta", "village");
        var hans = Npc(11, "Hans", "village");
        hans.StandAt(marta.Feet + new Vector2(1, 0));
        world.AddNpc(marta);
        world.AddNpc(hans);
        marta.Journal.Add(Saw(7));

        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(Gossip.IntervalSeconds + 0.1)));

        Assert.Single(Rumours(hans));
    }

    // ------------------------------------------------------------------ the chat prompt

    [Fact]
    public void The_chat_request_has_each_NPCs_attitude_to_the_others_and_todays_rumours_as_hearsay()
    {
        var world = NewWorld();
        var guard = Npc(10, "Sir Lancelot", "guard");
        guard.StandAt(world.player.Feet + new Vector2(0.5f, 0));
        world.AddNpc(guard);
        guard.ChangeOpinionOf(world.player, -35, "he shoved me at the well");
        guard.Journal.Add(new Memory("The traveller robbed the miller", MemoryKind.Rumour) { SourceId = 3, TellerId = 10, Hops = 1, Day = 1 });
        guard.Journal.Add(new Memory("Old news", MemoryKind.Rumour) { SourceId = 2, TellerId = 10, Hops = 1, Day = 0 });

        var convo = world.StartConversation(world.player, guard);
        convo.Say(world.player.Name, "Good day.");

        var npc = Assert.Single(_chatsSent).participants.Single(p => p.name == "Sir Lancelot");
        Assert.Equal(new[] { "Traveller: wary (-35) - he shoved me at the well" }, npc.attitudes);
        Assert.Equal(new[] { "Sir Lancelot told you: The traveller robbed the miller" }, npc.rumours);
        Assert.Empty(npc.memories);   // rumours aren't facts
    }

    [Fact]
    public void A_grudge_shows_in_the_attitude_line()
    {
        var world = NewWorld();
        var guard = Npc(10, "Sir Lancelot", "guard");
        world.AddNpc(guard);
        world.Events.Post(GameEvent.Of(EventType.Attacked, world.location.Name, guard.Feet, world.player.Id, guard.Id, "Traveller attacked Sir Lancelot"));

        Assert.Equal("Traveller: hostile (0) - attacked you recently",
                     Relations.Describe(guard, world.player, world.player.Name, world.Events, 1));
    }

    // ------------------------------------------------------------------ join_faction

    private (World world, NPC guard) TalkedToTheGuard(string joinFaction, bool admits = true)
    {
        var world = NewWorld();
        var guard = admits ? new AdmittingNpc(10, "Captain", "guard") : (NPC)Npc(10, "Captain", "guard");
        guard.StandAt(world.player.Feet + new Vector2(0.5f, 0));
        world.AddNpc(guard);
        _decision = new NpcDecisionDto("Swore Loki in.", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>()) { join_faction = joinFaction };

        var convo = world.StartConversation(world.player, guard);
        convo.Add(world.player.Name, "May I join the guard?");
        convo.End();
        Step(world);   // finish: ask
        Step(world);   // apply
        return (world, guard);
    }

    private sealed class AdmittingNpc : TestNpc
    {
        public AdmittingNpc(int id, string name, string faction) : base(0, 0, id, name, new WanderBrain())
        {
            Admits = true;
            Factions.Add(faction);
            Opinions.Seed(Factions, Table);
        }
    }

    [Fact]
    public void An_NPC_who_can_admit_people_is_offered_the_choice_and_its_yes_lets_the_player_in()
    {
        var (world, guard) = TalkedToTheGuard("guard");

        Assert.Equal(new[] { "guard" }, Assert.Single(_decisionsAsked).can_admit);
        Assert.Contains("guard", world.player.Factions);
        var joined = world.Events.All.Single(e => e.Type == EventType.Joined);
        Assert.Equal(((int?)0, (int?)guard.Id), (joined.ActorId, joined.TargetId));
        Assert.Equal("Traveller joined the guard, sworn in by Captain", joined.Text);
    }

    [Fact]
    public void A_join_the_NPC_cannot_grant_is_ignored()
    {
        var (world, _) = TalkedToTheGuard("bandit");                       // not his to give
        Assert.DoesNotContain("bandit", world.player.Factions);

        _decisionsAsked.Clear();
        var (world2, _) = TalkedToTheGuard("guard", admits: false);         // can't let anyone in
        Assert.Empty(Assert.Single(_decisionsAsked).can_admit);
        Assert.DoesNotContain("guard", world2.player.Factions);
    }

    // ------------------------------------------------------------------ console

    private static string[] Run(World world, string input)
    {
        var commands = new CommandRegistry();
        WorldCommands.Register(commands, world);
        return commands.Execute(input).Select(l => l.Text).ToArray();
    }

    private static ConsoleLine RunOne(World world, string input)
    {
        var commands = new CommandRegistry();
        WorldCommands.Register(commands, world);
        return Assert.Single(commands.Execute(input));
    }

    [Fact]
    public void Factions_lists_the_table_and_who_is_in_each()
    {
        var world = NewWorld();
        world.AddNpc(Npc(10, "Guard", "guard"));

        var lines = Run(world, "factions");

        Assert.Equal("guard (joinable): guard 50, bandit -80, village 40, traveller 0", lines[0]);
        Assert.Equal("  members: Guard (#10)", lines[1]);
        Assert.Equal("  members: Traveller (you)", lines[7]);
    }

    [Fact]
    public void Join_and_leave_change_membership_post_an_event_and_show_standings()
    {
        var world = NewWorld();

        Assert.Equal("Joined the guard.", Run(world, "join guard")[0]);
        Assert.Contains("guard", world.player.Factions);
        Assert.Equal(EventType.Joined, world.Events.All.Last().Type);
        Assert.Equal(LineKind.Error, RunOne(world, "join guard").Kind);
        Assert.Equal(LineKind.Error, RunOne(world, "join pirates").Kind);

        Assert.Equal("Left the guard.", Run(world, "leave guard")[0]);
        Assert.Equal(EventType.Left, world.Events.All.Last().Type);
        Assert.Equal(LineKind.Error, RunOne(world, "leave guard").Kind);
    }

    [Fact]
    public void Opinion_sets_a_person_or_a_faction_and_opinions_shows_them()
    {
        var world = NewWorld();
        world.AddNpc(Npc(10, "Guard", "guard"));

        Assert.Equal("Guard now thinks -45 of Traveller.", RunOne(world, "opinion Guard player -45").Text);
        Assert.Equal("Guard now thinks 10 of the bandit.", RunOne(world, "opinion 10 bandit 10").Text);
        Assert.Equal(LineKind.Error, RunOne(world, "opinion 10 player 500").Kind);

        var lines = Run(world, "opinions Guard");
        Assert.Equal("Guard (#10), guard:", lines[0]);
        Assert.Contains("    Traveller (#0): -45 (wary)", lines);
        Assert.Contains(lines, l => l.StartsWith("  factions: guard 50, bandit 10"));
    }

    [Fact]
    public void Standing_shows_every_faction_and_sets_one()
    {
        var world = NewWorld();

        var lines = Run(world, "standing bandit -70");

        Assert.Equal("Traveller, in traveller. Standing:", lines[0]);
        Assert.Contains("  bandit: -70 (hostile)", lines);
        Assert.Equal(-70, world.player.StandingWith("bandit"));
    }

    [Fact]
    public void Event_attack_posts_an_attack_that_the_victim_holds_against_the_attacker()
    {
        var world = NewWorld();
        var guard = Npc(10, "Guard", "guard");
        world.AddNpc(guard);

        Assert.Equal($"#1 day 1, {world.location.Name}: Traveller attacked Guard", RunOne(world, "event attack player Guard").Text);

        Assert.Equal(new[] { 0 }, Relations.Grudges(world.Events, guard, 1));
        Assert.Contains(Run(world, "opinions 10"), l => l.StartsWith("  grudges"));
        Assert.Equal(LineKind.Error, RunOne(world, "event hug player Guard").Kind);
    }

    [Fact]
    public void Rumours_lists_what_an_NPC_heard_and_from_whom()
    {
        var world = NewWorld();
        var guard = Npc(10, "Guard", "guard");
        world.AddNpc(guard);
        guard.Journal.Add(new Memory("Grim robbed the miller", MemoryKind.Rumour) { SourceId = 4, TellerId = 0, Hops = 1, Day = 1 });

        var lines = Run(world, "rumours Guard");

        Assert.Equal("Guard (#10) has heard 1 rumour(s):", lines[0]);
        Assert.Equal("  - day 1: Traveller (#0) told you: Grim robbed the miller  (event #4, hop 1)", lines[1]);
    }
}
