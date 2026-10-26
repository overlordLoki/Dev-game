using Ashenveil.Core.Config;

namespace Ashenveil.Tests;

/// <summary>
/// settings.json in a throwaway temp folder: what comes back after a save, and what a
/// missing, broken, old or hand-edited file loads as. Load must never throw.
/// </summary>
public class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ashenveil-settings-" + Guid.NewGuid().ToString("N"));
    private readonly SettingsStore _store;

    public SettingsStoreTests() => _store = new SettingsStore(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private void WriteFile(string json)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(_store.FilePath, json);
    }

    [Fact]
    public void What_is_saved_loads_back_the_same()
    {
        var saved = new GameSettings { backendUrl = "http://192.168.1.20:9000", devMode = false };

        _store.Save(saved);
        var loaded = _store.Load();

        Assert.Equal("http://192.168.1.20:9000", loaded.backendUrl);
        Assert.False(loaded.devMode);
        Assert.Equal(GameSettings.CurrentVersion, loaded.version);
    }

    [Fact]
    public void Saving_creates_the_folder_and_leaves_no_temp_file()
    {
        _store.Save(new GameSettings());

        Assert.True(File.Exists(_store.FilePath));
        Assert.False(File.Exists(_store.FilePath + ".tmp"));
    }

    [Fact]
    public void Saving_again_replaces_the_old_file()
    {
        _store.Save(new GameSettings { backendUrl = "http://one:1" });
        _store.Save(new GameSettings { backendUrl = "http://two:2" });

        Assert.Equal("http://two:2", _store.Load().backendUrl);
    }

    [Fact]
    public void The_file_has_a_version()
    {
        _store.Save(new GameSettings());

        Assert.Contains($"\"version\": {GameSettings.CurrentVersion}", File.ReadAllText(_store.FilePath));
    }

    [Fact]
    public void No_file_gives_the_defaults()
    {
        var s = _store.Load();

        Assert.Equal(GameSettings.DefaultBackendUrl, s.backendUrl);
        Assert.Equal(new GameSettings().devMode, s.devMode);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"backendUrl\": 42 }")]   // the wrong type for a key
    public void A_broken_file_gives_the_defaults(string json)
    {
        WriteFile(json);

        Assert.Equal(GameSettings.DefaultBackendUrl, _store.Load().backendUrl);
    }

    [Fact]
    public void Unknown_keys_are_ignored_and_missing_ones_keep_their_defaults()
    {
        WriteFile("""{ "version": 1, "backendUrl": "http://x:1", "volume": 0.5, "somethingElse": { "a": 1 } }""");

        var s = _store.Load();

        Assert.Equal("http://x:1", s.backendUrl);
        Assert.Equal(new GameSettings().devMode, s.devMode);
    }

    [Fact]
    public void A_hand_edited_file_with_comments_trailing_commas_and_other_case_still_loads()
    {
        WriteFile("""
            {
              // where the Python backend is
              "BackendUrl": "http://x:1",
              "DEVMODE": true,
            }
            """);

        var s = _store.Load();

        Assert.Equal("http://x:1", s.backendUrl);
        Assert.True(s.devMode);
    }

    [Theory]
    [InlineData("""{ "backendUrl": "http://x:1" }""")]                 // from before versions
    [InlineData("""{ "version": 0, "backendUrl": "http://x:1" }""")]
    [InlineData("""{ "version": 99, "backendUrl": "http://x:1" }""")]  // from a newer game
    public void Another_version_loads_what_it_can_and_becomes_this_version(string json)
    {
        WriteFile(json);

        var s = _store.Load();

        Assert.Equal("http://x:1", s.backendUrl);
        Assert.Equal(GameSettings.CurrentVersion, s.version);
    }

    [Theory]
    [InlineData("""{ "backendUrl": "" }""")]
    [InlineData("""{ "backendUrl": null }""")]
    [InlineData("""{ "backendUrl": "ftp://x" }""")]
    public void A_stored_address_that_isnt_one_gives_the_default(string json)
    {
        WriteFile(json);

        Assert.Equal(GameSettings.DefaultBackendUrl, _store.Load().backendUrl);
    }

    [Fact]
    public void A_stored_address_is_tidied_on_load()
    {
        WriteFile("""{ "backendUrl": "localhost:8000/" }""");

        Assert.Equal("http://localhost:8000", _store.Load().backendUrl);
    }

    [Fact]
    public void A_leftover_temp_file_from_a_crash_doesnt_matter()
    {
        _store.Save(new GameSettings { backendUrl = "http://good:1" });
        File.WriteAllText(_store.FilePath + ".tmp", "{ half writ");   // a crash mid-write

        Assert.Equal("http://good:1", _store.Load().backendUrl);
        _store.Save(new GameSettings { backendUrl = "http://next:2" });
        Assert.Equal("http://next:2", _store.Load().backendUrl);
    }
}
