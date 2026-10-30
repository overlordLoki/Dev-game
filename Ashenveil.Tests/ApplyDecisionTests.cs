using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Jobs;

namespace Ashenveil.Tests;

/// <summary>
/// ApplyDecision turns an LLM decision (summary, memories, behaviour, tasks) into real
/// changes on the brain — a journal entry, a reshaped task queue, a new state. It must
/// distrust every field: unknown task types, bad POIs, nulls and blanks all degrade to
/// "nothing changed" rather than crashing. No LLM here — decisions are hand-built.
/// </summary>
public class ApplyDecisionTests
{
    // Two POIs the tasks can aim at: shop (3,0) and spawn (2,1).
    private const string Level = """
        {
          "cols": 4, "rows": 3,
          "tiles": [["grass","grass","grass","grass"],
                    ["grass","grass","grass","grass"],
                    ["grass","grass","grass","grass"]],
          "pois": [ { "name": "shop", "x": 3, "y": 0 },
                    { "name": "spawn", "x": 2, "y": 1 } ]
        }
        """;

    public ApplyDecisionTests() => NullContentManager.Install();

    // A fresh brain attached to a throwaway NPC (Attach needs an owner). The NPC stays
    // alive through brain.Npc, so returning the brain is enough.
    private static WanderBrain NewBrain()
    {
        var brain = new WanderBrain();
        _ = new TestNpc(0, 0, brain);
        return brain;
    }

    private static TaskSpecDto GoTo(string target) => new("go_to", target, 0f);
    private static TaskSpecDto Wait(float seconds) => new("wait", null, seconds);

    // --------------------------------------------------------------- the happy paths

    [Fact]
    public void Do_tasks_enqueues_every_valid_task_and_enters_Tasked()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "Agreed to run an errand.", new[] { "player wants supplies" },
            "do_tasks", clear_current_tasks: true,
            new[] { GoTo("shop"), Wait(5f), GoTo("spawn") });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Equal(3, brain.JobQueue.Count);
        Assert.Equal(NpcBrain.NPCState.Working, brain.State);
    }

    [Fact]
    public void An_ordinary_chat_writes_the_journal_without_tasking_the_npc()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "Chatted about the roads.", new[] { "the traveller asked about the roads" },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>());

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Empty(brain.JobQueue);
        Assert.NotEqual(NpcBrain.NPCState.Working, brain.State);
        var memory = Assert.Single(brain.Journal.Memories);
        Assert.Equal("the traveller asked about the roads", memory.Text);
        Assert.Equal(MemoryKind.Conversation, memory.Kind);
    }

    [Fact]
    public void Idle_behaviour_with_no_tasks_lands_in_Idle()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "Stood watch.", null, "idle", clear_current_tasks: true, System.Array.Empty<TaskSpecDto>());

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Equal(NpcBrain.NPCState.Idle, brain.State);
    }

    // --------------------------------------------------------------- the journal

    [Fact]
    public void Memories_are_stored_as_facts_with_blanks_dropped()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "Met the traveller.", new[] { "name is Aldric", "", "   ", "headed north" },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>());

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        var texts = brain.Journal.Memories.Select(m => m.Text).ToList();
        Assert.Equal(2, texts.Count);                       // the two blanks were filtered out
        Assert.Contains("name is Aldric", texts);
        Assert.Contains("headed north", texts);
    }

    [Fact]
    public void No_facts_means_no_memories_are_stored()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "", new[] { "", "  " }, "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>());

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Empty(brain.Journal.Memories);
    }

    [Fact]
    public void A_fact_already_remembered_is_not_stored_again()
    {
        var brain = NewBrain();
        var level = TestLevels.Location(Level);
        brain.ApplyDecision(new NpcDecisionDto(
            "x", new[] { "The traveller's name is Loki." },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()), level);

        // Same fact next conversation, different case and stray whitespace, plus a new one.
        brain.ApplyDecision(new NpcDecisionDto(
            "x", new[] { "  the traveller's name is loki. ", "The traveller is headed north." },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()), level);

        var texts = brain.Journal.Memories.Select(m => m.Text).ToList();
        Assert.Equal(2, texts.Count);
        Assert.Contains("The traveller's name is Loki.", texts);   // the original wording kept
        Assert.Contains("The traveller is headed north.", texts);
    }

    [Fact]
    public void Duplicates_within_one_decision_are_stored_once()
    {
        var brain = NewBrain();
        brain.ApplyDecision(new NpcDecisionDto(
            "x", new[] { "Loki is a traveller.", "LOKI IS A TRAVELLER." },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()),
            TestLevels.Location(Level));

        Assert.Single(brain.Journal.Memories);
    }

    // --------------------------------------------------------------- the transcript link

    private static Transcript NewTranscript(int id = 4) =>
        new(id, day: 1, "Medows", new[] { new Participant(0, "Traveller"), new Participant(1, "Test") }, new[] { "Traveller: my name is Loki" });

    [Fact]
    public void The_decision_summary_labels_the_transcript()
    {
        var brain = NewBrain();
        var transcript = NewTranscript();

        brain.ApplyDecision(new NpcDecisionDto(
            "  Met a traveller named Loki.  ", null,
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()),
            TestLevels.Location(Level), transcript);

        Assert.Equal("Met a traveller named Loki.", transcript.Summary);
    }

    [Fact]
    public void New_facts_point_back_to_the_transcript_they_came_from()
    {
        var brain = NewBrain();

        brain.ApplyDecision(new NpcDecisionDto(
            "x", new[] { "The traveller's name is Loki." },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()),
            TestLevels.Location(Level), NewTranscript(id: 9));

        Assert.Equal(9, Assert.Single(brain.Journal.Memories).SourceId);
    }

    [Fact]
    public void Without_a_transcript_facts_have_no_source()
    {
        var brain = NewBrain();

        brain.ApplyDecision(new NpcDecisionDto(
            "x", new[] { "Something happened." },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()),
            TestLevels.Location(Level));

        Assert.Null(Assert.Single(brain.Journal.Memories).SourceId);
    }

    [Fact]
    public void A_blank_summary_leaves_the_transcript_unlabelled()
    {
        var transcript = NewTranscript();

        NewBrain().ApplyDecision(new NpcDecisionDto(
            "   ", null, "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()),
            TestLevels.Location(Level), transcript);

        Assert.Null(transcript.Summary);
    }

    [Fact]
    public void In_a_shared_transcript_the_first_summary_sticks()
    {
        // Two NPCs were in the same conversation; both decisions land on one transcript.
        var transcript = NewTranscript();
        var level = TestLevels.Location(Level);

        NewBrain().ApplyDecision(new NpcDecisionDto(
            "First.", null, "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()),
            level, transcript);
        NewBrain().ApplyDecision(new NpcDecisionDto(
            "Second.", null, "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>()),
            level, transcript);

        Assert.Equal("First.", transcript.Summary);
    }

    // --------------------------------------------------------------- distrusting input

    [Fact]
    public void Unknown_task_types_are_dropped()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "x", null, "do_tasks", clear_current_tasks: true,
            new[] { new TaskSpecDto("fly", "shop", 0f), Wait(2f) });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Single(brain.JobQueue);   // only the wait survived
    }

    [Fact]
    public void Go_to_a_nonexistent_poi_is_dropped()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "x", null, "do_tasks", clear_current_tasks: true,
            new[] { GoTo("nowhere"), Wait(2f) });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Single(brain.JobQueue);   // "nowhere" dropped, the wait remains
    }

    [Fact]
    public void A_null_decision_is_a_no_op()
    {
        var brain = NewBrain();

        brain.ApplyDecision(null, TestLevels.Location(Level));   // must not throw

        Assert.Empty(brain.JobQueue);
        Assert.Empty(brain.Journal.Memories);
    }

    // --------------------------------------------------------------- clearing the queue

    [Fact]
    public void Clear_current_tasks_empties_the_queue_before_adding_new_ones()
    {
        var brain = NewBrain();
        brain.Enqueue(new WaitJob(1f));
        brain.Enqueue(new WaitJob(1f));          // two tasks already queued

        var decision = new NpcDecisionDto(
            "x", null, "do_tasks", clear_current_tasks: true, new[] { Wait(1f) });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Single(brain.JobQueue);           // old two cleared, one new added
    }

    [Fact]
    public void Without_clear_new_tasks_are_appended_to_the_existing_queue()
    {
        var brain = NewBrain();
        brain.Enqueue(new WaitJob(1f));

        var decision = new NpcDecisionDto(
            "x", null, "do_tasks", clear_current_tasks: false, new[] { Wait(1f) });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Equal(2, brain.JobQueue.Count);   // original plus the new one
    }

    // --------------------------------------------------------------- following

    private static TaskSpecDto Follow(string who) => new("follow", who, 0f);

    // A brain whose NPC is Sir Kay (id 5), just back from talking with the Traveller (id 0).
    private static WanderBrain KayBrain()
    {
        var brain = new WanderBrain();
        _ = new TestNpc(0, 0, 5, "Sir Kay", brain);
        return brain;
    }

    private static Transcript KayMetTraveller() =>
        new(4, day: 1, "Medows", new[] { new Participant(0, "Traveller"), new Participant(5, "Sir Kay") }, new[] { "Traveller: walk with me" });

    private static NpcDecisionDto Doing(params TaskSpecDto[] tasks) =>
        new("x", null, "do_tasks", clear_current_tasks: false, tasks);

    [Fact]
    public void Follow_someone_from_the_conversation_follows_them_by_id()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("Traveller")), TestLevels.Location(Level), KayMetTraveller());

        Assert.Equal(0, Assert.IsType<FollowJob>(Assert.Single(brain.JobQueue)).TargetId);
        Assert.Equal(NpcBrain.NPCState.Working, brain.State);
    }

    [Fact]
    public void Follow_matches_the_name_whatever_its_case_and_spacing()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("  traveller ")), TestLevels.Location(Level), KayMetTraveller());

        Assert.IsType<FollowJob>(Assert.Single(brain.JobQueue));
    }

    [Fact]
    public void Follow_someone_who_wasnt_in_the_conversation_is_dropped()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("Sir Lancelot"), Wait(2f)), TestLevels.Location(Level), KayMetTraveller());

        Assert.IsType<WaitJob>(Assert.Single(brain.JobQueue));
    }

    [Fact]
    public void An_NPC_cant_follow_itself()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("Sir Kay")), TestLevels.Location(Level), KayMetTraveller());

        Assert.Empty(brain.JobQueue);
    }

    [Fact]
    public void Follow_with_no_transcript_or_no_target_is_dropped()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("Traveller")), TestLevels.Location(Level));            // no transcript
        brain.ApplyDecision(Doing(Follow(null!)), TestLevels.Location(Level), KayMetTraveller());  // no target

        Assert.Empty(brain.JobQueue);
    }

    [Fact]
    public void Tasks_after_a_follow_in_the_same_decision_replace_it()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("Traveller"), GoTo("shop")), TestLevels.Location(Level), KayMetTraveller());

        Assert.IsType<GoToJob>(Assert.Single(brain.JobQueue));   // the follow would never have let it run
    }

    [Fact]
    public void A_later_decisions_tasks_replace_a_follow_without_clearing()
    {
        var brain = KayBrain();
        var level = TestLevels.Location(Level);
        brain.ApplyDecision(Doing(Wait(1f), Follow("Traveller")), level, KayMetTraveller());

        brain.ApplyDecision(Doing(GoTo("shop")), level, KayMetTraveller());

        // The wait was before the follow, so it still runs; the follow made way for the go_to.
        Assert.Collection(brain.JobQueue,
            j => Assert.IsType<WaitJob>(j),
            j => Assert.IsType<GoToJob>(j));
    }

    [Fact]
    public void A_new_follow_replaces_the_old_one()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("Traveller"), Follow("Traveller")), TestLevels.Location(Level), KayMetTraveller());

        Assert.IsType<FollowJob>(Assert.Single(brain.JobQueue));
    }

    // --------------------------------------------------------------- talking to someone

    private static TaskSpecDto TalkTo(string who, string reason) => new("talk_to", who, 0f, reason);

    [Fact]
    public void Talk_to_someone_from_the_conversation_walks_up_to_them_with_the_reason()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(TalkTo("Traveller", "  warn them about the wolves ")), TestLevels.Location(Level), KayMetTraveller());

        var job = Assert.IsType<TalkToJob>(Assert.Single(brain.JobQueue));
        Assert.Equal(0, job.TargetId);
        Assert.Equal("warn them about the wolves", job.Reason);
        Assert.Equal(NpcBrain.NPCState.Working, brain.State);
    }

    [Fact]
    public void Talk_to_with_a_blank_reason_still_goes_but_has_none()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(TalkTo("Traveller", "   ")), TestLevels.Location(Level), KayMetTraveller());

        Assert.Null(Assert.IsType<TalkToJob>(Assert.Single(brain.JobQueue)).Reason);
    }

    [Fact]
    public void Talk_to_a_stranger_or_itself_is_dropped()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(TalkTo("Sir Lancelot", "hi"), TalkTo("Sir Kay", "hi")), TestLevels.Location(Level), KayMetTraveller());

        Assert.Empty(brain.JobQueue);
    }

    [Fact]
    public void Talk_to_after_a_follow_replaces_it()
    {
        var brain = KayBrain();

        brain.ApplyDecision(Doing(Follow("Traveller"), TalkTo("Traveller", "hi")), TestLevels.Location(Level), KayMetTraveller());

        Assert.IsType<TalkToJob>(Assert.Single(brain.JobQueue));
    }

    [Fact]
    public void A_talk_to_from_the_server_arrives_with_its_reason()
    {
        // What /decision sends back, read the way DialogueAPI reads it (ReadFromJsonAsync: Web defaults).
        const string json = """
            {"summary": "x", "memories": [], "behaviour": "do_tasks", "clear_current_tasks": false,
             "tasks": [{"type": "talk_to", "target": "Traveller", "seconds": 0.0, "reason": "warn them"}],
             "join_faction": null, "model": "test"}
            """;

        var decision = System.Text.Json.JsonSerializer.Deserialize<NpcDecisionDto>(
            json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        var task = Assert.Single(decision.tasks);
        Assert.Equal("talk_to", task.type);
        Assert.Equal("Traveller", task.target);
        Assert.Equal("warn them", task.reason);
    }
}
