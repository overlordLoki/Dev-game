using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;

namespace Ashenveil.Tests;

/// <summary>
/// Conversation.ToTranscript turns a live conversation into plain data for the cold tier.
/// It must keep only what was said, use names rather than entities, and be a snapshot -
/// nothing said afterwards may leak into it.
/// </summary>
public class TranscriptTests
{
    private const string Level = """
        { "cols": 1, "rows": 1, "tiles": [["grass"]] }
        """;

    public TranscriptTests() => NullContentManager.Install();

    // The player (a plain entity) talking to one named NPC. Every reply the "API" gives is
    // whatever `reply` says, so a test can make it answer or fail.
    private static Conversation NewConversation(Task<ChatMessage[]> reply, Ashenveil.Core.GameWorld.Locations.Location level = null) =>
        new(new Entity[] { new TestEntity(0, 0), new TestNpc(100, 0) }, level,
            new ConversationRequestDto { participants = new() }, _ => reply);

    private static Task<ChatMessage[]> Answer(string text) =>
        Task.FromResult(new[] { new ChatMessage("Test", text) });

    [Fact]
    public void Lines_are_what_was_said_in_order()
    {
        var c = NewConversation(Answer("Well met."));
        c.Add("Test", "Halt.");
        c.Say(Conversation.PlayerName, "Hello.");
        c.Poll();

        var t = c.ToTranscript(id: 1, day: 3);

        Assert.Equal(new[] { "Test: Halt.", "Traveller: Hello.", "Test: Well met." }, t.Lines);
    }

    [Fact]
    public void Game_notes_like_no_answer_are_left_out()
    {
        var c = NewConversation(Task.FromResult(System.Array.Empty<ChatMessage>()));   // API answers nothing
        c.Say(Conversation.PlayerName, "Hello?");
        c.Poll();                                                                       // adds "(no answer)"

        var t = c.ToTranscript(1, 1);

        Assert.Equal(new[] { "Traveller: Hello?" }, t.Lines);
    }

    [Fact]
    public void Participants_are_names_with_the_player_as_Traveller()
    {
        var t = NewConversation(Answer("")).ToTranscript(1, 1);

        Assert.Equal(new[] { Conversation.PlayerName, "Test" }, t.Participants);
    }

    [Fact]
    public void Id_day_and_location_name_are_carried_and_summary_starts_empty()
    {
        var level = TestLevels.Location(Level);

        var t = NewConversation(Answer(""), level).ToTranscript(id: 7, day: 12);

        Assert.Equal(7, t.Id);
        Assert.Equal(12, t.Day);
        Assert.Equal(level.Name, t.Location);
        Assert.Null(t.Summary);   // filled in when the decision lands
    }

    [Fact]
    public void A_conversation_with_no_level_has_no_location()
    {
        Assert.Null(NewConversation(Answer("")).ToTranscript(1, 1).Location);
    }

    [Fact]
    public void It_is_a_snapshot_later_lines_do_not_change_it()
    {
        var c = NewConversation(Answer("x"));
        c.Add("Test", "Halt.");
        var t = c.ToTranscript(1, 1);

        c.Add("Test", "Said afterwards.");

        Assert.Equal(new[] { "Test: Halt." }, t.Lines);
    }

    [Fact]
    public void Journal_keeps_transcripts_in_the_cold_tier_apart_from_memories()
    {
        var journal = new Journal();
        journal.Add(new Transcript(1, 1, "Medows", new[] { "Traveller" }, new[] { "Traveller: hi" }));

        Assert.Single(journal.Transcripts);
        Assert.Empty(journal.Memories);
    }
}
