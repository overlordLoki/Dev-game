using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Graphics;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// The World runs with no window, no screen and no network: it's built from a level, given
/// fake LLM calls, and stepped with hand-made GameTimes - exactly what the game does, minus
/// the parts that show it. With Layout.Update(500) a cell is 100px.
/// </summary>
public class WorldTests : IDisposable
{
    // One open 5x5 field with the player's spawn in the middle cell (2,2) -> pixel centre (250,250).
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

    // What the fake backend was asked, and what it answers.
    private readonly List<DecisionRequestDto> _decisionsAsked = new();
    private NpcDecisionDto _decisionReply = new("x", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>());

    public WorldTests()
    {
        NullContentManager.Install();
        Layout.Update(500);
        AshenveilGame.Input = new FakeInput();   // no keys held: the player stands still
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private World NewWorld()
    {
        var world = new World(TestLevels.Location(Level),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: req => { _decisionsAsked.Add(req); return Task.FromResult(_decisionReply); });
        world.Init();
        return world;
    }

    private static void Step(World world, double seconds) =>
        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    private static Vector2 Centre(Entity e) => e.Position + new Vector2(e.Width / 2f, e.Height / 2f);

    // An NPC standing right next to the player, with its brain kept so the test can read its journal.
    private static (TestNpc npc, Ashenveil.Core.Entities.NpcBrain brain) NpcBeside(World world)
    {
        var brain = new WanderBrain();
        var npc = new TestNpc(world.player.Position.X + 50, world.player.Position.Y, brain);
        world.AddNpc(npc);
        return (npc, brain);
    }

    // ------------------------------------------------------------------ it runs

    [Fact]
    public void A_world_can_be_built_and_stepped_with_no_screen()
    {
        var world = NewWorld();

        for (int i = 0; i < 60; i++) Step(world, 1 / 60.0);   // one second of frames

        Assert.Equal(new Vector2(250, 250), Centre(world.player));   // still standing at spawn
    }

    [Fact]
    public void The_player_spawns_centred_on_its_spawn_point()
    {
        Assert.Equal(new Vector2(250, 250), Centre(NewWorld().player));
    }

    [Fact]
    public void Ten_minutes_of_game_time_is_one_day()
    {
        var world = NewWorld();

        Step(world, 600);

        Assert.Equal(2, world.Clock.TotalDays);
    }

    // ------------------------------------------------------------------ interacting

    [Fact]
    public void Interacting_beside_an_NPC_starts_a_conversation_and_tells_the_screen()
    {
        var world = NewWorld();
        var (npc, _) = NpcBeside(world);
        Conversation shown = null;
        world.PlayerConversationStarted += c => shown = c;

        var convo = world.Interact();

        Assert.NotNull(convo);
        Assert.Same(convo, shown);                 // the screen would open the chat box for this
        Assert.Contains(npc, convo.Participants);
    }

    [Fact]
    public void Interacting_with_nothing_nearby_does_nothing()
    {
        var world = NewWorld();
        bool raised = false;
        world.PlayerConversationStarted += _ => raised = true;

        Assert.Null(world.Interact());
        Assert.False(raised);
    }

    [Fact]
    public void A_conversation_between_NPCs_does_not_open_the_chat_box()
    {
        var world = NewWorld();
        var (a, _) = NpcBeside(world);
        var (b, _) = NpcBeside(world);
        bool raised = false;
        world.PlayerConversationStarted += _ => raised = true;

        world.StartConversation(a, b);

        Assert.False(raised);
    }

    // ------------------------------------------------------------------ remembering

    [Fact]
    public void Opening_and_closing_a_chat_without_speaking_records_nothing()
    {
        var world = NewWorld();
        var (_, brain) = NpcBeside(world);
        var convo = world.Interact();

        convo.End();
        Step(world, 1 / 60.0);

        Assert.Empty(brain.Journal.Transcripts);
        Assert.Empty(_decisionsAsked);             // and no LLM call was made
    }

    [Fact]
    public void A_finished_chat_is_remembered_and_what_the_NPC_learned_is_applied()
    {
        var world = NewWorld();
        var (_, brain) = NpcBeside(world);
        _decisionReply = new NpcDecisionDto("Met Loki.", new[] { "The traveller's name is Loki." },
            "resume", false, Array.Empty<TaskSpecDto>());
        var convo = world.Interact();

        convo.Add(Conversation.PlayerName, "My name is Loki.");
        convo.End();
        Step(world, 1 / 60.0);

        // The conversation went to the cold tier...
        var transcript = Assert.Single(brain.Journal.Transcripts);
        Assert.Contains("Traveller: My name is Loki.", transcript.Lines);
        // ...the decision was asked for with what was said...
        Assert.Contains("Traveller: My name is Loki.", Assert.Single(_decisionsAsked).transcript);
        // ...and its answer landed: a summary on the transcript, a fact pointing back to it.
        Assert.Equal("Met Loki.", transcript.Summary);
        var fact = Assert.Single(brain.Journal.Memories);
        Assert.Equal("The traveller's name is Loki.", fact.Text);
        Assert.Equal(transcript.Id, fact.SourceId);
    }
}
