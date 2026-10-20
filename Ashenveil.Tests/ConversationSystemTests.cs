using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;

namespace Ashenveil.Tests;

/// <summary>
/// The ConversationSystem on its own: no World, no player, no level. The "player" is a plain
/// entity, and both LLM calls are fakes the test controls - including leaving a decision
/// unfinished, to check nothing is applied before it arrives.
/// </summary>
public class ConversationSystemTests
{
    private const int Today = 5;

    private readonly List<ConversationRequestDto> _chatsSent = new();
    private readonly List<DecisionRequestDto> _decisionsAsked = new();
    // What the next decision request returns. Swap in an unfinished or failing task to test those.
    private Task<NpcDecisionDto> _decisionReply = Task.FromResult(Decision("Met Loki.", "The traveller's name is Loki."));

    private readonly TestEntity _player = new(0, 0);
    private readonly WanderBrain _brain = new();
    private readonly TestNpc _npc;
    private readonly ConversationSystem _system;

    public ConversationSystemTests()
    {
        NullContentManager.Install();
        _npc = new TestNpc(50, 0, _brain);   // TestNpc's name is "Test"
        _system = new ConversationSystem(
            sendChat: req => { _chatsSent.Add(req); return Task.FromResult(Array.Empty<ChatMessage>()); },
            requestDecision: req => { _decisionsAsked.Add(req); return _decisionReply; });
    }

    private static NpcDecisionDto Decision(string summary, params string[] facts) =>
        new(summary, facts, "resume", false, Array.Empty<TaskSpecDto>());

    private Conversation StartWithPlayer() => _system.Start(_player, new NPC[] { _npc }, level: null, Today);

    // A conversation where the player actually said something, then left.
    private Conversation FinishedChat()
    {
        var convo = StartWithPlayer();
        convo.Add(Conversation.PlayerName, "My name is Loki.");
        convo.End();
        return convo;
    }

    // ------------------------------------------------------------------ 1. start

    [Fact]
    public void Starting_sets_the_NPCs_talking()
    {
        var convo = StartWithPlayer();

        Assert.Equal(Ashenveil.Core.Entities.NpcBrain.NPCState.Talking, _brain.State);
        Assert.Contains(convo, _system.Active);
    }

    [Fact]
    public void The_player_speaks_first_the_chat_opens_with_only_a_note()
    {
        var convo = StartWithPlayer();

        var opening = Assert.Single(convo.Messages);
        Assert.Null(opening.Speaker);                    // a game note, not a line anyone said
        Assert.Equal("You approach Test.", opening.Text);
        Assert.Empty(_chatsSent);                        // nothing asked of the LLM until the player speaks
    }

    [Fact]
    public void The_opening_note_is_not_sent_to_the_LLM()
    {
        var convo = StartWithPlayer();

        convo.Say(Conversation.PlayerName, "Hello.");

        Assert.Equal(new[] { "Traveller: Hello." }, Assert.Single(_chatsSent).history);
    }

    [Fact]
    public void Opening_and_closing_without_speaking_leaves_nothing_to_remember()
    {
        var convo = StartWithPlayer();
        convo.End();

        _system.Update(Today);

        Assert.Empty(_decisionsAsked);
    }

    [Fact]
    public void The_LLM_is_told_what_the_NPC_remembers()
    {
        _brain.Journal.Add(new Ashenveil.Core.Entities.brains.Memorys.Memory(
            "The traveller's name is Loki.", Ashenveil.Core.Entities.brains.Memorys.MemoryKind.Conversation));

        var convo = StartWithPlayer();
        convo.Say(Conversation.PlayerName, "Do you remember me?");   // sends the request

        var npcInPrompt = Assert.Single(_chatsSent).participants.Single(p => p.name == "Test");
        Assert.Contains("The traveller's name is Loki.", npcInPrompt.memories);
    }

    // ------------------------------------------------------------------ 2. finish

    [Fact]
    public void A_chat_where_only_the_NPC_spoke_is_not_recorded_or_sent()
    {
        StartWithPlayer().End();

        _system.Update(Today);

        Assert.Empty(_brain.Journal.Transcripts);
        Assert.Empty(_decisionsAsked);
        Assert.Empty(_system.Active);   // but it is closed
    }

    [Fact]
    public void A_finished_chat_is_filed_with_todays_date_and_a_decision_is_asked_for()
    {
        FinishedChat();

        _system.Update(Today);

        var transcript = Assert.Single(_brain.Journal.Transcripts);
        Assert.Equal(Today, transcript.Day);
        Assert.Contains("Traveller: My name is Loki.", Assert.Single(_decisionsAsked).transcript);
    }

    [Fact]
    public void The_NPC_goes_back_to_what_it_was_doing_when_the_chat_ends()
    {
        FinishedChat();

        _system.Update(Today);

        Assert.NotEqual(Ashenveil.Core.Entities.NpcBrain.NPCState.Talking, _brain.State);
    }

    // ------------------------------------------------------------------ 3. apply

    [Fact]
    public void A_decision_that_has_arrived_is_applied_in_the_same_update()
    {
        FinishedChat();

        _system.Update(Today);

        var fact = Assert.Single(_brain.Journal.Memories);
        Assert.Equal("The traveller's name is Loki.", fact.Text);
        Assert.Equal(_brain.Journal.Transcripts[0].Id, fact.SourceId);
        Assert.Equal(0, _system.PendingDecisions);
    }

    [Fact]
    public void A_decision_still_on_its_way_is_applied_only_once_it_arrives()
    {
        var reply = new TaskCompletionSource<NpcDecisionDto>();
        _decisionReply = reply.Task;
        FinishedChat();

        _system.Update(Today);                              // asked, but no answer yet
        Assert.Empty(_brain.Journal.Memories);
        Assert.Equal(1, _system.PendingDecisions);

        reply.SetResult(Decision("Met Loki.", "The traveller's name is Loki."));
        _system.Update(Today);                              // the answer is picked up next frame

        Assert.Single(_brain.Journal.Memories);
        Assert.Equal(0, _system.PendingDecisions);
    }

    [Fact]
    public void A_failed_decision_is_dropped_and_the_NPC_learns_nothing()
    {
        _decisionReply = Task.FromException<NpcDecisionDto>(new HttpRequestException("backend down"));
        FinishedChat();

        _system.Update(Today);

        Assert.Empty(_brain.Journal.Memories);
        Assert.Single(_brain.Journal.Transcripts);          // the conversation itself is still remembered
        Assert.Equal(0, _system.PendingDecisions);
    }
}
