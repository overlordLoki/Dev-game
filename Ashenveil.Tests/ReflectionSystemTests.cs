using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.GameWorld;

namespace Ashenveil.Tests;

/// <summary>
/// The ReflectionSystem on its own: no World, no clock. NPCs are given journals by hand, the
/// day turning over is a method call, and the reflection call is a fake whose answers the test
/// hands out one at a time - so "in flight" really is in flight.
/// </summary>
public class ReflectionSystemTests
{
    private readonly List<(ReflectionRequestDto req, TaskCompletionSource<ReflectionDto> answer)> _asked = new();
    private readonly ReflectionSystem _system;

    public ReflectionSystemTests()
    {
        NullContentManager.Install();
        _system = new ReflectionSystem(req =>
        {
            var answer = new TaskCompletionSource<ReflectionDto>();
            _asked.Add((req, answer));
            return answer.Task;
        });
    }

    private static TestNpc Npc(int id, string name = "Test") => new(0, 0, id, name, new WanderBrain());

    private static Memory Seen(string text, int day, int eventId) =>
        new(text, MemoryKind.Event) { Day = day, SourceId = eventId };

    private static ReflectionDto Answer(string diary, params string[] facts) => new() { diary = diary, facts = facts };

    // ------------------------------------------------------------------ 1. queue

    [Fact]
    public void A_day_turning_over_queues_only_NPCs_with_something_from_that_day()
    {
        var saw = Npc(1);
        saw.Journal.Add(Seen("The traveller arrived at Medows", day: 3, eventId: 1));
        var talked = Npc(2);
        talked.Journal.Add(new Transcript(1, 3, "Medows", new[] { new Participant(0, "Loki") }, new[] { "Loki: hi" }));
        var quiet = Npc(3);
        quiet.Journal.Add(Seen("Old news", day: 2, eventId: 2));

        _system.DayStarted(4, new NPC[] { saw, talked, quiet });

        Assert.Equal(2, _system.Queued);
        _system.Update();
        Assert.Equal(new[] { "Test" , "Test" }, _asked.Select(a => a.req.npc_name));
    }

    [Fact]
    public void A_day_already_reflected_on_is_not_queued_again()
    {
        var npc = Npc(1);
        npc.Journal.Add(Seen("Something", day: 3, eventId: 1));
        npc.Journal.LastReflectedDay = 3;   // the console's `reflect` got there first

        _system.DayStarted(4, new NPC[] { npc });

        Assert.Equal(0, _system.Queued);
    }

    [Fact]
    public void The_same_NPC_and_day_is_only_queued_once()
    {
        var npc = Npc(1);

        Assert.True(_system.Queue(npc, 3));
        Assert.False(_system.Queue(npc, 3));
        _system.Update();
        Assert.False(_system.Queue(npc, 3));   // in flight now

        Assert.Single(_asked);
    }

    // ------------------------------------------------------------------ 2. start

    [Fact]
    public void No_more_than_two_calls_are_in_flight_at_once()
    {
        var npcs = Enumerable.Range(1, 5).Select(id => Npc(id)).ToList();
        foreach (var npc in npcs) _system.Queue(npc, 3);

        _system.Update();
        Assert.Equal((2, 3), (_system.InFlight, _system.Queued));

        _asked[0].answer.SetResult(Answer("One."));
        _system.Update();   // one lands, one more starts
        Assert.Equal((2, 2), (_system.InFlight, _system.Queued));
        Assert.Equal(3, _asked.Count);
    }

    [Fact]
    public void The_request_has_the_days_events_and_conversation_summaries_and_nothing_from_other_days()
    {
        var npc = Npc(1, "Sir Lancelot");
        npc.Persona = "A weary knight.";
        npc.Journal.Add(Seen("Sir Lancelot couldn't reach the well", day: 3, eventId: 1));
        npc.Journal.Add(Seen("Yesterday's news", day: 2, eventId: 2));
        var hello = new Transcript(1, 3, "Medows", new[] { new Participant(0, "Loki") }, new[] { "Loki: hi" });
        npc.Journal.Add(hello);
        npc.ApplyDecision(new NpcDecisionDto("Loki said hello.", null, "resume", false, null), null, hello);   // labels it
        npc.Journal.Add(new Transcript(2, 3, "Medows", new[] { new Participant(0, "Loki") }, new[] { "Loki: bye" }));   // no summary yet
        _system.Queue(npc, 3);

        _system.Update();

        var req = Assert.Single(_asked).req;
        Assert.Equal(("Sir Lancelot", "A weary knight.", "Spring 3, Year 1"), (req.npc_name, req.persona, req.date));
        Assert.Equal(new[] { "Sir Lancelot couldn't reach the well" }, req.events);
        Assert.Equal(new[] { "Loki said hello." }, req.conversations);
    }

    // ------------------------------------------------------------------ 3. apply

    [Fact]
    public void An_answer_adds_a_diary_entry_and_facts_with_the_day_and_drops_the_days_events()
    {
        var npc = Npc(1);
        var seen = Seen("Sir Lancelot couldn't reach the well", day: 3, eventId: 1);
        npc.Journal.Add(seen);
        _system.Queue(npc, 3);
        _system.Update();

        var later = Seen("Seen while the call was out", day: 3, eventId: 2);
        npc.Journal.Add(later);
        _asked[0].answer.SetResult(Answer("  I never reached the well.  ", "The well path is blocked", " ", "a", "b", "c"));
        _system.Update();

        Assert.Equal(new DayEntry(3, "I never reached the well."), Assert.Single(npc.Journal.Days));
        var facts = npc.Journal.Memories.Where(m => m.Kind == MemoryKind.Reflection).ToList();
        Assert.Equal(new[] { "The well path is blocked", "a", "b" }, facts.Select(f => f.Text));   // blanks dropped, at most 3
        Assert.All(facts, f => Assert.Equal(3, f.Day));
        Assert.Equal(new[] { later }, npc.Journal.EventsOn(3));   // only what the request was written from left
        Assert.Equal(3, npc.Journal.LastReflectedDay);
        Assert.Equal(0, _system.InFlight);
    }

    [Fact]
    public void A_failed_call_changes_nothing()
    {
        var npc = Npc(1);
        var seen = Seen("Something", day: 3, eventId: 1);
        npc.Journal.Add(seen);
        _system.Queue(npc, 3);
        _system.Update();

        _asked[0].answer.SetException(new HttpRequestException("backend down"));
        _system.Update();

        Assert.Empty(npc.Journal.Days);
        Assert.Equal(new[] { seen }, npc.Journal.Memories);
        Assert.Equal(0, npc.Journal.LastReflectedDay);
        Assert.Equal(0, _system.InFlight);
    }

    [Fact]
    public void Reflected_facts_count_as_facts_in_conversation_and_the_diary_is_recalled()
    {
        var npc = Npc(1);
        npc.Journal.Add(Seen("Something", day: 3, eventId: 1));
        _system.Queue(npc, 3);
        _system.Update();
        _asked[0].answer.SetResult(Answer("I waited at the castle door.", "The well path is blocked"));
        _system.Update();

        Assert.Equal(new[] { "The well path is blocked" }, npc.RecentMemories(5));
        Assert.Equal(new[] { "Yesterday: I waited at the castle door." }, npc.RecentDays(3, today: 4));
    }

    // ------------------------------------------------------------------ after a load

    [Fact]
    public void Catching_up_after_a_load_queues_missed_days_oldest_first_back_to_the_limit()
    {
        var npc = Npc(1);
        for (int day = 1; day <= 6; day++) npc.Journal.Add(Seen($"Day {day}", day, eventId: day));
        npc.Journal.LastReflectedDay = 2;

        _system.CatchUp(new NPC[] { npc }, today: 6);   // days 3, 4, 5 missed; only 4 and 5 are in reach
        _system.Update();

        Assert.Equal(new[] { "Spring 4, Year 1", "Spring 5, Year 1" }, _asked.Select(a => a.req.date));
    }

    [Fact]
    public void Catching_up_skips_days_with_nothing_in_them_and_never_today()
    {
        var npc = Npc(1);
        npc.Journal.Add(Seen("Day 4", day: 4, eventId: 1));
        npc.Journal.Add(Seen("Today", day: 6, eventId: 2));
        npc.Journal.LastReflectedDay = 3;

        _system.CatchUp(new NPC[] { npc }, today: 6);

        Assert.Equal(1, _system.Queued);
        _system.Update();
        _asked[0].answer.SetResult(Answer("Day four."));
        _system.Update();
        Assert.Equal(4, npc.Journal.LastReflectedDay);
    }

    // ------------------------------------------------------------------ opinions

    // A knight (a guard) who saw a bandit attack him, and heard a rumour about the player.
    private (TestNpc knight, TestNpc bandit, Ashenveil.Core.Entities.Player player) DayWithABanditAndARumour()
    {
        var player = new Ashenveil.Core.Entities.Player(Microsoft.Xna.Framework.Vector2.Zero, 0);
        player.SeedStanding(FactionTable.Default);
        var knight = Npc(1, "Sir Lancelot");
        knight.Factions.Add("guard");
        knight.Opinions.Seed(knight.Factions, FactionTable.Default);
        var bandit = Npc(2, "Grim");
        bandit.Factions.Add("bandit");

        var log = new EventLog(() => 3);
        var attack = log.Post(GameEvent.Of(EventType.Attacked, "Medows", default, bandit.Id, knight.Id, "Grim attacked Sir Lancelot"));
        var theft = log.Post(GameEvent.Of(EventType.TaskDone, "Medows", default, player.Id, null, "Traveller robbed the miller"));
        knight.Journal.Add(Seen(attack.Text, day: 3, eventId: attack.Id));
        knight.Journal.Add(new Memory(theft.Text, MemoryKind.Rumour) { Day = 3, SourceId = theft.Id, TellerId = 9, Hops = 1 });

        var people = new Dictionary<int, Ashenveil.Core.Entities.Entity> { [0] = player, [1] = knight, [2] = bandit };
        _system.Events = log;
        _system.FindPerson = id => people.GetValueOrDefault(id);
        _system.Queue(knight, 3);
        _system.Update();
        return (knight, bandit, player);
    }

    private static ReflectionDto Changes(params (string about, int by)[] changes) => new()
    {
        diary = "A hard day.",
        facts = Array.Empty<string>(),
        opinion_changes = changes.Select(c => new OpinionChangeDto { about = c.about, by = c.by, why = $"because {c.about}" }).ToArray(),
    };

    [Fact]
    public void The_request_names_who_the_day_was_about_and_their_factions_with_keys()
    {
        DayWithABanditAndARumour();

        var req = Assert.Single(_asked).req;
        Assert.Equal(new[] { ("person:2", "Grim", -80), ("person:0", "Traveller", 0) },
                     req.people.Select(k => (k.key, k.name, k.opinion)));
        Assert.Equal(new[] { "faction:bandit", "faction:traveller" }, req.factions.Select(k => k.key));
        Assert.Equal(new[] { "Someone told you: Traveller robbed the miller" }, req.rumours);   // the teller isn't known here
        Assert.DoesNotContain(req.people, k => k.key == "person:1");                           // never itself
    }

    [Fact]
    public void Valid_changes_are_applied_and_a_change_about_a_player_moves_their_standing()
    {
        var (knight, bandit, player) = DayWithABanditAndARumour();
        int standing = player.StandingWith("guard");

        _asked[0].answer.SetResult(Changes(("person:2", -10), ("faction:bandit", -5), ("person:0", -12)));
        _system.Update();

        Assert.Equal(-90, knight.OpinionOf(bandit));
        Assert.Equal("because person:2", knight.Opinions.People[2].Why);
        Assert.Equal(-85, knight.Opinions.OfFaction("bandit"));
        Assert.Equal(-12, knight.OpinionOf(player));
        Assert.Equal(standing - 12 / ReflectionSystem.StandingShare, player.StandingWith("guard"));
    }

    [Fact]
    public void Changes_about_anyone_not_in_the_day_are_dropped()
    {
        var (knight, _, _) = DayWithABanditAndARumour();

        _asked[0].answer.SetResult(Changes(("person:1", 50), ("person:77", -30), ("faction:village", -30), ("nonsense", 5)));
        _system.Update();

        Assert.Empty(knight.Opinions.People);
        Assert.Equal(40, knight.Opinions.OfFaction("village"));
        Assert.Single(knight.Journal.Days);   // the rest of the reflection still lands
    }

    [Fact]
    public void Changes_are_capped_lower_for_what_was_only_heard_and_repeats_share_the_cap()
    {
        var (knight, bandit, player) = DayWithABanditAndARumour();
        knight.Opinions.SetFaction("bandit", -20);   // room to move before hitting -100

        _asked[0].answer.SetResult(Changes(("person:2", -30), ("person:2", -30), ("person:0", -40)));
        _system.Update();

        Assert.Equal(-20 - ReflectionSystem.SeenCap, knight.Opinions.People[2].Score);   // -60 asked, 40 allowed
        Assert.Equal(-ReflectionSystem.HeardCap, knight.OpinionOf(player));             // only a rumour about him
    }

    [Fact]
    public void A_failed_call_changes_no_opinions()
    {
        var (knight, _, player) = DayWithABanditAndARumour();
        int standing = player.StandingWith("guard");

        _asked[0].answer.SetException(new HttpRequestException("down"));
        _system.Update();

        Assert.Empty(knight.Opinions.People);
        Assert.Equal(standing, player.StandingWith("guard"));
        Assert.Equal(2, knight.Journal.Memories.Count);   // the event and the rumour are still there
    }
}
