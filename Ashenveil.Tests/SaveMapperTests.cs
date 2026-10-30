using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Jobs;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Saving;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// SaveMapper on its own, no database: a World built from a level is turned into save
/// classes, and NPCs (journals, shared transcripts, task queues) are rebuilt from them.
/// Building a whole World from a save comes with World's load path.
/// </summary>
public class SaveMapperTests : IDisposable
{
    // A 5x5 field with the player's spawn in the middle cell.
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

    private static readonly DateTime SavedAt = new(2026, 10, 27, 9, 30, 0, DateTimeKind.Utc);

    private readonly World _world;

    public SaveMapperTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = new FakeInput();   // no keys held: the player stands still
        _world = new World(TestLevels.Location(Level),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(new NpcDecisionDto("x", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>())));
        _world.Init();
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    // An NPC in the level, with an id from the roster as the game hands them out.
    private TestNpc AddNpc(float x, float y)
    {
        var npc = new TestNpc(x, y, _world.Roster.NextId());
        _world.AddNpc(npc);
        return npc;
    }

    private static Transcript Talk(int id, params (int id, string name)[] who) =>
        new(id, day: 2, "Medows", who.Select(p => new Participant(p.id, p.name)).ToList(), new[] { "Traveller: my name is Loki" });

    // Summary has an internal setter, so it's filled the way the game does: by a decision about that transcript.
    private static void Summarise(NPC npc, Transcript t, string summary) =>
        npc.ApplyDecision(new NpcDecisionDto(summary, null, "resume", false, Array.Empty<TaskSpecDto>()), null, t);

    // ------------------------------------------------------------------ game -> save

    [Fact]
    public void A_save_holds_the_clock_the_id_counters_and_the_populated_levels()
    {
        AddNpc(1, 1);
        AddNpc(3, 1);
        _world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(30)));

        var save = SaveMapper.ToSave(_world, SavedAt);

        Assert.Equal(SavedAt, save.SavedAt);
        Assert.Equal(_world.Clock.TotalDays, save.TotalDays);
        Near.Equal(30f, save.SecondsIntoDay);
        Assert.Equal(3, save.NextPersonId);       // ids 1 and 2 are taken
        Assert.Equal(1, save.NextTranscriptId);   // no conversations yet
        Assert.Equal(new[] { _world.location.Name }, save.PopulatedLevels);
    }

    [Fact]
    public void A_save_holds_the_player_by_id_and_name_with_their_level_and_feet()
    {
        _world.player.Name = "Loki";

        var player = Assert.Single(SaveMapper.ToSave(_world, SavedAt).Players);

        Assert.Equal(0, player.Id);
        Assert.Equal("Loki", player.Name);
        Assert.Equal(_world.location.Name, player.Level);
        Near.Equal(_world.player.Feet, new Vector2(player.X, player.Y));
    }

    [Fact]
    public void A_save_holds_each_NPC_with_its_type_level_and_feet()
    {
        var npc = AddNpc(1, 3);

        var saved = Assert.Single(SaveMapper.ToSave(_world, SavedAt).Npcs);

        Assert.Equal(npc.Id, saved.Id);
        Assert.Equal(nameof(TestNpc), saved.Type);
        Assert.Equal("Test", saved.Name);
        Assert.Equal(_world.location.Name, saved.Level);
        Near.Equal(npc.Feet, new Vector2(saved.X, saved.Y));
    }

    [Fact]
    public void A_conversation_two_NPCs_shared_is_saved_once_and_both_journals_list_it()
    {
        var a = AddNpc(1, 1);
        var b = AddNpc(3, 1);
        var shared = Talk(4, (0, "Traveller"), (a.Id, "Test"), (b.Id, "Test"));
        a.Journal.Add(shared);
        b.Journal.Add(shared);

        var save = SaveMapper.ToSave(_world, SavedAt);

        Assert.Equal(4, Assert.Single(save.Transcripts).Id);
        Assert.All(save.Npcs, npc => Assert.Equal(new[] { 4 }, npc.TranscriptIds));
    }

    // ------------------------------------------------------------------ save -> game

    [Fact]
    public void Memories_and_conversations_come_back_in_order_with_their_summary_and_source()
    {
        var npc = AddNpc(1, 1);
        var first = Talk(1, (0, "Traveller"), (npc.Id, "Test"));
        var second = Talk(2, (0, "Traveller"), (npc.Id, "Test"));
        npc.Journal.Add(first);
        npc.Journal.Add(second);
        Summarise(npc, first, "Met Loki.");
        npc.Journal.Add(new Memory("The traveller's name is Loki.", MemoryKind.Conversation) { SourceId = 1 });
        npc.Journal.Add(new Memory("Saw wolves by the well.", MemoryKind.Event));

        var save = SaveMapper.ToSave(_world, SavedAt);
        var journal = new Journal();
        SaveMapper.FillJournal(journal, save.Npcs[0], SaveMapper.TranscriptsFrom(save));

        Assert.Equal(new[] { 1, 2 }, journal.Transcripts.Select(t => t.Id));
        var back = journal.Transcripts[0];
        Assert.Equal("Met Loki.", back.Summary);
        Assert.Null(journal.Transcripts[1].Summary);
        Assert.Equal(first.Day, back.Day);
        Assert.Equal(first.Location, back.Location);
        Assert.Equal(first.Participants, back.Participants);
        Assert.Equal(first.Lines, back.Lines);

        Assert.Collection(journal.Memories,
            m => { Assert.Equal("The traveller's name is Loki.", m.Text); Assert.Equal(MemoryKind.Conversation, m.Kind); Assert.Equal(1, m.SourceId); },
            m => { Assert.Equal("Saw wolves by the well.", m.Text); Assert.Equal(MemoryKind.Event, m.Kind); Assert.Null(m.SourceId); });
    }

    [Fact]
    public void A_shared_transcript_is_one_object_again_after_loading()
    {
        var a = AddNpc(1, 1);
        var b = AddNpc(3, 1);
        var shared = Talk(4, (0, "Traveller"), (a.Id, "Test"), (b.Id, "Test"));
        a.Journal.Add(shared);
        b.Journal.Add(shared);
        var save = SaveMapper.ToSave(_world, SavedAt);

        var transcripts = SaveMapper.TranscriptsFrom(save);
        Journal journalA = new(), journalB = new();
        SaveMapper.FillJournal(journalA, save.Npcs[0], transcripts);
        SaveMapper.FillJournal(journalB, save.Npcs[1], transcripts);

        Assert.Same(Assert.Single(journalA.Transcripts), Assert.Single(journalB.Transcripts));
    }

    // ------------------------------------------------------------------ NPCs and their tasks

    // A knight in the level, built the way the game builds one.
    private Knight AddKnight(float x, float y)
    {
        var knight = new Knight(new Vector2(x, y), _world.Roster.NextId(), "Sir Lancelot", new KnightBrain());
        _world.AddNpc(knight);
        return knight;
    }

    // Saves the world and rebuilds its only NPC from the save.
    private NPC SaveAndRebuildOnlyNpc()
    {
        var save = SaveMapper.ToSave(_world, SavedAt);
        return SaveMapper.FromSave(Assert.Single(save.Npcs), SaveMapper.TranscriptsFrom(save));
    }

    // Runs a task for `seconds` outside any brain, as if the NPC had been working on it.
    private static void Run(IJob task, NPC owner, float seconds) =>
        task.Execute(new JobContext { Owner = owner, Blackboard = new Blackboard(), DeltaTime = seconds });

    [Fact]
    public void A_saved_knight_comes_back_as_a_knight_with_its_id_name_feet_and_memories()
    {
        var knight = AddKnight(1, 1);
        knight.Journal.Add(new Memory("The traveller's name is Loki.", MemoryKind.Conversation));

        var back = Assert.IsType<Knight>(SaveAndRebuildOnlyNpc());

        Assert.Equal(knight.Id, back.Id);
        Assert.Equal("Sir Lancelot", back.Name);
        Near.Equal(knight.Feet, back.Feet);
        Assert.Equal("The traveller's name is Loki.", Assert.Single(back.Journal.Memories).Text);
    }

    [Fact]
    public void A_task_queue_comes_back_in_order_with_the_waits_time_left()
    {
        var knight = AddKnight(1, 1);
        var wait = new WaitJob(5f);
        Run(wait, knight, 2f);   // 3 seconds still to go
        knight.GiveJob(new GoToJob(new Vector2(3.5f, 1.9f)));
        knight.GiveJob(wait);
        knight.GiveJob(new GoToJob(new Vector2(1.5f, 1.9f)));

        var tasks = SaveAndRebuildOnlyNpc().Jobs.ToList();

        Assert.Collection(tasks,
            t => Near.Equal(new Vector2(3.5f, 1.9f), Assert.IsType<GoToJob>(t).Target),
            t => Near.Equal(3f, Assert.IsType<WaitJob>(t).SecondsLeft),
            t => Near.Equal(new Vector2(1.5f, 1.9f), Assert.IsType<GoToJob>(t).Target));
        Assert.All(tasks, t => Assert.Equal(JobState.Inactive, t.State));   // starts afresh: a go_to replans
    }

    [Fact]
    public void A_follow_comes_back_following_the_same_person()
    {
        var knight = AddKnight(1, 1);
        knight.GiveJob(new WaitJob(2f));
        knight.GiveJob(new FollowJob(targetId: 7));

        var tasks = SaveAndRebuildOnlyNpc().Jobs.ToList();

        Assert.IsType<WaitJob>(tasks[0]);
        Assert.Equal(7, Assert.IsType<FollowJob>(tasks[1]).TargetId);
    }

    [Fact]
    public void A_follow_saved_with_nobody_to_follow_is_left_out()
    {
        var saved = new NpcSave
        {
            Type = nameof(Knight), Name = "Sir Lancelot",
            Tasks = { new TaskSave { Type = "follow" }, new TaskSave { Type = "wait", Seconds = 4 } },
        };

        var npc = SaveMapper.FromSave(saved, new Dictionary<int, Transcript>());

        Assert.IsType<WaitJob>(Assert.Single(npc.Jobs));
    }

    [Fact]
    public void An_NPC_with_jobs_loads_as_Tasked()
    {
        AddKnight(1, 1).GiveJob(new WaitJob(3f));

        Assert.Equal(NpcBrain.NPCState.Working, SaveAndRebuildOnlyNpc().State);
    }

    [Fact]
    public void An_NPC_without_tasks_loads_in_its_types_usual_state()
    {
        var knight = AddKnight(1, 1);

        Assert.Equal(knight.State, SaveAndRebuildOnlyNpc().State);   // a knight stands idle
    }

    [Fact]
    public void An_NPC_of_a_type_the_game_does_not_know_is_left_out()
    {
        Assert.Null(SaveMapper.FromSave(new NpcSave { Type = "Dragon", Name = "Smaug" }, new Dictionary<int, Transcript>()));
    }

    [Fact]
    public void A_task_of_an_unknown_type_or_missing_its_values_is_left_out_and_the_rest_load()
    {
        var saved = new NpcSave
        {
            Type = nameof(Knight), Name = "Sir Lancelot",
            Tasks =
            {
                new TaskSave { Type = "patrol" },              // not a type this game has
                new TaskSave { Type = "go_to", X = 1 },        // no Y
                new TaskSave { Type = "wait", Seconds = 4 },
            },
        };

        var npc = SaveMapper.FromSave(saved, new Dictionary<int, Transcript>());

        Near.Equal(4f, Assert.IsType<WaitJob>(Assert.Single(npc.Jobs)).SecondsLeft);
    }

    [Fact]
    public void A_wait_counts_down_its_time_left_and_never_goes_below_zero()
    {
        var npc = AddKnight(1, 1);
        var wait = new WaitJob(2f);

        Run(wait, npc, 0.5f);
        Near.Equal(1.5f, wait.SecondsLeft);
        Run(wait, npc, 5f);
        Assert.Equal(0f, wait.SecondsLeft);
    }

    [Fact]
    public void A_journal_entry_whose_transcript_is_missing_is_skipped_and_the_rest_is_kept()
    {
        var saved = new NpcSave
        {
            Memories = { new MemorySave { Text = "Still remembered.", Kind = "Event" } },
            TranscriptIds = { 99 },   // not in the save
        };
        var journal = new Journal();

        SaveMapper.FillJournal(journal, saved, new Dictionary<int, Transcript>());

        Assert.Empty(journal.Transcripts);
        Assert.Equal("Still remembered.", Assert.Single(journal.Memories).Text);
    }
}
