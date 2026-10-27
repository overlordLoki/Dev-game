using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Saving;

namespace Ashenveil.Tests;

/// <summary>
/// The console's `saves`, `save [n]` and `load &lt;n&gt;`, with fakes for the saving and loading:
/// the numbering, which save a number picks, and what's said when it goes wrong. The real
/// saving and loading are SaveLibraryTests' job.
/// </summary>
public class SaveCommandsTests
{
    private readonly CommandRegistry _commands = new();
    private List<SaveSummary> _saves = new();
    private int _savedNew;
    private SaveSummary? _savedOver, _loaded;
    private (bool ok, string message) _answer = (true, "Done.");

    public SaveCommandsTests() =>
        SaveCommands.Register(_commands,
            () => _saves,
            () => { _savedNew++; return _answer; },
            save => { _savedOver = save; return _answer; },
            save => { _loaded = save; return _answer; });

    private static SaveSummary Save(string player) => new()
    {
        FilePath = $"{player}.db",
        PlayerName = player,
        Level = "Medows",
        TotalDays = 1,
        SavedAt = new DateTime(2026, 10, 27, 9, 30, 0, DateTimeKind.Utc),
    };

    private List<ConsoleLine> Run(string input) => _commands.Execute(input).ToList();

    [Fact]
    public void Saves_are_listed_numbered_from_1_in_the_order_given()
    {
        _saves = new() { Save("Newest"), Save("Older") };

        var lines = Run("saves").Select(l => l.Text).ToList();

        Assert.Equal(2, lines.Count);
        // Just "Saved ": the date after it is in this machine's time zone.
        Assert.StartsWith("1. Newest - Medows, Spring 1, Year 1 (Saved ", lines[0]);
        Assert.StartsWith("2. Older - ", lines[1]);
    }

    [Fact]
    public void No_saves_says_how_to_make_one()
    {
        Assert.Contains("'save' makes one", Assert.Single(Run("saves")).Text);
    }

    [Fact]
    public void Save_with_no_number_makes_a_new_save()
    {
        var line = Assert.Single(Run("save"));

        Assert.Equal(1, _savedNew);
        Assert.Null(_savedOver);
        Assert.Equal("Done.", line.Text);
        Assert.Equal(LineKind.Output, line.Kind);
    }

    [Fact]
    public void Save_n_saves_over_the_nth_save()
    {
        _saves = new() { Save("A"), Save("B") };

        Run("save 2");

        Assert.Same(_saves[1], _savedOver);
        Assert.Equal(0, _savedNew);
    }

    [Fact]
    public void Load_n_loads_the_nth_save()
    {
        _saves = new() { Save("A"), Save("B") };

        Run("load 1");

        Assert.Same(_saves[0], _loaded);
    }

    [Theory]
    [InlineData("load 3", "There's no save 3")]
    [InlineData("load 0", "There's no save 0")]
    [InlineData("load two", "must be a save's number")]
    [InlineData("save 9", "There's no save 9")]
    public void A_number_that_is_not_a_save_is_an_error_and_does_nothing(string input, string says)
    {
        _saves = new() { Save("A"), Save("B") };

        var line = Assert.Single(Run(input));

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains(says, line.Text);
        Assert.Null(_loaded);
        Assert.Null(_savedOver);
    }

    [Fact]
    public void Load_needs_a_number()
    {
        Assert.Equal(LineKind.Error, Assert.Single(Run("load")).Kind);
    }

    [Fact]
    public void A_save_or_load_that_fails_is_shown_as_an_error()
    {
        _answer = (false, "You can't save in the middle of a conversation.");

        var line = Assert.Single(Run("save"));

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Equal("You can't save in the middle of a conversation.", line.Text);
    }
}
