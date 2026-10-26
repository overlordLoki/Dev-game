using Ashenveil.Core;
using Ashenveil.Core.Config;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Screens;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// The settings screen's logic without a window, like ConsoleScreenTests: typing through
/// OnTextInput, the commit and health check faked. Layout and drawing are checked by hand.
/// </summary>
public class SettingsScreenTests : IDisposable
{
    private readonly FakeInput _input = new();
    private readonly GameSettings _settings = new() { backendUrl = "http://127.0.0.1:8000", devMode = false };
    private readonly SettingsScreen _screen;
    private int _commits, _backs;
    private Exception? _commitFails;
    private TaskCompletionSource<HealthResult> _health = new();
    private string? _checked;

    public SettingsScreenTests()
    {
        AshenveilGame.Input = _input;
        _screen = new SettingsScreen(null, _settings,
            () => { _commits++; if (_commitFails != null) throw _commitFails; },
            url => { _checked = url; return _health.Task; },
            showDevMode: true, () => _backs++);
        _screen.Open(1280, 720);
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    // Click the field (at its row: x 296..816, y 120..170) so it's focused, clear it, type.
    private void Retype(string text)
    {
        _input.MoveMouse(400, 140, leftDown: true);
        _screen.Update(new GameTime());
        _input.MoveMouse(400, 140);
        _screen.Update(new GameTime());
        foreach (char _ in _screen.TypedUrl) _screen.OnTextInput('\b');
        foreach (char c in text) _screen.OnTextInput(c);
    }

    [Fact]
    public void It_opens_showing_the_saved_address()
    {
        Assert.Equal("http://127.0.0.1:8000", _screen.TypedUrl);
    }

    [Fact]
    public void Enter_takes_a_new_address_tidied_and_saves_it()
    {
        Retype("10.0.0.9:8000/");
        _screen.OnTextInput('\r');

        Assert.Equal("http://10.0.0.9:8000", _settings.backendUrl);
        Assert.Equal("http://10.0.0.9:8000", _screen.TypedUrl);
        Assert.Equal(1, _commits);
    }

    [Fact]
    public void Back_takes_the_address_and_leaves()
    {
        Retype("http://10.0.0.9:8000");
        _screen.Back();

        Assert.Equal("http://10.0.0.9:8000", _settings.backendUrl);
        Assert.Equal(1, _backs);
    }

    [Fact]
    public void Back_with_nothing_changed_leaves_without_saving()
    {
        _screen.Back();

        Assert.Equal(0, _commits);
        Assert.Equal(1, _backs);
    }

    [Fact]
    public void Back_with_a_bad_address_stays_and_says_why()
    {
        Retype("ftp://nope");
        _screen.Back();

        Assert.Equal(0, _backs);
        Assert.Equal("http://127.0.0.1:8000", _settings.backendUrl);
        Assert.Contains("http://", _screen.Status);
    }

    [Fact]
    public void Escape_is_back()
    {
        _input.Hold(Keys.Escape);
        _screen.Update(new GameTime());

        Assert.Equal(1, _backs);
    }

    [Fact]
    public void A_failed_save_is_shown_and_a_second_back_leaves_anyway()
    {
        _commitFails = new IOException("read-only");
        Retype("http://10.0.0.9:8000");

        _screen.Back();
        Assert.Equal(0, _backs);
        Assert.Contains("read-only", _screen.Status);

        _screen.Back();
        Assert.Equal(1, _backs);
    }

    [Fact]
    public void Test_connection_checks_the_typed_address_without_taking_it_and_shows_the_answer()
    {
        Retype("10.0.0.9:8000");

        _screen.TestConnection();
        Assert.Equal("http://10.0.0.9:8000", _checked);
        Assert.StartsWith("Checking", _screen.Status);

        _health.SetResult(new HealthResult(HealthStatus.Ok, "m", "Connected. Model: m"));
        _screen.Update(new GameTime());   // picked up on the game's thread

        Assert.Equal("Connected. Model: m", _screen.Status);
        Assert.Equal("http://127.0.0.1:8000", _settings.backendUrl);
        Assert.Equal(0, _commits);
    }

    [Fact]
    public void Test_connection_on_a_bad_address_says_why_and_checks_nothing()
    {
        Retype("");

        _screen.TestConnection();

        Assert.Null(_checked);
        Assert.Contains(GameSettings.DefaultBackendUrl, _screen.Status);
    }

    [Fact]
    public void Dev_mode_is_applied_and_saved_as_soon_as_its_clicked()
    {
        _screen.SetDevMode(true);

        Assert.True(_settings.devMode);
        Assert.Equal(1, _commits);
    }
}
