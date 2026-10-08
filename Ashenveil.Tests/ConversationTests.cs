using Ashenveil.Core;
using Ashenveil.Core.Dialogue;
using Ashenveil.Core.Entities;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Conversation never talks to the API itself - it is handed a function that returns a
/// Task and polls it - so every outcome (answered, down, empty, still thinking) is just
/// a different Task.
/// </summary>
public class ConversationTests
{
    private const string Npc = "Sir Lancelot";
    private const string Greeting = "Hello, traveller.";
    private const string Player = Conversation.PlayerName;

    private readonly ConversationRequestDto _request = new() { participants = new() };
    private int _requestsSent;

    private Conversation NewConversation(Task<ChatMessage[]> reply)
    {
        var c = new Conversation(new Entity[] { new TestEntity(0, 0), new TestEntity(100, 50) }, location: null,
            _request, _ => { _requestsSent++; return reply; });
        c.Add(Npc, Greeting);
        return c;
    }

    private static Task<ChatMessage[]> Reply(params string[] lines) =>
        Task.FromResult(lines.Select(l => new ChatMessage(Npc, l)).ToArray());

    // What the player sees after saying "Hi" and the request finishing one way or another.
    private Conversation Answered(Task<ChatMessage[]> reply)
    {
        var c = NewConversation(reply);
        c.Say(Player, "Hi");
        c.Poll();
        return c;
    }

    private static string[] Texts(Conversation c) => c.Messages.Select(m => m.Text).ToArray();

    // ------------------------------------------------------------------------- Add

    [Fact]
    public void Add_shows_the_greeting_without_asking_for_a_reply()
    {
        var c = NewConversation(Reply("a"));

        Assert.Equal(new[] { new ChatMessage(Npc, Greeting) }, c.Messages);
        Assert.False(c.IsLoading);
        Assert.Equal(0, _requestsSent);
    }

    // ------------------------------------------------------------------------- Say

    [Fact]
    public void Say_shows_the_players_line_straight_away_and_starts_loading()
    {
        var c = NewConversation(new TaskCompletionSource<ChatMessage[]>().Task);

        c.Say(Player, "Hi");

        Assert.Equal(new[] { Greeting, "Hi" }, Texts(c));
        Assert.Equal(Player, c.Messages[1].Speaker);
        Assert.True(c.IsLoading);
        Assert.Equal(1, _requestsSent);
    }

    [Fact]
    public void Say_while_a_reply_is_on_its_way_is_ignored()
    {
        var c = NewConversation(new TaskCompletionSource<ChatMessage[]>().Task);
        c.Say(Player, "Hi");

        c.Say(Player, "Hello?");

        Assert.Equal(new[] { Greeting, "Hi" }, Texts(c));
        Assert.Equal(1, _requestsSent);
    }

    [Fact]
    public void Say_after_the_conversation_has_ended_is_ignored()
    {
        var c = NewConversation(Reply("a"));
        c.End();

        c.Say(Player, "Hi");

        Assert.Equal(new[] { Greeting }, Texts(c));
        Assert.Equal(0, _requestsSent);
    }

    [Fact]
    public void The_request_carries_everything_said_so_far_as_history()
    {
        var c = Answered(Reply("a"));
        c.Say(Player, "Bye");

        Assert.Equal(new[] { $"{Npc}: {Greeting}", $"{Player}: Hi", $"{Npc}: a", $"{Player}: Bye" }, _request.history);
    }

    // ------------------------------------------------------------------------ Poll

    [Fact]
    public void Poll_success_appends_the_reply_after_the_players_line()
    {
        var c = Answered(Reply("a", "b"));

        Assert.Equal(new[] { Greeting, "Hi", "a", "b" }, Texts(c));
        Assert.Equal(Npc, c.Messages[2].Speaker);
        Assert.False(c.IsLoading);
    }

    [Fact]
    public void Poll_api_down_leaves_a_note_and_lets_the_player_try_again()
    {
        var c = Answered(Task.FromException<ChatMessage[]>(new HttpRequestException("connection refused")));

        Assert.Equal(new ChatMessage(null, Conversation.NoReply), c.Messages[^1]);
        Assert.False(c.IsLoading);
    }

    [Fact]
    public void Poll_cancelled_request_leaves_a_note()
    {
        var c = Answered(Task.FromCanceled<ChatMessage[]>(new CancellationToken(canceled: true)));

        Assert.Equal(new[] { Greeting, "Hi", Conversation.NoReply }, Texts(c));
        Assert.False(c.IsLoading);
    }

    [Fact]
    public void Poll_empty_reply_leaves_a_note()
    {
        var c = Answered(Task.FromResult(Array.Empty<ChatMessage>()));

        Assert.Equal(new[] { Greeting, "Hi", Conversation.NoReply }, Texts(c));
        Assert.False(c.IsLoading);
    }

    [Fact]
    public void Poll_null_reply_leaves_a_note()
    {
        var c = Answered(Task.FromResult<ChatMessage[]>(null!));

        Assert.Equal(new[] { Greeting, "Hi", Conversation.NoReply }, Texts(c));
        Assert.False(c.IsLoading);
    }

    [Fact]
    public void The_no_answer_note_is_not_sent_to_the_llm_as_history()
    {
        Answered(Task.FromResult(Array.Empty<ChatMessage>()));

        Assert.Equal(new[] { $"{Npc}: {Greeting}", $"{Player}: Hi" }, _request.history);
    }

    [Fact]
    public void Poll_while_still_waiting_changes_nothing_and_keeps_loading()
    {
        var c = NewConversation(new TaskCompletionSource<ChatMessage[]>().Task);
        c.Say(Player, "Hi");

        c.Poll();
        c.Poll();

        Assert.Equal(new[] { Greeting, "Hi" }, Texts(c));
        Assert.True(c.IsLoading);
    }

    [Fact]
    public void Poll_picks_up_the_reply_on_the_first_frame_after_it_arrives()
    {
        var request = new TaskCompletionSource<ChatMessage[]>();
        var c = NewConversation(request.Task);
        c.Say(Player, "Hi");
        c.Poll();

        request.SetResult(new[] { new ChatMessage(Npc, "a") });
        c.Poll();

        Assert.Equal(new[] { Greeting, "Hi", "a" }, Texts(c));
        Assert.False(c.IsLoading);
    }

    [Fact]
    public void Poll_after_the_reply_has_been_taken_changes_nothing()
    {
        var c = Answered(Reply("a", "b"));

        c.Poll();
        c.Poll();

        Assert.Equal(new[] { Greeting, "Hi", "a", "b" }, Texts(c));
    }

    [Fact]
    public void Poll_with_nothing_asked_leaves_the_transcript_alone()
    {
        var c = NewConversation(Reply("a"));

        c.Poll();

        Assert.Equal(new[] { Greeting }, Texts(c));
        Assert.False(c.IsLoading);
    }

    // ------------------------------------------------------------------ IsFinished

    [Fact]
    public void IsFinished_only_once_the_conversation_is_ended()
    {
        var c = Answered(Reply("a"));
        Assert.False(c.IsFinished);

        c.End();

        Assert.True(c.IsFinished);
    }

    // ---------------------------------------------------------------- CenterOfMass

    [Fact]
    public void CenterOfMass_is_the_average_of_the_participants_positions()
    {
        Layout.Update(500);

        var c = NewConversation(Reply("a"));   // (0,0) and (100,50)

        Assert.Equal(new Vector2(50, 25), c.CenterOfMass);
    }
}
