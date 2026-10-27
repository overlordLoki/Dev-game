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
}
