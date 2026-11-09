using Ashenveil.Core;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Picking fights and joining them (combat step 5), first with no World - NPCs, a log and
/// Aggro on their own - then the enemy brain, the bandit, spawning, and a few whole-world fights.
/// </summary>
public class AggroTests : IDisposable
{
    private int _day = 1;
    private readonly EventLog _log;
    private readonly Aggro _aggro;

    public AggroTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = new FakeInput();
        _log = new EventLog(() => _day);
        _aggro = new Aggro(_log, () => _day);
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    // An NPC of some factions, with its faction opinions seeded as a spawned one's are.
    private static TestNpc Member(float x, float y, int id, string faction, NpcBrain? brain = null)
    {
        var npc = new TestNpc(x, y, id, $"{faction}{id}", brain ?? new KnightBrain(new Random(1)));
        npc.SetHealth(50);
        npc.Factions.Add(faction);
        npc.Opinions.Seed(npc.Factions, FactionTable.Default);
        return npc;
    }

    private static Player Traveller(float x, float y)
    {
        var player = new Player(new Vector2(x, y), 0);
        player.SeedStanding(FactionTable.Default);
        return player;
    }

    private void Look(IReadOnlyList<NPC> npcs, params Entity[] people) =>
        _aggro.Update(Aggro.LookSeconds, npcs, people);

    // ------------------------------------------------------------------ picking fights

    [Fact]
    public void A_guard_goes_for_a_bandit_in_range_with_nobody_telling_him()
    {
        var guard = Member(0, 0, 1, "guard");
        var bandit = Member(2, 0, 2, "bandit");

        Look(new NPC[] { guard }, guard, bandit);

        Assert.Same(bandit, guard.FightingWith);
    }

    [Fact]
    public void Nobody_is_picked_out_of_range()
    {
        var guard = Member(0, 0, 1, "guard");
        var bandit = Member(NpcBrain.DefaultAggroRange + 1, 0, 2, "bandit");

        Look(new NPC[] { guard }, guard, bandit);

        Assert.Null(guard.FightingWith);
    }

    [Fact]
    public void It_only_looks_a_few_times_a_second()
    {
        var guard = Member(0, 0, 1, "guard");
        var bandit = Member(2, 0, 2, "bandit");

        _aggro.Update(Aggro.LookSeconds / 2, new NPC[] { guard }, new Entity[] { bandit });
        Assert.Null(guard.FightingWith);

        _aggro.Update(Aggro.LookSeconds / 2, new NPC[] { guard }, new Entity[] { bandit });
        Assert.Same(bandit, guard.FightingWith);
    }

    [Fact]
    public void The_nearest_one_is_picked()
    {
        var guard = Member(0, 0, 1, "guard");
        var far = Member(3, 0, 2, "bandit");
        var near = Member(0, 1.5f, 3, "bandit");

        Look(new NPC[] { guard }, far, near);

        Assert.Same(near, guard.FightingWith);
    }

    [Fact]
    public void A_guard_leaves_a_traveller_alone_but_a_bandit_goes_for_them()
    {
        var guard = Member(0, 0, 1, "guard");
        var bandit = Member(4, 0, 2, "bandit", new EnemyBrain(new Random(1)));
        var player = Traveller(2, 0);

        Look(new NPC[] { guard, bandit }, player);

        Assert.Null(guard.FightingWith);         // neutral
        Assert.Same(player, bandit.FightingWith);  // wary is enough for an enemy
    }

    [Fact]
    public void Todays_grudge_makes_a_guard_go_for_the_player()
    {
        var guard = Member(0, 0, 1, "guard");
        var player = Traveller(2, 0);
        Look(new NPC[] { guard }, player);
        Assert.Null(guard.FightingWith);

        _log.Post(GameEvent.Of(EventType.Attacked, "field", guard.Feet, player.Id, guard.Id, "Traveller attacked guard1"));
        Look(new NPC[] { guard }, player);

        Assert.Same(player, guard.FightingWith);
    }

    [Fact]
    public void The_down_neither_pick_fights_nor_get_picked()
    {
        var guard = Member(0, 0, 1, "guard");
        var bandit = Member(2, 0, 2, "bandit");
        bandit.Health.Kill();

        Look(new NPC[] { guard }, bandit);
        Assert.Null(guard.FightingWith);

        var other = Member(2, 1, 3, "bandit");
        guard.Health.Kill();
        Look(new NPC[] { guard }, other);
        Assert.Null(guard.FightingWith);
    }

    [Fact]
    public void Someone_already_fighting_doesnt_switch_to_a_new_target()
    {
        var guard = Member(0, 0, 1, "guard");
        var first = Member(3, 0, 2, "bandit");
        var nearer = Member(1, 0, 3, "bandit");
        guard.StartFighting(first);

        Look(new NPC[] { guard }, nearer);

        Assert.Same(first, guard.FightingWith);
    }

    // ------------------------------------------------------------------ joining in

    [Fact]
    public void A_guard_who_sees_the_player_hit_another_guard_joins_against_the_player()
    {
        var victim = Member(0, 0, 1, "guard");
        var friend = Member(3, 0, 2, "guard");
        var player = Traveller(1, 0);

        _aggro.JoinIn(player, victim, new NPC[] { victim, friend });

        Assert.Same(player, friend.FightingWith);
    }

    [Fact]
    public void A_guard_who_sees_the_player_hit_a_bandit_joins_against_the_bandit()
    {
        var bandit = Member(0, 0, 1, "bandit");
        var guard = Member(3, 0, 2, "guard");
        var player = Traveller(1, 0);

        _aggro.JoinIn(player, bandit, new NPC[] { bandit, guard });

        Assert.Same(bandit, guard.FightingWith);
    }

    [Fact]
    public void Nobody_takes_sides_in_a_fight_they_dont_care_about_or_cant_see()
    {
        var a = new TestNpc(0, 0, 1);   // no factions: neutral to everyone
        var b = new TestNpc(1, 0, 2);
        var onlooker = new TestNpc(2, 0, 3);
        var farFriend = Member(Aggro.JoinRange + 5, 0, 4, "guard");
        var guard = Member(0, 1, 5, "guard");
        a.SetHealth(10); b.SetHealth(10);

        _aggro.JoinIn(a, b, new NPC[] { a, b, onlooker, farFriend });
        _aggro.JoinIn(guard, a, new NPC[] { farFriend });

        Assert.Null(onlooker.FightingWith);
        Assert.Null(farFriend.FightingWith);
    }

    // ------------------------------------------------------------------ the enemy brain

    [Fact]
    public void An_enemy_dragged_too_far_from_home_gives_up_walks_back_and_leaves_people_alone_meanwhile()
    {
        var brain = new EnemyBrain(new Random(1)) { Home = new Vector2(0.5f, 1) };
        var bandit = Member(EnemyBrain.LeashDistance + 1, 0, 1, "bandit", brain);
        var player = Traveller(EnemyBrain.LeashDistance + 2, 0);
        bandit.StartFighting(player);

        bandit.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.05)));

        Assert.Null(bandit.FightingWith);
        Assert.Equal(NpcBrain.NPCState.Working, bandit.State);   // the walk home
        Assert.True(brain.IsReturning);
        Look(new NPC[] { bandit }, player);
        Assert.Null(bandit.FightingWith);
    }

    [Fact]
    public void An_enemy_ambles_back_towards_home_when_it_drifts_off()
    {
        var brain = new EnemyBrain(new Random(1)) { Home = new Vector2(0.5f, 1) };
        var bandit = Member(5, 0, 1, "bandit", brain);
        bandit.Speed = 1;

        for (int i = 0; i < 10; i++) bandit.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)));

        Assert.True(bandit.Feet.X < 5.5f);
    }

    // ------------------------------------------------------------------ the bandit

    [Fact]
    public void A_bandit_is_made_by_type_dies_for_good_and_belongs_to_the_bandits()
    {
        var bandit = Assert.IsType<Bandit>(NpcFactory.Make("Bandit", 3, "Grim"));

        Assert.True(bandit.DiesForGood);
        Assert.False(bandit.Admits);
        Assert.NotEmpty(bandit.Attacks);
        Assert.Equal(new[] { "bandit" }, NpcFactory.DefaultFactions("Bandit"));
        Assert.Equal("Bandit", NpcFactory.Canonical("bandit"));

        bandit.Health.Kill();
        Assert.False(bandit.IsKnockedOut);
    }

    // ------------------------------------------------------------------ spawning from a level

    private const string SpawnLevel = """
        {
          "cols": 8, "rows": 5,
          "tiles": [["grass","grass","grass","grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass","grass","grass","grass"]],
          "pois": [ { "name": "Player_spawn", "x": 0, "y": 0 },
                    { "name": "camp", "x": 6, "y": 3 },
                    { "name": "well", "x": 3, "y": 3 } ],
          "npcs": [
            { "type": "Bandit", "at": "camp" },
            { "type": "Knight", "name": "Sir Kay", "at": "well", "factions": ["guard", "village"],
              "tasks": [ { "type": "wait", "seconds": 2 }, { "type": "go_to", "target": "camp" },
                         { "type": "go_to", "target": "nowhere" } ] },
            { "type": "Dragon", "at": "camp" },
            { "type": "Knight", "at": "nowhere" }
          ]
        }
        """;

    [Fact]
    public void A_level_spawns_its_npcs_with_their_defaults_errands_and_points()
    {
        var level = TestLevels.Location(SpawnLevel);
        var roster = new NpcRoster();

        roster.Populate(level);
        var npcs = roster.In(level.Name);

        Assert.Equal(3, npcs.Count);   // the dragon isn't a type the game knows
        var bandit = Assert.IsType<Bandit>(npcs[0]);
        Assert.Equal(Bandit.DefaultName, bandit.Name);
        Assert.Equal(new[] { "bandit" }, bandit.Factions);
        Near.Equal(level.Poi("camp").StandingSpot(), bandit.Feet);

        var kay = Assert.IsType<Knight>(npcs[1]);
        Assert.Equal("Sir Kay", kay.Name);
        Assert.Equal(new[] { "guard", "village" }, kay.Factions);
        Assert.Equal(2, kay.Jobs.Count);   // the go_to nowhere is dropped
        Assert.Equal(FactionTable.Default.View("guard", "bandit"), kay.Opinions.OfFaction("bandit"));

        Near.Equal(level.Poi("default").StandingSpot(), npcs[2].Feet);   // no such point: default
        Assert.Equal("Knight", npcs[2].Name);
    }

    // ------------------------------------------------------------------ whole worlds

    private static World NewWorld()
    {
        var world = new World(TestLevels.Location(SpawnLevel),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(new NpcDecisionDto("x", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>())),
            requestReflection: _ => Task.FromResult<ReflectionDto>(null!));
        world.Init();
        return world;
    }

    private static void Step(World world, double seconds = 0.05) =>
        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    // The test bounds.json gives the knight's art no body (so no swing of his can land here):
    // these two are 1x1-cell test NPCs that fight like a guard and a bandit.
    private static TestNpc Fighter(World world, Vector2 feet, int id, string faction, NpcBrain brain)
    {
        var npc = Member(0, 0, id, faction, brain);
        npc.SetHealth(40);
        npc.Speed = 1;
        npc.TestAttacks = new[] { new AttackData(8, 0.6f, 0.8f, 0.1f, 0.1f, 0.1f, 0.2f, "attack") };
        npc.StandAt(feet);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void A_guard_and_a_bandit_fight_it_out_with_nobody_involved()
    {
        var world = NewWorld();
        var where = world.location.Poi("well").StandingSpot();
        var guard = Fighter(world, where, 20, "guard", new KnightBrain(new Random(1)));
        var bandit = Fighter(world, where + new Vector2(1.5f, 0), 21, "bandit", new EnemyBrain(new Random(1)));

        Step(world, Aggro.LookSeconds);
        Assert.Same(bandit, guard.FightingWith);
        Assert.Same(guard, bandit.FightingWith);
        var kay = guard;

        for (int i = 0; i < 400 && !bandit.IsDead && !kay.IsDead; i++) Step(world);
        for (int i = 0; i < 3; i++) Step(world);

        Assert.True(bandit.IsDead || kay.IsDead);
        Assert.Contains(world.Events.All, e => e.Type is EventType.Killed or EventType.KnockedOut or EventType.Attacked);
    }

    [Fact]
    public void Hitting_a_guard_in_front_of_another_brings_the_other_one_in()
    {
        var input = new FakeInput();
        AshenveilGame.Input = input;
        var world = new World(TestLevels.Location("""
            { "cols": 6, "rows": 5,
              "tiles": [["grass","grass","grass","grass","grass","grass"],["grass","grass","grass","grass","grass","grass"],
                        ["grass","grass","grass","grass","grass","grass"],["grass","grass","grass","grass","grass","grass"],
                        ["grass","grass","grass","grass","grass","grass"]],
              "pois": [ { "name": "Player_spawn", "x": 1, "y": 1 } ] }
            """),
            sendChat: _ => Task.FromResult(Array.Empty<ChatMessage>()),
            requestDecision: _ => Task.FromResult(new NpcDecisionDto("x", Array.Empty<string>(), "resume", false, Array.Empty<TaskSpecDto>())));
        world.Init();
        var feet = world.player.Feet;
        // Just below the player, who faces down to start with; the friend a little way off.
        var victim = Member(feet.X - 0.5f, feet.Y + 0.05f, 7, "guard");
        var friend = Member(feet.X + 2f, feet.Y, 8, "guard");
        world.AddNpc(victim);
        world.AddNpc(friend);

        input.Hold(Microsoft.Xna.Framework.Input.Keys.Space);
        for (int i = 0; i < 5; i++) Step(world);

        Assert.True(victim.Health.Current < 50);
        Assert.Same(world.player, victim.FightingWith);
        Assert.Same(world.player, friend.FightingWith);
    }
}
