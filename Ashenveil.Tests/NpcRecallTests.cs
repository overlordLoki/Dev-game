using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;

namespace Ashenveil.Tests;

/// <summary>
/// NPC.PastConversationsWith: the cold tier recalled as one line per earlier conversation with
/// someone ("Yesterday, at Medows: ..."), which goes into the prompt when a new chat starts.
/// Only conversations that got a summary are recalled, newest few, oldest first.
/// </summary>
public class NpcRecallTests
{
    public NpcRecallTests() => NullContentManager.Install();   // TestNpc construction needs Textures ready

    // An NPC whose journal holds the given transcripts.
    private static TestNpc NpcRemembering(params Transcript[] transcripts)
    {
        var brain = new WanderBrain();
        var npc = new TestNpc(0, 0, brain);
        foreach (var t in transcripts) brain.Journal.Add(t);
        return npc;
    }

    private static Transcript Talk(int day, string summary, string with = "Traveller", string location = "Medows")
    {
        var t = new Transcript(day, day, location, new[] { with, "Test" }, new[] { $"{with}: hi" });
        if (summary != null) Summarise(t, summary);
        return t;
    }

    // Summary has an internal setter, so the test fills it the way the game does: via ApplyDecision.
    private static void Summarise(Transcript t, string summary) =>
        new WanderBrain().ApplyDecision(new NpcDecisionDto(summary, null, "resume", false,
            System.Array.Empty<TaskSpecDto>()), null, t);

    [Fact]
    public void Recall_words_each_conversation_relative_to_today()
    {
        var npc = NpcRemembering(Talk(1, "Met Loki."), Talk(4, "Loki asked about wolves."), Talk(5, "Said goodbye."));

        var lines = npc.PastConversationsWith("Traveller", count: 3, today: 5).ToList();

        Assert.Equal(new[]
        {
            "4 days ago, at Medows: Met Loki.",
            "Yesterday, at Medows: Loki asked about wolves.",
            "Today, at Medows: Said goodbye.",
        }, lines);
    }

    [Fact]
    public void Recall_keeps_only_the_most_recent_few_oldest_first()
    {
        var npc = NpcRemembering(Talk(1, "One."), Talk(2, "Two."), Talk(3, "Three."));

        var lines = npc.PastConversationsWith("Traveller", count: 2, today: 3).ToList();

        Assert.Equal(new[] { "Yesterday, at Medows: Two.", "Today, at Medows: Three." }, lines);
    }

    [Fact]
    public void Recall_only_includes_conversations_with_that_person()
    {
        var npc = NpcRemembering(Talk(1, "With the player."), Talk(1, "With the smith.", with: "Smith"));

        var lines = npc.PastConversationsWith("Traveller", 5, today: 1).ToList();

        Assert.Equal(new[] { "Today, at Medows: With the player." }, lines);
    }

    [Fact]
    public void Recall_skips_conversations_that_never_got_a_summary()
    {
        // The decision never came back (API down), so there's nothing to say about it.
        var npc = NpcRemembering(Talk(1, summary: null), Talk(2, "Got this one."));

        var lines = npc.PastConversationsWith("Traveller", 5, today: 2).ToList();

        Assert.Equal(new[] { "Today, at Medows: Got this one." }, lines);
    }

    [Fact]
    public void Recall_leaves_out_the_place_when_it_is_unknown()
    {
        var npc = NpcRemembering(Talk(1, "Somewhere.", location: null));

        Assert.Equal("Today: Somewhere.", Assert.Single(npc.PastConversationsWith("Traveller", 5, today: 1)));
    }
}
