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
/// the parts that show it. Everything is in cells, so no window size is needed.
/// </summary>
public class WorldTests : IDisposable
{
    // One open 5x5 field with the player's spawn in the middle cell (2,2) -> standing spot (2.5, 2.9).
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

    // An NPC standing right next to the player, with its brain kept so the test can read its journal.
    private static (TestNpc npc, Ashenveil.Core.Entities.NpcBrain brain) NpcBeside(World world)
    {
        var brain = new WanderBrain();
        var npc = new TestNpc(world.player.Position.X + 0.5f, world.player.Position.Y, brain);
        world.AddNpc(npc);
        return (npc, brain);
    }

    // ------------------------------------------------------------------ it runs

    [Fact]
    public void A_world_can_be_built_and_stepped_with_no_screen()
    {
        var world = NewWorld();

        for (int i = 0; i < 60; i++) Step(world, 1 / 60.0);   // one second of frames

        // Init stood the player's feet on Player_spawn, and with no keys held they're still there.
        Near.Equal(new Vector2(2.5f, 2.9f), world.player.Feet);
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

    // ------------------------------------------------------------------ exits

    // The same field with a door in the top-middle cell (2,0) -> x 2..3, y 0..1.
    // It leads to the real Medows level, which ships with the tests.
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

    private World NewWorldWithDoor()
    {
        var world = new World(TestLevels.Location(LevelWithDoor),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(_decisionReply));
        world.Init();
        return world;
    }

    // Stands the player with the top of their body `gap` cells below the door cell's bottom
    // edge (y = 1), feet in the row below - where collision leaves you against a solid door.
    private static void StandBelowDoor(World world, float gap)
    {
        var p = world.player;
        p.Position += new Vector2(2.5f - p.Bounds.Center.X, 1 + gap - p.Bounds.Top);
    }

    [Fact]
    public void Interacting_pressed_against_a_door_goes_through_it()
    {
        // The bug: the feet were the only thing checked, and they can't reach a door set
        // into something solid, so the castle door in Medows never worked.
        var world = NewWorldWithDoor();
        StandBelowDoor(world, gap: 0);

        world.Interact();

        Assert.Equal("Medows", world.location.Name);
    }

    [Fact]
    public void Going_through_a_door_lands_on_the_point_it_names()
    {
        var world = NewWorldWithDoor();
        StandBelowDoor(world, gap: 0);

        world.Interact();

        // "entry" in Medows is cell (3,7) -> standing spot (3.5, 7.9).
        Near.Equal(world.location.Poi("entry").StandingSpot(), world.player.Feet);
        Near.Equal(new Vector2(3.5f, 7.9f), world.player.Feet);
    }

    [Fact]
    public void Medows_and_the_castle_lead_into_each_other_and_back()
    {
        // The shipped levels, both ways: each door lands just inside the other one's door.
        var world = new World(new Ashenveil.Core.GameWorld.Locations.Location("Medows"),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(_decisionReply));
        world.Init();

        world.UseExit(world.location.ExitAt(3, 6));
        Assert.Equal("CastleInterior", world.location.Name);
        Near.Equal(world.location.Poi("from_medows").StandingSpot(), world.player.Feet);

        world.UseExit(world.location.ExitAt(3, 4));
        Assert.Equal("Medows", world.location.Name);
        Near.Equal(world.location.Poi("entry").StandingSpot(), world.player.Feet);
    }

    [Fact]
    public void Going_back_to_a_level_returns_to_the_same_one_not_a_fresh_copy()
    {
        var world = new World(new Ashenveil.Core.GameWorld.Locations.Location("Medows"),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(_decisionReply));
        world.Init();
        var medows = world.location;

        world.UseExit(medows.ExitAt(3, 6));       // into the castle...
        var castle = world.location;
        world.UseExit(castle.ExitAt(3, 4));       // ...back out...
        Assert.Same(medows, world.location);

        world.UseExit(medows.ExitAt(3, 6));       // ...and in again
        Assert.Same(castle, world.location);
    }

    [Fact]
    public void Interacting_away_from_the_door_stays_put()
    {
        var world = NewWorldWithDoor();
        StandBelowDoor(world, gap: 0.3f);
        string before = world.location.Name;

        world.Interact();

        Assert.Equal(before, world.location.Name);
    }

    [Fact]
    public void At_a_door_with_an_NPC_beside_it_the_door_wins()
    {
        var world = NewWorldWithDoor();
        StandBelowDoor(world, gap: 0);
        NpcBeside(world);
        bool talked = false;
        world.PlayerConversationStarted += _ => talked = true;

        var convo = world.Interact();

        Assert.Null(convo);
        Assert.False(talked);
        Assert.Equal("Medows", world.location.Name);
    }

    // ------------------------------------------------------------------ remembering

    [Fact]
    public void A_finished_chat_is_remembered_and_what_the_NPC_learned_is_applied()
    {
        var world = NewWorld();
        var (_, brain) = NpcBeside(world);
        _decisionReply = new NpcDecisionDto("Met Loki.", new[] { "The traveller's name is Loki." },
            "resume", false, Array.Empty<TaskSpecDto>());
        var convo = world.Interact();

        convo.Add(Player.DefaultName, "My name is Loki.");
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
