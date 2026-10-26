using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Entities.brains.Dialogue;

namespace Ashenveil.Tests;

/// <summary>
/// `server` and `health` against fakes, like WorldCommandsTests: a string for the saved
/// address, and a health check that answers whatever the test says, straight away.
/// </summary>
public class BackendCommandsTests
{
    private readonly CommandRegistry _commands = new();
    private string _url = "http://127.0.0.1:8000";
    private readonly List<string> _checked = new();      // addresses health was asked about
    private readonly List<ConsoleLine> _later = new();   // lines written once a check finished
    private HealthResult _answer = new(HealthStatus.Ok, "m", "Connected. Model: m");
    private Exception? _saveFails;

    public BackendCommandsTests()
    {
        BackendCommands.Register(_commands, () => _url,
            url => { _url = url; if (_saveFails != null) throw _saveFails; },
            url => { _checked.Add(url); return Task.FromResult(_answer); },
            _later.Add);
    }

    private ConsoleLine RunOne(string input) => Assert.Single(_commands.Execute(input));

    [Fact]
    public void Server_on_its_own_shows_the_address()
    {
        Assert.Equal("Backend: http://127.0.0.1:8000", RunOne("server").Text);
    }

    [Fact]
    public void Server_with_an_address_tidies_and_saves_it()
    {
        var line = RunOne("server 192.168.1.20:9000/");

        Assert.Equal(LineKind.Output, line.Kind);
        Assert.Equal("http://192.168.1.20:9000", _url);
        Assert.Contains("saved", line.Text);
    }

    [Fact]
    public void Server_with_a_bad_address_is_an_error_and_changes_nothing()
    {
        var line = RunOne("server ftp://nope");

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Equal("http://127.0.0.1:8000", _url);
    }

    [Fact]
    public void Server_says_so_when_the_address_couldnt_be_saved()
    {
        _saveFails = new IOException("disk full");

        var line = RunOne("server http://x:1");

        Assert.Equal("http://x:1", _url);   // still in use this session
        Assert.Contains("couldn't be saved", line.Text);
        Assert.Contains("disk full", line.Text);
    }

    [Fact]
    public void Health_checks_the_saved_address_and_writes_the_answer_when_it_comes()
    {
        var now = RunOne("health");

        Assert.Equal("Checking http://127.0.0.1:8000...", now.Text);
        Assert.Equal(new[] { "http://127.0.0.1:8000" }, _checked);
        var answer = Assert.Single(_later);
        Assert.Equal(new ConsoleLine("Connected. Model: m", LineKind.Output), answer);
    }

    [Fact]
    public void Health_with_an_address_checks_that_one_and_saves_nothing()
    {
        RunOne("health other:1");

        Assert.Equal(new[] { "http://other:1" }, _checked);
        Assert.Equal("http://127.0.0.1:8000", _url);
    }

    [Fact]
    public void A_bad_health_answer_is_written_as_an_error()
    {
        _answer = new HealthResult(HealthStatus.Unreachable, null, "Can't reach it.");

        RunOne("health");

        Assert.Equal(LineKind.Error, Assert.Single(_later).Kind);
    }

    [Fact]
    public void A_health_check_that_throws_is_written_as_an_error_not_a_crash()
    {
        var commands = new CommandRegistry();
        var later = new List<ConsoleLine>();
        BackendCommands.Register(commands, () => "http://x:1", _ => { },
            _ => Task.FromException<HealthResult>(new InvalidOperationException("boom")), later.Add);

        commands.Execute("health");

        var line = Assert.Single(later);
        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("boom", line.Text);
    }
}
