using Ashenveil.Core;
using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Task;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// The event log on its own (ids, order, Posted), then wired into a World built from a test
/// level: who perceives an event, the events tasks, doors and chats post, and the console
/// commands that read the log and move the clock. No LLM: every call is a fake.
/// </summary>
public class EventLogTests : IDisposable
{
    // A 5x5 field with the player's spawn in the middle and a well in the bottom-right corner.
    private const string Level = """
        {
          "cols": 5, "rows": 5,
          "tiles": [["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"]],
          "pois": [ { "name": "Player_spawn", "x": 2, "y": 2 }, { "name": "well", "x": 4, "y": 4 } ]
        }
        """;

    private readonly List<ReflectionRequestDto> _reflectionsAsked = new();

    public EventLogTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = new FakeInput();
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private World NewWorld()
    {
        var world = new World(TestLevels.Location(Level),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(new NpcDecisionDto("Met.", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>())),
            requestReflection: req => { _reflectionsAsked.Add(req); return Task.FromResult(new ReflectionDto { diary = "A day.", facts = Array.Empty<string>() }); });
        world.Init();
        return world;
    }

    // An NPC with its feet on `feet`, in the level being played. Ids from 10 up, clear of the player's 0.
    private static (TestNpc npc, WanderBrain brain) NpcAt(World world, Vector2 feet, int id, string name = "Test")
    {
        var brain = new WanderBrain();
        var npc = new TestNpc(0, 0, id, name, brain);
        npc.StandAt(feet);
        world.AddNpc(npc);
        return (npc, brain);
    }

    private static GameEvent Something(string level, Vector2 where, int? actor = null, int? target = null) =>
        GameEvent.Of(EventType.TaskDone, level, where, actor, target, "Something happened");

    private static IEnumerable<Memory> EventsOf(NpcBrain brain) => brain.Journal.Memories.Where(m => m.Kind == MemoryKind.Event);

    // ------------------------------------------------------------------ the log

    [Fact]
    public void Ids_count_up_from_one_and_every_event_is_stamped_with_today()
    {
        int today = 4;
        var log = new EventLog(() => today);

        var first = log.Post(Something("Medows", Vector2.Zero));
        today = 5;
        var second = log.Post(Something("Medows", Vector2.Zero) with { Id = 99, Day = 1 });   // ignored: the log decides

        Assert.Equal((1, 4), (first.Id, first.Day));
        Assert.Equal((2, 5), (second.Id, second.Day));
        Assert.Equal(3, log.PeekNextId);
    }

    [Fact]
    public void Posted_is_raised_once_per_event_after_it_is_perceived()
    {
        var order = new List<string>();
        var log = new EventLog(() => 1, e => order.Add($"perceived {e.Id}"));
        log.Posted += e => order.Add($"posted {e.Id}");

        log.Post(Something("Medows", Vector2.Zero));
        log.Post(Something("Medows", Vector2.Zero));

        Assert.Equal(new[] { "perceived 1", "posted 1", "perceived 2", "posted 2" }, order);
    }

    [Fact]
    public void The_log_keeps_every_event_in_order()
    {
        var log = new EventLog(() => 1);
        for (int i = 0; i < 12; i++) log.Post(Something("Medows", Vector2.Zero));

        Assert.Equal(Enumerable.Range(1, 12), log.All.Select(e => e.Id));
        Assert.Equal(new[] { 10, 11, 12 }, log.Recent(3).Select(e => e.Id));
        Assert.Equal(7, log.Find(7)!.Id);
    }

    [Fact]
    public void A_restored_log_carries_on_from_the_saved_id()
    {
        var log = new EventLog(() => 3);
        log.Restore(8, new[] { new GameEvent(7, 2, "Medows", Vector2.Zero, EventType.Talked, 0, 1, "Loki talked with Sir Lancelot") });

        Assert.Equal(8, log.Post(Something("Medows", Vector2.Zero)).Id);
        Assert.Equal(2, log.All.Count);
    }

    // ------------------------------------------------------------------ perception

    [Fact]
    public void The_actor_and_the_target_always_know_however_far_away()
    {
        var world = NewWorld();
        var (_, actor) = NpcAt(world, new Vector2(0.5f, 0.9f), 10);
        var (_, target) = NpcAt(world, new Vector2(100, 100), 11);   // far out of range

        world.Events.Post(Something(world.location.Name, new Vector2(0.5f, 0.9f), actor: 10, target: 11));

        Assert.Single(EventsOf(actor));
        Assert.Single(EventsOf(target));
    }

    [Fact]
    public void Only_bystanders_in_range_and_in_the_same_level_know()
    {
        var world = NewWorld();
        var where = new Vector2(2.5f, 2.9f);
        var (_, near) = NpcAt(world, where + new Vector2(Perception.Range - 0.5f, 0), 10);
        var (_, far) = NpcAt(world, where + new Vector2(Perception.Range + 0.5f, 0), 11);
        var elsewhereBrain = new WanderBrain();
        var elsewhere = new TestNpc(0, 0, 12, "Elsewhere", elsewhereBrain);
        elsewhere.StandAt(where);   // the same spot, but in another level
        world.Roster.Add(TestLevels.Location(Level), elsewhere);

        world.Events.Post(Something(world.location.Name, where));

        Assert.Single(EventsOf(near));
        Assert.Empty(EventsOf(far));
        Assert.Empty(EventsOf(elsewhereBrain));
    }

    [Fact]
    public void A_perceived_event_is_a_memory_with_its_day_and_id()
    {
        var world = NewWorld();
        world.Clock.SkipDays(2);   // day 3
        var (_, brain) = NpcAt(world, new Vector2(2.5f, 2.9f), 10);

        var posted = world.Events.Post(Something(world.location.Name, new Vector2(2.5f, 2.9f)));

        var memory = Assert.Single(EventsOf(brain));
        Assert.Equal("Something happened", memory.Text);
        Assert.Equal(3, memory.Day);
        Assert.Equal(posted.Id, memory.SourceId);
    }

    // ------------------------------------------------------------------ task events

    private static void Tick(NpcBrain brain, int times = 1)
    {
        for (int i = 0; i < times; i++) brain.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void A_finished_trip_posts_TaskDone_naming_the_place()
    {
        var world = NewWorld();
        var well = world.location.Poi("well")!.StandingSpot();
        var (npc, brain) = NpcAt(world, well, 10);
        npc.GiveTask(new GoToTask(well));   // already there: done on the first tick

        Tick(brain);

        var e = Assert.Single(world.Events.All);
        Assert.Equal(EventType.TaskDone, e.Type);
        Assert.Equal("Test reached the well", e.Text);
        Assert.Equal(10, e.ActorId);
        Assert.Contains(EventsOf(brain), m => m.SourceId == e.Id);   // the NPC remembers its own trip
    }

    [Fact]
    public void A_trip_that_cannot_get_there_posts_TaskFailed()
    {
        var world = NewWorld();
        var (npc, brain) = NpcAt(world, new Vector2(0.5f, 0.9f), 10);
        npc.GiveTask(new GoToTask(world.location.Poi("well")!.StandingSpot()));

        // Ticking the brain alone never moves the body, so the trip gets stuck and gives up.
        Tick(brain, 20);

        var e = Assert.Single(world.Events.All);
        Assert.Equal(EventType.TaskFailed, e.Type);
        Assert.Equal("Test couldn't reach the well", e.Text);
    }

    [Fact]
    public void A_wait_is_never_news()
    {
        var world = NewWorld();
        var (npc, brain) = NpcAt(world, new Vector2(0.5f, 0.9f), 10);
        npc.GiveTask(new WaitTask(1f));

        Tick(brain, 3);

        Assert.Empty(world.Events.All);
    }

    // ------------------------------------------------------------------ doors and chats

    private const string LevelWithDoor = """
        {
          "cols": 5, "rows": 5,
          "tiles": [["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"]],
          "exits": [ { "col": 2, "row": 0, "to": "Medows", "arriveAt": "entry" } ],
          "pois": [ { "name": "Player_spawn", "x": 2, "y": 2 } ]
        }
        """;

    [Fact]
    public void Using_a_door_posts_leaving_one_level_and_arriving_at_the_next()
    {
        var world = new World(TestLevels.Location(LevelWithDoor),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult<NpcDecisionDto>(null!),
            requestReflection: _ => Task.FromResult<ReflectionDto>(null!));
        world.Init();
        string from = world.location.Name;

        world.UseExit(world.location.ExitAt(2, 0));

        Assert.Collection(world.Events.All,
            left =>
            {
                Assert.Equal((EventType.LeftLevel, from, (int?)0), (left.Type, left.Level, left.ActorId));
                Assert.Equal($"Traveller left {from}", left.Text);
            },
            arrived =>
            {
                Assert.Equal((EventType.EnteredLevel, "Medows"), (arrived.Type, arrived.Level));
                Assert.Equal("Traveller arrived at Medows", arrived.Text);
                Near.Equal(world.player.Feet, arrived.Where);
            });
    }

    [Fact]
    public void A_chat_the_player_spoke_in_posts_Talked_pointing_at_its_transcript()
    {
        var world = NewWorld();
        var (npc, brain) = NpcAt(world, world.player.Feet + new Vector2(0.5f, 0), 10, "Sir Lancelot");

        var convo = world.StartConversation(world.player, npc);
        convo.Add(world.player.Name, "Hello.");
        convo.End();
        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1 / 60.0)));

        var e = Assert.Single(world.Events.All);
        var transcript = Assert.Single(brain.Journal.Transcripts);
        Assert.Equal((EventType.Talked, (int?)0, (int?)10, (int?)transcript.Id), (e.Type, e.ActorId, e.TargetId, e.TranscriptId));
        Assert.Equal("Traveller talked with Sir Lancelot", e.Text);
        Assert.Contains(EventsOf(brain), m => m.SourceId == e.Id);
    }

    [Fact]
    public void A_chat_where_nobody_but_the_NPC_spoke_posts_nothing()
    {
        var world = NewWorld();
        var (npc, _) = NpcAt(world, world.player.Feet + new Vector2(0.5f, 0), 10);

        world.StartConversation(world.player, npc).End();
        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1 / 60.0)));

        Assert.Empty(world.Events.All);
    }

    // ------------------------------------------------------------------ console

    private static CommandRegistry Console(World world)
    {
        var commands = new CommandRegistry();
        WorldCommands.Register(commands, world);
        return commands;
    }

    private static string[] Run(CommandRegistry commands, string input) =>
        commands.Execute(input).Select(l => l.Text).ToArray();

    [Fact]
    public void Events_lists_the_last_n_with_ids_and_says_when_there_are_none()
    {
        var world = NewWorld();
        var commands = Console(world);
        Assert.Equal(new[] { "Nothing has happened yet." }, Run(commands, "events"));

        for (int i = 0; i < 12; i++) world.Events.Post(Something(world.location.Name, Vector2.Zero));

        Assert.Equal(10, Run(commands, "events").Length);
        Assert.Equal(new[] { $"#11 day 1, {world.location.Name}: Something happened", $"#12 day 1, {world.location.Name}: Something happened" },
                     Run(commands, "events 2"));
        Assert.Equal(LineKind.Error, Assert.Single(commands.Execute("events none")).Kind);
    }

    [Fact]
    public void Time_shows_the_date_and_the_time_of_day()
    {
        var world = NewWorld();
        world.Clock.SkipHours(6.5f);

        Assert.Equal(new[] { "Spring 1, Year 1 (day 1), 06:30" }, Run(Console(world), "time"));
    }

    [Fact]
    public void Time_plus_a_day_starts_the_next_day_raising_DayStarted_once()
    {
        var world = NewWorld();
        world.Clock.SkipHours(5);
        var started = new List<int>();
        world.Clock.DayStarted += started.Add;

        var lines = Run(Console(world), "time +1d");

        Assert.Equal(new[] { 2 }, started);
        Assert.Equal(new[] { "Spring 2, Year 1 (day 2), 00:00" }, lines);
    }

    [Fact]
    public void Time_plus_hours_crosses_midnight_like_play_does()
    {
        var world = NewWorld();
        var commands = Console(world);
        var started = new List<int>();
        world.Clock.DayStarted += started.Add;

        Run(commands, "time +20h");
        Assert.Empty(started);
        Assert.Equal(new[] { "Spring 2, Year 1 (day 2), 02:00" }, Run(commands, "time +6h"));
        Assert.Equal(new[] { 2 }, started);
    }

    [Theory]
    [InlineData("1d")]
    [InlineData("+d")]
    [InlineData("+0d")]
    [InlineData("+1.5d")]
    [InlineData("+2w")]
    [InlineData("+-3h")]
    public void Time_refuses_a_skip_it_cannot_read(string skip)
    {
        var world = NewWorld();

        Assert.Equal(LineKind.Error, Assert.Single(Console(world).Execute($"time {skip}")).Kind);
        Assert.Equal(1, world.Clock.TotalDays);
    }

    [Fact]
    public void Reflect_queues_today_and_the_answer_lands_once_the_game_runs()
    {
        var world = NewWorld();
        var (_, brain) = NpcAt(world, new Vector2(2.5f, 2.9f), 10);
        world.Events.Post(Something(world.location.Name, new Vector2(2.5f, 2.9f)));

        Assert.Contains("will reflect on today", Assert.Single(Run(Console(world), "reflect 10")));
        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1 / 60.0)));   // asks
        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1 / 60.0)));   // applies

        Assert.Equal(new[] { "Something happened" }, Assert.Single(_reflectionsAsked).events);
        Assert.Equal(new DayEntry(1, "A day."), Assert.Single(brain.Journal.Days));
        Assert.Empty(EventsOf(brain));   // reflected on: out of the hot list...
        Assert.Single(world.Events.All); // ...but still in the log
    }

    [Fact]
    public void Reflect_on_an_NPC_with_nothing_to_reflect_on_says_so()
    {
        var world = NewWorld();
        NpcAt(world, new Vector2(2.5f, 2.9f), 10);

        var line = Assert.Single(Console(world).Execute("reflect 10"));

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("Nothing has happened", line.Text);
    }
}
