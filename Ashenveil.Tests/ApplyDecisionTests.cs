using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Task;

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

        Assert.Equal(3, brain.TaskQueue.Count);
        Assert.Equal(NpcBrain.NPCState.Tasked, brain.State);
    }

    [Fact]
    public void An_ordinary_chat_writes_the_journal_without_tasking_the_npc()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "Chatted about the roads.", new[] { "the traveller asked about the roads" },
            "resume", clear_current_tasks: false, System.Array.Empty<TaskSpecDto>());

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Empty(brain.TaskQueue);
        Assert.NotEqual(NpcBrain.NPCState.Tasked, brain.State);
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
        new(id, day: 1, "Medows", new[] { "Traveller", "Test" }, new[] { "Traveller: my name is Loki" });

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

        Assert.Single(brain.TaskQueue);   // only the wait survived
    }

    [Fact]
    public void Go_to_a_nonexistent_poi_is_dropped()
    {
        var brain = NewBrain();
        var decision = new NpcDecisionDto(
            "x", null, "do_tasks", clear_current_tasks: true,
            new[] { GoTo("nowhere"), Wait(2f) });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Single(brain.TaskQueue);   // "nowhere" dropped, the wait remains
    }

    [Fact]
    public void A_null_decision_is_a_no_op()
    {
        var brain = NewBrain();

        brain.ApplyDecision(null, TestLevels.Location(Level));   // must not throw

        Assert.Empty(brain.TaskQueue);
        Assert.Empty(brain.Journal.Memories);
    }

    // --------------------------------------------------------------- clearing the queue

    [Fact]
    public void Clear_current_tasks_empties_the_queue_before_adding_new_ones()
    {
        var brain = NewBrain();
        brain.Enqueue(new WaitTask(1f));
        brain.Enqueue(new WaitTask(1f));          // two tasks already queued

        var decision = new NpcDecisionDto(
            "x", null, "do_tasks", clear_current_tasks: true, new[] { Wait(1f) });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Single(brain.TaskQueue);           // old two cleared, one new added
    }

    [Fact]
    public void Without_clear_new_tasks_are_appended_to_the_existing_queue()
    {
        var brain = NewBrain();
        brain.Enqueue(new WaitTask(1f));

        var decision = new NpcDecisionDto(
            "x", null, "do_tasks", clear_current_tasks: false, new[] { Wait(1f) });

        brain.ApplyDecision(decision, TestLevels.Location(Level));

        Assert.Equal(2, brain.TaskQueue.Count);   // original plus the new one
    }
}
