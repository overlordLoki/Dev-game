using Ashenveil.Core;
using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Saving;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// SaveLibrary, what the menus call, against real files in a temp folder: listing every save
/// (newest first, damaged ones last), new file names, saving new or over, loading, deleting,
/// and the rules (no saving mid-conversation; a save clears unsaved play). Plus the world
/// commands following a World swapped in by a load.
/// </summary>
public class SaveLibraryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ashenveil-library-" + Guid.NewGuid().ToString("N"));
    private readonly SaveLibrary _library;

    public SaveLibraryTests()
    {
        NullContentManager.Install();
        AshenveilGame.Input = new FakeInput();   // no keys held: the player stands still
        _library = new SaveLibrary(_folder);
    }

    public void Dispose()
    {
        AshenveilGame.Input = new LiveInput();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Task<ChatMessage[]> NoReply(ConversationRequestDto _) => Task.FromResult(Array.Empty<ChatMessage>());

    private static World NewGame(string playerName = "Traveller")
    {
        var world = new World(new Location("Medows"), NoReply);
        world.Init();
        world.player.Name = playerName;
        return world;
    }

    private static void Step(World world) => world.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1 / 60.0)));

    // A save written straight through SaveStore, so the test picks its file name and saved time.
    private string SaveAs(string fileName, string playerName, DateTime savedAt)
    {
        string path = Path.Combine(_folder, fileName);
        var save = SaveMapper.ToSave(NewGame(playerName), savedAt);
        new SaveStore(path).Save(save);
        return path;
    }

    private string Junk(string fileName)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, fileName);
        File.WriteAllText(path, "not a save");
        return path;
    }

    private static DateTime Day(int day) => new(2026, 10, day, 12, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------------------ listing

    [Fact]
    public void A_missing_folder_means_no_saves()
    {
        Assert.Empty(_library.List());
        Assert.False(_library.HasSaves);
    }

    [Fact]
    public void Saves_are_listed_newest_first_with_damaged_ones_last()
    {
        SaveAs("save-1.db", "Oldest", Day(1));
        Junk("broken.db");
        SaveAs("save-2.db", "Newest", Day(3));
        SaveAs("slot1.db", "Middle", Day(2));   // the single-slot game's file is just another save

        var list = _library.List();

        Assert.Equal(new[] { "Newest", "Middle", "Oldest", null }, list.Select(s => s.PlayerName));
        Assert.False(list[3].IsReadable);
        Assert.Equal("broken.db", Path.GetFileName(list[3].FilePath));
        Assert.True(_library.HasSaves);
    }

    [Fact]
    public void Files_that_are_not_db_files_are_ignored()
    {
        SaveAs("save-1.db", "Loki", Day(1));
        Junk("notes.txt");
        Junk("save-1.db-journal");
        Junk("save-9.dbx");

        Assert.Single(_library.List());
    }

    // ------------------------------------------------------------------ new names

    [Fact]
    public void New_saves_are_numbered_save_1_then_save_2()
    {
        Assert.Equal(Path.Combine(_folder, "save-1.db"), _library.NextNewFilePath());

        _library.TrySaveNew(NewGame(), out _);

        Assert.Equal(Path.Combine(_folder, "save-2.db"), _library.NextNewFilePath());
    }

    [Fact]
    public void A_gap_left_by_a_deleted_save_is_not_refilled()
    {
        SaveAs("save-1.db", "A", Day(1));
        SaveAs("save-3.db", "C", Day(3));
        Junk("slot1.db");   // other names never count

        Assert.Equal(Path.Combine(_folder, "save-4.db"), _library.NextNewFilePath());
    }

    // ------------------------------------------------------------------ save

    [Fact]
    public void Saving_new_adds_a_save_and_says_so()
    {
        Assert.True(_library.TrySaveNew(NewGame("Loki"), out var message));

        Assert.Equal("Game saved.", message);
        Assert.Equal("Loki", Assert.Single(_library.List()).PlayerName);
    }

    [Fact]
    public void Saving_new_twice_keeps_both()
    {
        _library.TrySaveNew(NewGame("First"), out _);
        _library.TrySaveNew(NewGame("Second"), out _);

        Assert.Equal(2, _library.List().Count);
    }

    [Fact]
    public void Saving_over_a_save_changes_that_one_only()
    {
        SaveAs("save-1.db", "Keep", Day(1));
        string over = SaveAs("save-2.db", "Old", Day(2));
        var target = _library.List().Single(s => s.FilePath == over);

        Assert.True(_library.TrySaveOver(NewGame("New"), target, out var message), message);

        var names = _library.List().Select(s => s.PlayerName).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("Keep", names);
        Assert.Contains("New", names);
        Assert.Equal("New", new SaveStore(over).ReadSummary().PlayerName);
    }

    [Fact]
    public void Saving_over_a_damaged_save_replaces_it_with_a_good_one()
    {
        string path = Junk("save-1.db");
        var damaged = Assert.Single(_library.List());

        Assert.True(_library.TrySaveOver(NewGame("Loki"), damaged, out var message), message);

        Assert.Equal("Loki", new SaveStore(path).ReadSummary().PlayerName);
    }

    [Fact]
    public void Saving_in_the_middle_of_a_conversation_is_refused_new_or_over_and_writes_nothing()
    {
        string path = SaveAs("save-1.db", "Old", Day(1));
        var world = NewGame("New");
        world.StartConversation(world.player, world.NPCs.ToArray());

        Assert.False(_library.CanSave(world, out var why));
        Assert.Contains("conversation", why);
        Assert.False(_library.TrySaveNew(world, out _));
        Assert.False(_library.TrySaveOver(world, _library.List()[0], out _));

        Assert.Equal("Old", Assert.Single(_library.List()).PlayerName);
        Assert.Equal(path, _library.List()[0].FilePath);
    }

    [Fact]
    public void A_new_game_has_no_unsaved_play_until_it_runs()
    {
        var world = NewGame();
        Assert.False(world.HasUnsavedPlay);

        Step(world);

        Assert.True(world.HasUnsavedPlay);
    }

    [Fact]
    public void Saving_clears_unsaved_play_and_playing_on_brings_it_back()
    {
        var world = NewGame();
        Step(world);

        _library.TrySaveNew(world, out _);
        Assert.False(world.HasUnsavedPlay);

        Step(world);
        Assert.True(world.HasUnsavedPlay);
    }

    [Fact]
    public void A_save_that_fails_still_counts_as_unsaved_play()
    {
        var world = NewGame();
        Step(world);
        world.StartConversation(world.player, world.NPCs.ToArray());

        _library.TrySaveNew(world, out _);

        Assert.True(world.HasUnsavedPlay);
    }

    // ------------------------------------------------------------------ load

    [Fact]
    public void Loading_a_save_carries_on_that_world_with_nothing_unsaved_yet()
    {
        SaveAs("save-1.db", "Other", Day(1));
        string path = SaveAs("save-2.db", "Loki", Day(2));
        var chosen = _library.List().Single(s => s.FilePath == path);

        Assert.True(_library.TryLoad(chosen, out var loaded, out var problem, NoReply), problem);

        Assert.Equal("Loki", loaded.player.Name);
        Assert.IsType<Knight>(Assert.Single(loaded.NPCs));
        Assert.False(loaded.HasUnsavedPlay);
    }

    [Fact]
    public void Loading_a_damaged_save_is_a_clear_problem()
    {
        Junk("save-1.db");

        Assert.False(_library.TryLoad(_library.List()[0], out var world, out var problem));

        Assert.Null(world);
        Assert.Contains("couldn't be read", problem);
    }

    [Fact]
    public void Loading_a_save_naming_a_level_that_is_gone_is_a_clear_problem_not_a_crash()
    {
        string path = Path.Combine(_folder, "save-1.db");
        var save = SaveMapper.ToSave(NewGame(), DateTime.UtcNow);
        save.Players[0].Level = "Atlantis";   // no such level file
        new SaveStore(path).Save(save);

        Assert.False(_library.TryLoad(_library.List()[0], out var world, out var problem));

        Assert.Null(world);
        Assert.Contains("couldn't be loaded", problem);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public void Deleting_a_save_removes_its_file_and_its_row()
    {
        SaveAs("save-1.db", "Keep", Day(1));
        string gone = SaveAs("save-2.db", "Gone", Day(2));

        Assert.True(_library.TryDelete(_library.List().Single(s => s.FilePath == gone), out var problem), problem);

        Assert.False(File.Exists(gone));
        Assert.Equal("Keep", Assert.Single(_library.List()).PlayerName);
    }

    [Fact]
    public void A_damaged_save_can_be_deleted()
    {
        string path = Junk("save-1.db");

        Assert.True(_library.TryDelete(_library.List()[0], out _));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Deleting_a_save_that_is_already_gone_is_a_message_not_a_crash()
    {
        string path = SaveAs("save-1.db", "Loki", Day(1));
        var summary = _library.List()[0];
        File.Delete(path);

        Assert.False(_library.TryDelete(summary, out var problem));

        Assert.Contains("isn't there", problem);
    }

    // ------------------------------------------------------------------ the console

    [Fact]
    public void The_world_commands_follow_a_world_swapped_in_by_a_load()
    {
        var first = NewGame();
        _library.TrySaveNew(first, out _);
        _library.TryLoad(_library.List()[0], out var loaded, out _, NoReply);
        Vector2 firstFeet = first.player.Feet;
        var current = first;
        var commands = new CommandRegistry();
        WorldCommands.Register(commands, () => current);

        current = loaded;
        commands.Execute("tpxy 1.5 1.9");

        Near.Equal(new Vector2(1.5f, 1.9f), loaded.player.Feet);   // the loaded world's player moved...
        Near.Equal(firstFeet, first.player.Feet);                   // ...not the old one's
    }
}
