using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Jobs;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Graphics;
using Ashenveil.Core.Utility;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// The console commands that read and change the game, run against a World built from a test
/// level - the same way WorldTests does, with no screen. A 5x5 field, in cells.
/// </summary>
public class WorldCommandsTests : IDisposable
{
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

    private readonly World _world;
    private readonly CommandRegistry _commands = new();

    public WorldCommandsTests()
    {
        NullContentManager.Install();
        Ashenveil.Core.AshenveilGame.Input = new FakeInput();

        _world = new World(TestLevels.Location(Level),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(new NpcDecisionDto("x", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>())));
        _world.Init();
        WorldCommands.Register(_commands, _world);
    }

    public void Dispose()
    {
        Ashenveil.Core.AshenveilGame.Input = new LiveInput();
        Debug.SHOWNAV = false;   // static, so don't leave it on for the next test
    }

    private string[] Run(string input) => _commands.Execute(input).Select(l => l.Text).ToArray();

    private ConsoleLine RunOne(string input) => Assert.Single(_commands.Execute(input));

    // An NPC (id 0) with its brain kept, so a test can fill its journal.
    private (TestNpc npc, WanderBrain brain) AddNpc()
    {
        var brain = new WanderBrain();
        var npc = new TestNpc(0, 0, brain);
        _world.AddNpc(npc);
        return (npc, brain);
    }

    // ------------------------------------------------------------------ where / tp

    [Fact]
    public void Where_names_the_level_and_the_cell_the_feet_are_in()
    {
        var line = RunOne("where");

        Assert.Equal($"Player: {_world.location.Name}, cell (2, 2), feet at (2.50, 2.90)", line.Text);
    }

    [Fact]
    public void Where_player_is_the_same_as_no_argument()
    {
        Assert.Equal(RunOne("where").Text, RunOne("where PLAYER").Text);
    }

    [Fact]
    public void Where_an_NPC_shows_its_feet_by_id_or_name()
    {
        var npc = new TestNpc(3, 1, 7);   // 1x1, box = whole sprite: feet at (3.5, 2)
        _world.AddNpc(npc);

        string expected = $"Test (#7): {_world.location.Name}, cell (3, 1), feet at (3.50, 2.00)";
        Assert.Equal(expected, RunOne("where 7").Text);
        Assert.Equal(expected, RunOne("where test").Text);
    }

    [Fact]
    public void Where_an_unknown_NPC_is_an_error()
    {
        Assert.Equal(LineKind.Error, RunOne("where Bob").Kind);
    }

    [Theory]
    [InlineData("tpxy 3.25 4.5", 3.25f, 4.5f)]
    [InlineData("tpxy 1 2", 1f, 2f)]
    [InlineData("tpxy 0 5", 0f, 5f)]          // the very edge of the map is allowed
    public void Tpxy_puts_the_feet_on_an_exact_spot(string input, float x, float y)
    {
        var line = RunOne(input);

        Assert.Equal(LineKind.Output, line.Kind);
        Near.Equal(new Vector2(x, y), _world.player.Feet);
    }

    [Theory]
    [InlineData("tpxy 5.1 2")]
    [InlineData("tpxy -0.5 2")]
    [InlineData("tpxy left up")]
    [InlineData("tpxy 3,5 2")]                // a comma isn't a decimal point here
    public void Tpxy_off_the_map_or_not_numbers_is_an_error_and_leaves_the_player_alone(string input)
    {
        var before = _world.player.Position;

        Assert.Equal(LineKind.Error, RunOne(input).Kind);
        Assert.Equal(before, _world.player.Position);
    }

    [Fact]
    public void Tp_stands_the_player_on_the_cell_like_a_spawn_point()
    {
        RunOne("tp 4 1");

        var p = _world.player;
        Near.Equal(new Vector2(4.5f, 1.9f), p.Feet);   // centred across, near the bottom
        Assert.Contains("cell (4, 1)", RunOne("where").Text);
    }

    [Theory]
    [InlineData("tp 5 0")]     // one past the right edge
    [InlineData("tp 0 5")]
    [InlineData("tp -1 0")]
    public void Tp_off_the_map_is_an_error_and_leaves_the_player_alone(string input)
    {
        var before = _world.player.Position;

        var line = RunOne(input);

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("5x5", line.Text);
        Assert.Equal(before, _world.player.Position);
    }

    [Fact]
    public void Tp_with_words_instead_of_numbers_is_an_error()
    {
        Assert.Equal(LineKind.Error, RunOne("tp left up").Kind);
    }

    // ------------------------------------------------------------------ npcs

    [Fact]
    public void Npcs_says_so_when_there_are_none()
    {
        Assert.Equal("No NPCs yet.", RunOne("npcs").Text);
    }

    [Fact]
    public void Npcs_lists_each_one_with_its_id_level_and_state()
    {
        var (npc, _) = AddNpc();

        var line = RunOne("npcs");

        Assert.Equal($"#{npc.Id} Test - {_world.location.Name}, {npc.State}", line.Text);
    }

    // ------------------------------------------------------------------ journal

    [Fact]
    public void Journal_shows_an_NPCs_facts_events_conversations_and_diary()
    {
        var (_, brain) = AddNpc();
        brain.Journal.Add(new Memory("The traveller's name is Loki.", MemoryKind.Conversation) { SourceId = 1, Day = 3 });
        brain.Journal.Add(new Memory("Sir Lancelot couldn't reach the well", MemoryKind.Event) { SourceId = 4, Day = 3 });
        brain.Journal.Add(new Transcript(1, 3, "Medows", new[] { new Participant(0, "Traveller"), new Participant(1, "Test") }, new[] { "Traveller: hi" }));
        brain.Journal.Add(new DayEntry(2, "A quiet day at the gate."));
        brain.Journal.LastReflectedDay = 2;

        var lines = Run("journal Test");

        Assert.Equal("Test (#0) remembers 1 fact(s):", lines[0]);
        Assert.Contains("The traveller's name is Loki.", lines[1]);
        Assert.Contains("day 3, conversation #1", lines[1]);
        Assert.Equal("...1 event(s) not yet reflected on:", lines[2]);
        Assert.Equal("  - day 3: Sir Lancelot couldn't reach the well  (event #4)", lines[3]);
        Assert.Equal("...1 conversation(s):", lines[4]);
        Assert.Equal("  #1 day 3 at Medows with Traveller, Test: (no summary yet)", lines[5]);
        Assert.Equal("...and 1 diary entry (last reflected on day 2):", lines[6]);
        Assert.Equal("  day 2: A quiet day at the gate.", lines[7]);
    }

    [Fact]
    public void Journal_finds_an_NPC_by_id_or_by_name_in_any_case()
    {
        _world.AddNpc(new TestNpc(0, 0, 7));

        Assert.StartsWith("Test (#7)", Run("journal 7")[0]);
        Assert.StartsWith("Test (#7)", Run("journal test")[0]);
    }

    [Fact]
    public void Journal_of_nobody_is_an_error_that_suggests_npcs()
    {
        var line = RunOne("journal Bob");

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("npcs", line.Text);
    }

    [Fact]
    public void Journal_with_a_shared_name_asks_for_an_id()
    {
        _world.AddNpc(new TestNpc(0, 0, 1));
        _world.AddNpc(new TestNpc(0, 0, 2));

        var line = RunOne("journal Test");

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("1, 2", line.Text);
    }

    // ------------------------------------------------------------------ nav

    [Fact]
    public void Nav_turns_the_overlay_on_then_off()
    {
        RunOne("nav");
        Assert.True(Debug.SHOWNAV);

        RunOne("nav");
        Assert.False(Debug.SHOWNAV);
    }

    [Fact]
    public void An_NPC_walking_somewhere_shows_its_path_ending_on_the_target()
    {
        var (npc, brain) = AddNpc();
        npc.StandAt(new Vector2(0.5f, 0.5f));
        var target = new Vector2(4.5f, 4.9f);
        npc.GiveJob(new GoToJob(target));

        brain.Update(new GameTime());   // the first tick plans the route

        Assert.NotEmpty(npc.CurrentPath);
        Assert.Equal(target, npc.CurrentPath[^1]);
    }

    [Fact]
    public void An_NPC_on_patrol_shows_its_path_to_the_next_stop()
    {
        var (npc, brain) = AddNpc();
        npc.StandAt(new Vector2(0.5f, 0.5f));
        var stop = new Vector2(4.5f, 4.9f);
        npc.GiveJob(new PatrolJob(new[] { stop, new Vector2(0.5f, 0.5f) }));

        brain.Update(new GameTime());

        Assert.Equal(stop, npc.CurrentPath[^1]);
    }

    [Fact]
    public void An_NPC_following_someone_shows_its_path_to_them()
    {
        var (npc, brain) = AddNpc();
        npc.StandAt(new Vector2(0.5f, 0.5f));
        _world.player.StandAt(new Vector2(4.5f, 4.9f));
        npc.GiveJob(new FollowJob(_world.player.Id));

        brain.Update(new GameTime());

        Assert.Equal(_world.player.Feet, npc.CurrentPath[^1]);
    }

    [Fact]
    public void An_NPC_with_nothing_to_do_has_no_path()
    {
        var (npc, _) = AddNpc();

        Assert.Empty(npc.CurrentPath);
    }

    // ------------------------------------------------------------------ health

    // An NPC with health, for the health commands.
    private TestNpc HurtableNpc(int id = 7, int max = 80)
    {
        var npc = new TestNpc(0, 0, id);
        npc.SetHealth(max);
        _world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void Hp_with_no_argument_shows_the_players_health()
    {
        Assert.Equal($"{_world.player.Name} (you): 100/100", RunOne("hp").Text);
    }

    [Fact]
    public void Damage_hurts_an_NPC_and_hp_shows_it()
    {
        var npc = HurtableNpc();

        Assert.Equal("Test (#7): 55/80", RunOne("damage 7 25").Text);
        Assert.Equal(55, npc.Health.Current);
        Assert.Equal("Test (#7): 55/80", RunOne("hp test").Text);
    }

    [Fact]
    public void Damage_lands_every_time_it_is_typed()
    {
        // No invulnerability from the console: each command is its own hit.
        HurtableNpc();

        RunOne("damage 7 10");

        Assert.Equal("Test (#7): 60/80", RunOne("damage 7 10").Text);
    }

    [Fact]
    public void Kill_kills_and_heal_brings_back_to_full()
    {
        var npc = HurtableNpc();

        Assert.Equal("Test (#7): 0/80, knocked out (20s left)", RunOne("kill 7").Text);
        Assert.Equal(NpcBrain.NPCState.Dead, npc.State);

        Assert.Equal("Test (#7): 80/80", RunOne("heal 7").Text);
        Assert.NotEqual(NpcBrain.NPCState.Dead, npc.State);
    }

    [Fact]
    public void Heal_by_an_amount()
    {
        HurtableNpc();
        RunOne("damage 7 50");

        Assert.Equal("Test (#7): 50/80", RunOne("heal 7 20").Text);
    }

    [Fact]
    public void God_mode_blocks_damage_to_the_player_and_toggles_off()
    {
        Assert.Equal("God mode on: nothing hurts you.", RunOne("god").Text);
        Assert.Equal(LineKind.Error, RunOne("damage player 10").Kind);
        Assert.Equal(100, _world.player.Health.Current);

        RunOne("god");
        RunOne("damage me 10");
        Assert.Equal(90, _world.player.Health.Current);
    }

    [Theory]
    [InlineData("damage 7 0")]
    [InlineData("damage 7 lots")]
    [InlineData("heal 7 -3")]
    [InlineData("damage Bob 5")]
    public void Bad_health_commands_are_errors(string input)
    {
        HurtableNpc();

        Assert.Equal(LineKind.Error, RunOne(input).Kind);
    }

    [Fact]
    public void Someone_with_no_health_cant_be_hurt()
    {
        _world.AddNpc(new TestNpc(0, 0, 9));   // TestNpc has no health unless given one

        Assert.Equal(LineKind.Error, RunOne("damage 9 5").Kind);
    }

    [Fact]
    public void Killing_the_dead_is_an_error()
    {
        HurtableNpc();
        RunOne("kill 7");

        Assert.Equal(LineKind.Error, RunOne("kill 7").Kind);
    }

    // ------------------------------------------------------------------ fighting

    [Fact]
    public void Aggro_makes_an_NPC_fight_the_player_and_calm_ends_it()
    {
        var npc = HurtableNpc();

        Assert.Equal($"Test (#7) is fighting {_world.player.Name}.", RunOne("aggro 7").Text);
        Assert.Equal(NpcBrain.NPCState.Fighting, npc.State);
        Assert.Same(_world.player, npc.FightingWith);

        Assert.Equal(LineKind.Output, RunOne("calm 7").Kind);
        Assert.Null(npc.FightingWith);
    }

    [Fact]
    public void Aggro_can_set_an_NPC_on_another_NPC()
    {
        var npc = HurtableNpc();
        var other = HurtableNpc(id: 8);

        RunOne("aggro 7 8");

        Assert.Same(other, npc.FightingWith);
    }

    [Theory]
    [InlineData("aggro 7 7")]        // itself
    [InlineData("aggro Bob")]        // nobody
    [InlineData("calm 7")]           // isn't fighting
    public void Bad_fight_commands_are_errors(string input)
    {
        HurtableNpc();

        Assert.Equal(LineKind.Error, RunOne(input).Kind);
    }

    [Fact]
    public void A_dead_NPC_cant_be_set_fighting()
    {
        HurtableNpc();
        RunOne("kill 7");

        Assert.Equal(LineKind.Error, RunOne("aggro 7").Kind);
    }
}
