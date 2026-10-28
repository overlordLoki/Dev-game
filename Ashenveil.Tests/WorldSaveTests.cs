using System.Text.Json;
using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Jobs;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Saving;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// World.FromSave, the load path: a real game in Medows (the knight and his walk included) is
/// saved with SaveMapper and loaded back, no database involved. What comes back has to carry
/// on exactly where it left off - same player, clock, ids, NPCs and what they remember.
/// </summary>
public class WorldSaveTests : IDisposable
{
    private static readonly DateTime SavedAt = new(2026, 10, 27, 9, 30, 0, DateTimeKind.Utc);

    // Every end-of-conversation decision: a summary and one fact.
    private static Task<NpcDecisionDto> Decision(DecisionRequestDto _) =>
        Task.FromResult(new NpcDecisionDto("Met Loki.", new[] { "The traveller's name is Loki." }, "resume", false, Array.Empty<TaskSpecDto>()));

    private static Task<ChatMessage[]> NoReply(ConversationRequestDto _) => Task.FromResult(Array.Empty<ChatMessage>());

    // Every day-end reflection: a diary entry and one fact.
    private static Task<ReflectionDto> Reflection(ReflectionRequestDto req) =>
        Task.FromResult(new ReflectionDto { diary = $"On {req.date} I met Loki.", facts = new[] { "Loki is friendly." } });

    public WorldSaveTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = new FakeInput();   // no keys held: the player stands still
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    // A new game in Medows, as the game starts one.
    private static World NewGame()
    {
        var world = new World(new Location("Medows"), NoReply, Decision, Reflection);
        world.Init();
        return world;
    }

    private static World Load(SaveData save) => World.FromSave(save, NoReply, Decision, Reflection);

    private static NPC Knight(World world) => Assert.IsType<Knight>(Assert.Single(world.Roster.All));

    private static void Step(World world, double seconds = 1 / 60.0) =>
        world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));

    // The player tells the knight their name and walks off; the decision lands a frame later.
    private static void TalkToTheKnight(World world)
    {
        var convo = world.StartConversation(world.player, (NPC)Knight(world));
        convo.Add(world.player.Name, "My name is Loki.");
        convo.End();
        Step(world);
        Step(world);
    }

    private static string Json(SaveData save) => JsonSerializer.Serialize(save);

    // ------------------------------------------------------------------ the whole thing

    [Fact]
    public void Loading_a_save_and_saving_it_again_gives_the_same_save()
    {
        var world = NewGame();
        world.player.Name = "Loki";
        TalkToTheKnight(world);
        world.Clock.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1300)));   // two days and a bit
        var save = SaveMapper.ToSave(world, SavedAt);

        var again = SaveMapper.ToSave(Load(save), SavedAt);

        Assert.Equal(Json(save), Json(again));
    }

    // ------------------------------------------------------------------ piece by piece

    [Fact]
    public void The_player_comes_back_with_their_id_name_level_and_feet()
    {
        var world = NewGame();
        world.player.Name = "Loki";
        world.player.StandAt(new Vector2(4.5f, 6.9f));

        var loaded = Load(SaveMapper.ToSave(world, SavedAt));

        Assert.Equal(world.player.Id, loaded.player.Id);
        Assert.Equal("Loki", loaded.player.Name);
        Assert.Equal("Medows", loaded.location.Name);
        Near.Equal(new Vector2(4.5f, 6.9f), loaded.player.Feet);
    }

    [Fact]
    public void The_clock_comes_back_on_the_same_day_and_time_of_day()
    {
        var world = NewGame();
        world.Clock.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(600 * 40 + 125)));   // day 41, 125s in

        var loaded = Load(SaveMapper.ToSave(world, SavedAt));

        Assert.Equal(41, loaded.Clock.TotalDays);
        Assert.Equal(world.Clock.DateDisplay, loaded.Clock.DateDisplay);   // "Summer 11, Year 1"
        Near.Equal(125f, loaded.Clock.SecondsIntoDay);
    }

    [Fact]
    public void The_knight_remembers_the_player_after_loading()
    {
        var world = NewGame();
        TalkToTheKnight(world);

        var loaded = Load(SaveMapper.ToSave(world, SavedAt));
        var knight = Knight(loaded);

        Assert.Equal("The traveller's name is Loki.", Assert.Single(knight.Journal.Recent(10)).Text);
        Assert.Equal("Today, at Medows: Met Loki.", Assert.Single(knight.PastConversationsWith(loaded.player.Id, 5, loaded.Clock.TotalDays)));
    }

    [Fact]
    public void Events_memory_days_the_diary_and_the_last_reflected_day_come_back()
    {
        var world = NewGame();
        TalkToTheKnight(world);                       // day 1: a Talked event and a fact
        world.Clock.SkipDays(1);                      // day 2 starts: day 1 is queued...
        Step(world);                                  // ...asked for...
        Step(world);                                  // ...and written up
        Step(world, 1.0);                             // the knight walks on, on day 2
        var knight = Knight(world);
        Assert.Single(knight.Journal.Days);           // the reflection did land before saving

        var loaded = Load(SaveMapper.ToSave(world, SavedAt));
        var back = Knight(loaded);

        Assert.Equal(world.Events.All, loaded.Events.All);
        Assert.Equal(world.Events.PeekNextId, loaded.Events.PeekNextId);
        Assert.Equal(knight.Journal.Memories.Select(m => (m.Text, m.Kind, m.Day, m.SourceId)),
                     back.Journal.Memories.Select(m => (m.Text, m.Kind, m.Day, m.SourceId)));
        Assert.Equal(new[] { new DayEntry(1, "On Spring 1, Year 1 I met Loki.") }, back.Journal.Days);
        Assert.Equal(1, back.Journal.LastReflectedDay);
    }

    [Fact]
    public void Factions_opinions_standings_and_rumours_come_back()
    {
        var world = NewGame();
        var knight = Knight(world);
        world.player.Join("guard", world.Factions);
        world.player.ChangeStanding("bandit", -7);
        knight.ChangeOpinionOf(world.player, 25, "he joined us");
        knight.Opinions.ChangeFaction("village", -15);
        knight.Journal.Add(new Memory("Grim robbed the miller", MemoryKind.Rumour) { SourceId = 4, TellerId = 9, Hops = 2, Day = 1 });

        var loaded = Load(SaveMapper.ToSave(world, SavedAt));
        var back = Knight(loaded);

        Assert.Equal(world.player.Factions, loaded.player.Factions);
        Assert.Equal(world.player.Standing.OrderBy(s => s.Key), loaded.player.Standing.OrderBy(s => s.Key));
        Assert.Equal(knight.Factions, back.Factions);
        Assert.Equal(new PersonalOpinion(knight.OpinionOf(world.player), "he joined us"), back.Opinions.People[loaded.player.Id]);
        Assert.Equal(knight.Opinions.Factions.OrderBy(f => f.Key), back.Opinions.Factions.OrderBy(f => f.Key));
        var rumour = back.Journal.Memories.Single(m => m.Kind == MemoryKind.Rumour);
        Assert.Equal((4, (int?)9, 2), (rumour.SourceId!.Value, rumour.TellerId, rumour.Hops));
        Assert.True(back.Admits);   // comes with the type, not the save
    }

    [Fact]
    public void Events_an_NPC_has_reflected_on_are_still_known_after_loading()
    {
        var world = NewGame();
        var knight = Knight(world);
        var seen = new Memory("Grim robbed the miller", MemoryKind.Event) { SourceId = 4, Day = 1 };
        knight.Journal.Add(seen);
        knight.Journal.Forget(new[] { seen });   // reflected on: the memory's gone, the event still known

        var back = Knight(Load(SaveMapper.ToSave(world, SavedAt)));

        Assert.DoesNotContain(back.Journal.Memories, m => m.SourceId == 4);
        Assert.True(back.Journal.Knows(4));
    }

    [Fact]
    public void A_new_event_after_loading_does_not_reuse_a_saved_id()
    {
        var world = NewGame();
        TalkToTheKnight(world);
        var save = SaveMapper.ToSave(world, SavedAt);

        var loaded = Load(save);
        TalkToTheKnight(loaded);

        Assert.Equal(save.NextEventId, loaded.Events.All.Last().Id);
    }

    [Fact]
    public void A_level_whose_NPCs_were_saved_does_not_spawn_them_again()
    {
        var loaded = Load(SaveMapper.ToSave(NewGame(), SavedAt));

        loaded.Roster.Populate(loaded.location);   // what walking back into Medows does

        Knight(loaded);   // still just the one knight
    }

    [Fact]
    public void New_NPCs_and_conversations_after_loading_do_not_reuse_saved_ids()
    {
        var world = NewGame();
        TalkToTheKnight(world);   // transcript #1
        var save = SaveMapper.ToSave(world, SavedAt);

        var loaded = Load(save);
        TalkToTheKnight(loaded);

        Assert.Equal(new[] { 1, 2 }, Knight(loaded).Journal.Transcripts.Select(t => t.Id));
        Assert.Equal(save.NextPersonId, loaded.Roster.NextId());
        Assert.DoesNotContain(save.NextPersonId, save.Npcs.Select(n => n.Id).Append(loaded.player.Id));
    }

    [Fact]
    public void An_NPC_saved_in_another_level_is_put_back_there()
    {
        var save = SaveMapper.ToSave(NewGame(), SavedAt);
        save.Npcs[0].Level = "CastleInterior";   // as if he'd walked inside

        var loaded = Load(save);
        var knight = Knight(loaded);

        Assert.Equal("CastleInterior", loaded.Roster.LevelOf(knight));
        Assert.Equal("CastleInterior", knight.Location.Name);
        Assert.Empty(loaded.NPCs);   // nobody left in Medows
    }

    [Fact]
    public void A_knight_saved_mid_walk_carries_on_walking_to_the_same_place()
    {
        var world = NewGame();
        Step(world, 1.0);   // a second into his walk to the entry
        var save = SaveMapper.ToSave(world, SavedAt);
        var heading = Assert.IsType<GoToJob>(Knight(world).Jobs.First()).Target;

        var loaded = Load(save);
        var knight = Knight(loaded);
        Vector2 before = knight.Feet;
        Step(loaded, 0.5);

        Assert.Equal(NpcBrain.NPCState.Working, knight.State);
        Near.Equal(heading, Assert.IsType<GoToJob>(knight.Jobs.First()).Target);
        Assert.True(Vector2.Distance(knight.Feet, heading) < Vector2.Distance(before, heading), "he should be closer to where he was going");
    }

    [Fact]
    public void A_go_to_that_can_no_longer_be_reached_fails_cleanly_after_loading()
    {
        var save = SaveMapper.ToSave(NewGame(), SavedAt);
        save.Npcs[0].Tasks = new() { new TaskSave { Type = "go_to", X = -50, Y = -50 } };   // off the map

        var loaded = Load(save);
        Step(loaded);
        Step(loaded);

        Assert.Empty(Knight(loaded).Jobs);   // dropped as Failed, not stuck or crashed
    }

    [Fact]
    public void A_save_with_no_player_is_refused()
    {
        var save = SaveMapper.ToSave(NewGame(), SavedAt);
        save.Players.Clear();

        Assert.Throws<InvalidDataException>(() => Load(save));
    }

    // ------------------------------------------------------------------ the clock and calendar

    [Theory]
    [InlineData(1, "Spring 1, Year 1")]
    [InlineData(30, "Spring 30, Year 1")]
    [InlineData(31, "Summer 1, Year 1")]
    [InlineData(121, "Spring 1, Year 2")]
    public void A_restored_clock_shows_the_right_date_for_its_day(int totalDays, string shown)
    {
        var clock = new GameClock();

        clock.Restore(totalDays, 0f);

        Assert.Equal(totalDays, clock.TotalDays);
        Assert.Equal(shown, clock.DateDisplay);
    }

    [Fact]
    public void Restoring_the_clock_does_not_announce_a_new_day()
    {
        var clock = new GameClock();
        int started = 0;
        clock.DayStarted += _ => started++;

        clock.Restore(5, 100f);

        Assert.Equal(0, started);
    }
}
