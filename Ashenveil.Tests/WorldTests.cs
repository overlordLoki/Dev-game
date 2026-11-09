using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
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
            requestDecision: req => { _decisionsAsked.Add(req); return Task.FromResult(_decisionReply); },
            requestReflection: _ => Task.FromResult<ReflectionDto>(null!));
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
        Conversation? shown = null;
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

    // ------------------------------------------------------------------ finding people nearby

    // An NPC with a known id, put in some other level than the player's.
    private static TestNpc NpcElsewhere(World world, int id)
    {
        var npc = new TestNpc(1, 1, id);
        world.Roster.Add(TestLevels.Location(Level), npc);   // a fresh throwaway level, its own name
        return npc;
    }

    [Fact]
    public void An_NPC_can_find_another_NPC_in_its_own_level()
    {
        var world = NewWorld();
        var asker = new TestNpc(1, 1, 900);
        var other = new TestNpc(3, 3, 901);
        world.AddNpc(asker);
        world.AddNpc(other);

        Assert.Same(other, world.FindPersonNear(asker, 901));
        Assert.Same(other, asker.FindPerson(901));   // the same lookup, through the roster's wiring
    }

    [Fact]
    public void An_NPC_cant_find_an_NPC_in_another_level()
    {
        var world = NewWorld();
        var asker = new TestNpc(1, 1, 900);
        world.AddNpc(asker);
        NpcElsewhere(world, 901);

        Assert.NotNull(world.FindPerson(901));       // they exist...
        Assert.Null(world.FindPersonNear(asker, 901));  // ...just not here
    }

    [Fact]
    public void An_NPC_in_the_players_level_can_find_the_player()
    {
        var world = NewWorld();
        var asker = new TestNpc(1, 1, 900);
        world.AddNpc(asker);

        Assert.Same(world.player, world.FindPersonNear(asker, world.player.Id));
    }

    [Fact]
    public void An_NPC_in_another_level_cant_find_the_player()
    {
        var world = NewWorld();
        var asker = NpcElsewhere(world, 900);

        Assert.Null(world.FindPersonNear(asker, world.player.Id));
    }

    [Fact]
    public void Nobody_is_found_for_an_unknown_id()
    {
        var world = NewWorld();
        var asker = new TestNpc(1, 1, 900);
        world.AddNpc(asker);

        Assert.Null(world.FindPersonNear(asker, 12345));
    }

    // ------------------------------------------------------------------ NPCs walking up to talk

    [Fact]
    public void An_NPC_walking_up_to_the_player_opens_the_chat_box()
    {
        var world = NewWorld();
        var (npc, _) = NpcBeside(world);
        Conversation? shown = null;
        world.PlayerConversationStarted += c => shown = c;

        bool started = npc.StartConversation(world.player, "warn them about the wolves");

        Assert.True(started);
        Assert.NotNull(shown);
        Assert.Contains(npc, shown.Participants);
        Assert.Contains(world.player, shown.Participants);
    }

    [Fact]
    public void An_NPC_cant_start_a_conversation_with_another_NPC_yet()
    {
        var world = NewWorld();
        var (a, _) = NpcBeside(world);
        var (b, _) = NpcBeside(world);

        Assert.False(a.StartConversation(b, "hello"));
        Assert.Empty(world.Conversations.Active);
    }

    [Fact]
    public void An_NPC_cant_talk_to_the_player_while_theyre_already_talking()
    {
        var world = NewWorld();
        var (a, _) = NpcBeside(world);
        var (b, _) = NpcBeside(world);
        world.StartConversation(world.player, a);

        Assert.False(b.StartConversation(world.player, "hello"));
        Assert.Single(world.Conversations.Active);
    }

    [Fact]
    public void Once_the_players_chat_has_ended_an_NPC_can_talk_to_them()
    {
        var world = NewWorld();
        var (a, _) = NpcBeside(world);
        var (b, _) = NpcBeside(world);
        world.StartConversation(world.player, a).End();

        Assert.True(b.StartConversation(world.player, "hello"));
    }

    [Fact]
    public void An_NPC_in_another_level_cant_talk_to_the_player()
    {
        var world = NewWorld();
        var npc = NpcElsewhere(world, 900);

        Assert.False(world.NpcStartsConversation(npc, world.player, "hello"));
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
        var fact = Assert.Single(brain.Journal.Recent(10));
        Assert.Equal("The traveller's name is Loki.", fact.Text);
        Assert.Equal(transcript.Id, fact.SourceId);
        Assert.Equal(transcript.Day, fact.Day);
    }

    // ------------------------------------------------------------------ the dead

    [Fact]
    public void A_dead_NPC_cant_be_talked_to()
    {
        var world = NewWorld();
        var (npc, _) = NpcBeside(world);
        npc.SetHealth(10);
        npc.Health.Kill();

        Assert.Null(world.Interact());
    }

    [Fact]
    public void A_dead_NPC_cant_walk_up_and_talk()
    {
        var world = NewWorld();
        var (npc, _) = NpcBeside(world);
        npc.SetHealth(10);
        npc.Health.Kill();

        Assert.False(npc.StartConversation(world.player, "hello"));
    }

    [Fact]
    public void A_dead_player_cant_talk_or_be_talked_to()
    {
        var world = NewWorld();
        var (npc, _) = NpcBeside(world);
        world.player.Health.Kill();

        Assert.Null(world.Interact());
        Assert.False(npc.StartConversation(world.player, "hello"));
    }

    [Fact]
    public void The_player_walks_over_a_dead_NPC_instead_of_being_pushed()
    {
        var world = NewWorld();
        var npc = new TestNpc(world.player.Position.X, world.player.Position.Y);   // right on top
        world.AddNpc(npc);
        npc.SetHealth(10);
        npc.Health.Kill();
        var where = world.player.Position;

        Step(world, 0.1);

        Assert.Equal(where, world.player.Position);
    }

    // ------------------------------------------------------------------ attacking

    [Fact]
    public void Pressing_Space_beside_an_NPC_hurts_it_and_says_so()
    {
        var input = new FakeInput();
        AshenveilGame.Input = input;
        var world = NewWorld();
        // Just below the player, who faces down to start with.
        var feet = world.player.Feet;
        var npc = new TestNpc(feet.X - 0.5f, feet.Y + 0.05f);
        world.AddNpc(npc);
        npc.SetHealth(80);
        var hits = new List<(Entity, Entity, int)>();
        world.Combat.Hit += (attacker, victim, damage) => hits.Add((attacker, victim, damage));

        input.Hold(Microsoft.Xna.Framework.Input.Keys.Space);
        for (int i = 0; i < 5; i++) Step(world, 0.05);

        Assert.Equal(80 - Player.Sword.Damage, npc.Health.Current);
        Assert.Equal((world.player, npc, Player.Sword.Damage), Assert.Single(hits));
        Assert.True(npc.Position.Y > feet.Y + 0.05f);   // knocked back, down and away
    }

    [Fact]
    public void Swinging_at_nothing_hurts_nobody()
    {
        var input = new FakeInput();
        AshenveilGame.Input = input;
        var world = NewWorld();
        var feet = world.player.Feet;
        var npc = new TestNpc(feet.X - 0.5f, feet.Y - 2f);   // behind the player, out of reach
        world.AddNpc(npc);
        npc.SetHealth(80);

        input.Hold(Microsoft.Xna.Framework.Input.Keys.Space);
        for (int i = 0; i < 10; i++) Step(world, 0.05);

        Assert.Equal(80, npc.Health.Current);
    }

    // ------------------------------------------------------------------ fighting back

    // An NPC with a KnightBrain (it stands still when not busy), some health and an attack.
    private static TestNpc Fighter(World world, float x, float y, int id)
    {
        var npc = new TestNpc(x, y, id, $"Fighter{id}", new Ashenveil.Core.Entities.brains.KnightBrain(new Random(1)));
        npc.SetHealth(80);
        npc.Speed = 1;
        npc.TestAttacks = new[] { new Ashenveil.Core.Combat.AttackData(5, 0.6f, 0.8f, 0.1f, 0.1f, 0.1f, 0.2f, "attack") };
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void Hitting_an_NPC_makes_it_fight_back()
    {
        var input = new FakeInput();
        AshenveilGame.Input = input;
        var world = NewWorld();
        var feet = world.player.Feet;
        var npc = Fighter(world, feet.X - 0.5f, feet.Y + 0.05f, 7);

        input.Hold(Microsoft.Xna.Framework.Input.Keys.Space);
        for (int i = 0; i < 5; i++) Step(world, 0.05);

        Assert.Equal(NpcBrain.NPCState.Fighting, npc.State);
        Assert.Same(world.player, npc.FightingWith);
    }

    // ------------------------------------------------------------------ fights become events

    [Fact]
    public void A_fight_is_one_event_and_one_hot_memory_once_it_goes_quiet()
    {
        var world = NewWorld();
        var feet = world.player.Feet;
        var npc = Fighter(world, feet.X - 0.5f, feet.Y + 0.05f, 7);
        var hits = 0;
        world.Combat.Hit += (_, _, _) => hits++;
        npc.StartFighting(world.player);

        for (int i = 0; i < 40; i++) Step(world, 0.05);
        Assert.True(hits > 1);
        Assert.DoesNotContain(world.Events.All, e => e.Type == EventType.Attacked);   // still going

        npc.StopFighting();
        for (int i = 0; i < 110; i++) Step(world, 0.1);   // past FightRecorder.QuietSeconds

        var fight = Assert.Single(world.Events.All, e => e.Type == EventType.Attacked);
        Assert.Equal(npc.Id, fight.ActorId);
        Assert.Equal(world.player.Id, fight.TargetId);
        Assert.Single(npc.Journal.Memories, m => m.Kind == MemoryKind.Event && m.SourceId == fight.Id);
    }

    [Fact]
    public void Saving_mid_fight_posts_the_fight_first()
    {
        var world = NewWorld();
        var feet = world.player.Feet;
        var npc = Fighter(world, feet.X - 0.5f, feet.Y + 0.05f, 7);
        npc.StartFighting(world.player);
        for (int i = 0; i < 20; i++) Step(world, 0.05);

        world.Fights.EndAll();   // what SaveLibrary does before it saves

        Assert.Single(world.Events.All, e => e.Type == EventType.Attacked);
        Assert.Empty(world.Fights.Open);
    }

    [Fact]
    public void Only_the_victim_of_a_fight_holds_a_grudge()
    {
        var world = NewWorld();
        var a = Fighter(world, 0.2f, 0.2f, 7);
        var b = Fighter(world, 1.2f, 0.2f, 8);
        world.Fights.Landed(a, b, 5);
        world.Fights.Landed(b, a, 5);

        world.Fights.EndAll();

        Assert.Equal(Attitude.Hostile, Relations.AttitudeTo(b, a, world.Events, world.Clock.TotalDays));
        Assert.NotEqual(Attitude.Hostile, Relations.AttitudeTo(a, b, world.Events, world.Clock.TotalDays));
    }

    [Fact]
    public void An_NPC_that_knocks_out_the_player_who_attacked_it_still_holds_the_grudge()
    {
        var world = NewWorld();
        var feet = world.player.Feet;
        var npc = Fighter(world, feet.X - 0.5f, feet.Y + 0.05f, 7);
        world.Fights.Landed(world.player, npc, 5);
        world.Fights.Landed(npc, world.player, 5);
        world.player.Health.Kill();

        Step(world, 0.05);

        var fight = Assert.Single(world.Events.All, e => e.Type == EventType.Attacked);
        Assert.Equal(world.player.Id, fight.ActorId);
        Assert.Equal(Attitude.Hostile, Relations.AttitudeTo(npc, world.player, world.Events, world.Clock.TotalDays));
    }

    [Fact]
    public void An_NPC_that_fights_the_player_hurts_them()
    {
        var world = NewWorld();
        var feet = world.player.Feet;
        var npc = Fighter(world, feet.X - 0.5f, feet.Y + 0.05f, 7);
        npc.StartFighting(world.player);

        for (int i = 0; i < 20; i++) Step(world, 0.05);

        Assert.True(world.player.Health.Current < Player.MaxHealth);
    }

    [Fact]
    public void An_NPC_mid_fight_cant_be_talked_to()
    {
        var world = NewWorld();
        var (npc, _) = NpcBeside(world);
        var other = new TestNpc(0, 0, 9);
        other.SetHealth(10);
        world.AddNpc(other);
        npc.StartFighting(other);

        Assert.Null(world.Interact());
    }

    [Fact]
    public void Two_NPCs_fight_with_no_player_involved()
    {
        var world = NewWorld();
        var a = Fighter(world, 0.2f, 0.2f, 7);
        var b = Fighter(world, 1.2f, 0.2f, 8);

        a.StartFighting(b);
        for (int i = 0; i < 10; i++) Step(world, 0.05);

        Assert.True(b.Health.Current < 80);
        Assert.Same(a, b.FightingWith);   // hit, so it fights back
    }

    // ------------------------------------------------------------------ dying and getting up

    [Fact]
    public void A_dead_player_respawns_on_the_spawn_point_with_full_health()
    {
        var world = NewWorld();
        var spawn = world.player.Feet;
        bool respawned = false;
        world.PlayerRespawned += () => respawned = true;
        world.player.StandAt(spawn + new Vector2(1.5f, 1f));
        world.player.Health.TakeDamage(30);
        world.player.Health.Kill();

        Step(world, World.RespawnSeconds - 0.5);
        Assert.True(world.player.IsDead);
        Assert.False(respawned);

        Step(world, 1);
        Assert.True(respawned);
        Assert.False(world.player.IsDead);
        Assert.Equal(world.player.Health.Max, world.player.Health.Current);
        Assert.Equal(Player.PlayerState.Idle, world.player.State);
        Near.Equal(spawn, world.player.Feet);
        Assert.Equal(0, world.PlayerDeadFor);
    }

    [Fact]
    public void A_player_healed_before_the_respawn_stays_where_they_are()
    {
        var world = NewWorld();
        var spot = world.player.Feet + new Vector2(1f, 0);
        world.player.StandAt(spot);
        world.player.Health.Kill();
        Step(world, 1);

        world.player.Health.Heal(10);
        Step(world, World.RespawnSeconds);

        Near.Equal(spot, world.player.Feet);
        Assert.Equal(10, world.player.Health.Current);
    }

    [Fact]
    public void Knocking_out_an_NPC_posts_one_knock_out_it_remembers_and_it_gets_up_later()
    {
        var world = NewWorld();
        var (npc, brain) = NpcBeside(world);
        npc.SetHealth(10);
        world.Fights.Landed(world.player, npc, 10);
        npc.Health.Kill();

        Step(world, 0.05);

        var e = Assert.Single(world.Events.All, e => e.Type == EventType.KnockedOut);
        Assert.Equal(world.player.Id, e.ActorId);
        Assert.Contains(brain.Journal.EventsOn(world.Clock.TotalDays), m => m.SourceId == e.Id);
        Assert.Contains(world.player.Id, Relations.Grudges(world.Events, npc, world.Clock.TotalDays));

        for (int i = 0; i < 21; i++) Step(world, 1);
        Assert.False(npc.IsDead);
    }

    [Fact]
    public void An_NPC_gives_up_when_the_player_dies_and_goes_back_to_its_errand()
    {
        var world = NewWorld();
        var feet = world.player.Feet;
        var npc = Fighter(world, feet.X - 0.5f, feet.Y + 0.05f, 7);
        npc.GiveJob(new Ashenveil.Core.Entities.brains.Jobs.WaitJob(100));
        npc.StartFighting(world.player);

        world.player.Health.Kill();
        Step(world, 0.05);

        Assert.Equal(NpcBrain.NPCState.Working, npc.State);
    }
}
