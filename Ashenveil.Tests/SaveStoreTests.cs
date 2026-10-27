using System.Text.Json;
using Ashenveil.Core.Saving;
using Microsoft.Data.Sqlite;

namespace Ashenveil.Tests;

/// <summary>
/// SaveStore against a real slot1.db in a throwaway temp folder: what comes back after a save,
/// that a save is one all-or-nothing snapshot, and what a missing, broken or too-new file
/// loads as. TryLoad must never throw.
/// </summary>
public class SaveStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ashenveil-saves-" + Guid.NewGuid().ToString("N"));
    private readonly SaveStore _store;

    public SaveStoreTests() => _store = new SaveStore(Path.Combine(_folder, "save-1.db"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    // A save with something in every table, including the values that can be null.
    private static SaveData FullSave() => new()
    {
        SavedAt = new DateTime(2026, 10, 27, 9, 30, 15, DateTimeKind.Utc),
        TotalDays = 12,
        SecondsIntoDay = 123.25f,
        NextPersonId = 3,
        NextTranscriptId = 3,
        Players = { new PlayerSave { Id = 0, Name = "Loki", Level = "Medows", X = 2.5f, Y = 2.9f } },
        PopulatedLevels = { "Castle", "Medows" },
        Transcripts =
        {
            new TranscriptSave
            {
                Id = 1, Day = 10, Location = "Medows", Summary = "Met Loki.",
                Participants = { new ParticipantSave { Id = 0, Name = "Traveller" }, new ParticipantSave { Id = 1, Name = "Sir O'Brien" } },
                Lines = { "Traveller: my name is Loki", "Sir O'Brien: Well met, Loki." },
            },
            new TranscriptSave
            {
                Id = 2, Day = 12, Location = null, Summary = null,   // nowhere in particular; decision never came back
                Participants = { new ParticipantSave { Id = 0, Name = "Loki" }, new ParticipantSave { Id = 1, Name = "Sir O'Brien" } },
                Lines = { "Loki: hello again" },
            },
        },
        Npcs =
        {
            new NpcSave
            {
                Id = 1, Type = "Knight", Name = "Sir O'Brien", Level = "Medows", X = 1.5f, Y = 3.9f,
                Memories =
                {
                    new MemorySave { Text = "The traveller's name is Loki.", Kind = "Conversation", TranscriptId = 1 },
                    new MemorySave { Text = "Saw wolves by the well.", Kind = "Event", TranscriptId = null },
                },
                TranscriptIds = { 1, 2 },
                Tasks =
                {
                    new TaskSave { Type = "go_to", X = 3.5f, Y = 1.9f },
                    new TaskSave { Type = "wait", Seconds = 2.75f },
                    new TaskSave { Type = "go_to", X = 1.5f, Y = 3.9f },
                },
            },
            new NpcSave { Id = 2, Type = "Knight", Name = "Sir Kay", Level = "Castle", X = 4.5f, Y = 4.9f, TranscriptIds = { 1 } },
        },
    };

    // Field for field: the save classes are plain values, so their JSON is a full picture of them.
    private static void AssertSame(SaveData expected, SaveData actual) =>
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    private SaveData Load()
    {
        Assert.True(_store.TryLoad(out var save, out var problem), problem);
        return save;
    }

    private string LoadProblem()
    {
        Assert.False(_store.TryLoad(out var save, out var problem));
        Assert.Null(save);
        Assert.False(string.IsNullOrWhiteSpace(problem));
        return problem;
    }

    // Opens the file directly, as DB Browser would, to poke at it behind the store's back.
    private void Sql(string sql)
    {
        Directory.CreateDirectory(_folder);
        using var db = new SqliteConnection($"Data Source={_store.FilePath};Pooling=False");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var db = new SqliteConnection($"Data Source={_store.FilePath};Pooling=False");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return (long)cmd.ExecuteScalar()!;
    }

    // ------------------------------------------------------------------ round trip

    [Fact]
    public void What_is_saved_loads_back_field_for_field()
    {
        var saved = FullSave();

        _store.Save(saved);

        AssertSame(saved, Load());
    }

    [Fact]
    public void Saving_creates_the_folder_and_the_file_and_stamps_it_with_the_schema_version()
    {
        Assert.False(Directory.Exists(_folder));

        _store.Save(FullSave());

        Assert.True(_store.HasSave);
        Assert.True(File.Exists(Path.Combine(_folder, "save-1.db")));
        Assert.Equal(SaveStore.SchemaVersion, Scalar("PRAGMA user_version"));
        Assert.EndsWith(Path.Combine("Ashenveil", "saves"), SaveStore.DefaultFolder);
    }

    // ------------------------------------------------------------------ summary

    [Fact]
    public void A_summary_says_who_where_which_day_and_when_without_loading_the_save()
    {
        _store.Save(FullSave());

        var summary = _store.ReadSummary();

        Assert.True(summary.IsReadable, summary.Problem);
        Assert.Equal(_store.FilePath, summary.FilePath);
        Assert.Equal("Loki", summary.PlayerName);
        Assert.Equal("Medows", summary.Level);
        Assert.Equal(12, summary.TotalDays);
        Assert.Equal(new DateTime(2026, 10, 27, 9, 30, 15, DateTimeKind.Utc), summary.SavedAt);
        Assert.Equal(DateTimeKind.Utc, summary.SavedAt.Kind);
    }

    [Fact]
    public void A_summary_is_of_the_owner_the_lowest_player_id()
    {
        var save = FullSave();
        save.Players.Insert(0, new PlayerSave { Id = 7, Name = "Guest", Level = "Castle", X = 1, Y = 1 });
        _store.Save(save);

        Assert.Equal("Loki", _store.ReadSummary().PlayerName);
    }

    [Fact]
    public void A_file_that_is_not_a_database_gives_a_summary_with_a_problem_not_a_crash()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(_store.FilePath, "this is not a database, it's a shopping list");

        var summary = _store.ReadSummary();

        Assert.False(summary.IsReadable);
        Assert.Equal(_store.FilePath, summary.FilePath);   // still known, so it can be deleted
        Assert.Contains("couldn't be read", summary.Problem);
    }

    [Fact]
    public void A_summary_of_a_save_from_a_newer_game_says_so()
    {
        _store.Save(FullSave());
        Sql($"PRAGMA user_version = {SaveStore.SchemaVersion + 1};");

        Assert.Contains("newer version", _store.ReadSummary().Problem);
    }

    [Fact]
    public void A_summary_of_an_empty_save_or_one_with_no_player_says_so()
    {
        _store.Save(FullSave());
        Sql("DELETE FROM players;");
        Assert.Contains("no player", _store.ReadSummary().Problem);

        Sql("DELETE FROM game;");
        Assert.Contains("empty", _store.ReadSummary().Problem);
    }

    [Fact]
    public void A_summary_of_a_file_that_is_gone_says_so()
    {
        Assert.False(_store.ReadSummary().IsReadable);
        Assert.False(File.Exists(_store.FilePath));   // reading it didn't create one
    }

    [Fact]
    public void Saving_again_fully_replaces_the_first_save_with_no_rows_left_over()
    {
        _store.Save(FullSave());
        var second = new SaveData
        {
            SavedAt = new DateTime(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc),
            TotalDays = 13,
            NextPersonId = 1,
            NextTranscriptId = 1,
            Players = { new PlayerSave { Id = 0, Name = "Traveller", Level = "Castle", X = 1, Y = 1 } },
        };

        _store.Save(second);

        AssertSame(second, Load());
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM tasks"));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM transcript_lines"));
    }

    [Fact]
    public void A_save_that_fails_part_way_leaves_the_previous_save_whole()
    {
        var first = FullSave();
        _store.Save(first);
        var broken = FullSave();
        broken.Npcs[0].TranscriptIds.Add(99);   // points at a transcript that isn't saved: the database refuses it

        Assert.Throws<SqliteException>(() => _store.Save(broken));

        AssertSame(first, Load());
    }

    // ------------------------------------------------------------------ nothing to load

    [Fact]
    public void No_save_file_means_there_is_nothing_to_continue()
    {
        Assert.False(_store.HasSave);
        LoadProblem();
        Assert.False(File.Exists(_store.FilePath));   // looking didn't create one
    }

    [Fact]
    public void A_file_that_is_not_a_database_is_a_clear_problem_not_a_crash()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(_store.FilePath, "this is not a database, it's a shopping list");

        Assert.Contains("couldn't be read", LoadProblem());
    }

    [Fact]
    public void A_database_that_is_not_an_Ashenveil_save_is_refused()
    {
        Sql("CREATE TABLE recipes (name TEXT);");   // a real SQLite file, but user_version is 0

        Assert.Contains("isn't an Ashenveil save", LoadProblem());
    }

    [Fact]
    public void A_save_missing_its_tables_is_a_clear_problem_not_a_crash()
    {
        _store.Save(FullSave());
        Sql("DROP TABLE tasks;");

        Assert.Contains("couldn't be read", LoadProblem());
    }

    [Fact]
    public void A_save_with_its_tables_but_nothing_in_them_is_empty()
    {
        _store.Save(FullSave());
        Sql("DELETE FROM game;");

        Assert.Contains("empty", LoadProblem());
    }

    // ------------------------------------------------------------------ versions

    [Fact]
    public void A_save_from_a_newer_game_is_refused_and_never_overwritten()
    {
        _store.Save(FullSave());
        Sql($"PRAGMA user_version = {SaveStore.SchemaVersion + 1};");

        Assert.Contains("newer version", LoadProblem());
        Assert.Throws<InvalidDataException>(() => _store.Save(FullSave()));
        Assert.Equal(SaveStore.SchemaVersion + 1, Scalar("PRAGMA user_version"));   // left as it was
    }
}
