using Ashenveil.Core.DevConsole;

namespace Ashenveil.Tests;

/// <summary>
/// The console's command registry on its own: splitting a typed line into words, finding the
/// command, checking its argument count, and turning every kind of failure into Error lines
/// instead of an exception. No screen, no world.
/// </summary>
public class CommandRegistryTests
{
    private readonly CommandRegistry _commands = new();

    // What the last "echo" command was given, so a test can see the arguments arrive.
    private string[]? _echoed;

    public CommandRegistryTests()
    {
        _commands.Register("echo", "<word>...", "Says its arguments back.",
            args => { _echoed = args; return args; }, minArgs: 0, maxArgs: 5);
    }

    private static string[] Texts(IEnumerable<ConsoleLine> lines) => lines.Select(l => l.Text).ToArray();

    // ------------------------------------------------------------------ splitting

    [Theory]
    [InlineData("tp 3 4", new[] { "tp", "3", "4" })]
    [InlineData("  tp   3    4  ", new[] { "tp", "3", "4" })]                         // extra spaces
    [InlineData("journal \"Sir Lancelot\"", new[] { "journal", "Sir Lancelot" })]     // quotes keep the space
    [InlineData("say \"\" x", new[] { "say", "", "x" })]                              // "" is an empty word
    [InlineData("journal \"Sir Lan", new[] { "journal", "Sir Lan" })]                 // unclosed quote runs to the end
    [InlineData("a\tb", new[] { "a", "b" })]                                          // any whitespace splits
    public void Split_breaks_a_line_into_words(string input, string[] expected)
    {
        Assert.Equal(expected, CommandRegistry.Split(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Split_of_nothing_is_no_words(string? input)
    {
        Assert.Empty(CommandRegistry.Split(input!));
    }

    // ------------------------------------------------------------------ running

    [Fact]
    public void A_command_gets_its_arguments_and_its_output_comes_back_as_Output_lines()
    {
        var lines = _commands.Execute("echo hello \"big world\"");

        Assert.Equal(new[] { "hello", "big world" }, _echoed);
        Assert.Equal(new[] { "hello", "big world" }, Texts(lines));
        Assert.All(lines, l => Assert.Equal(LineKind.Output, l.Kind));
    }

    [Fact]
    public void Command_names_ignore_case()
    {
        _commands.Execute("ECHO hi");

        Assert.Equal(new[] { "hi" }, _echoed);
    }

    [Fact]
    public void Blank_input_does_nothing()
    {
        Assert.Empty(_commands.Execute("   "));
    }

    [Fact]
    public void An_unknown_command_is_an_error_that_points_at_help()
    {
        var line = Assert.Single(_commands.Execute("fly"));

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("fly", line.Text);
        Assert.Contains("help", line.Text);
    }

    [Theory]
    [InlineData("tp")]            // too few
    [InlineData("tp 1")]
    [InlineData("tp 1 2 3")]      // too many
    public void The_wrong_number_of_arguments_shows_the_usage_and_doesnt_run(string input)
    {
        bool ran = false;
        _commands.Register("tp", "<col> <row>", "Moves.", _ => { ran = true; return Array.Empty<string>(); },
                           minArgs: 2, maxArgs: 2);

        var line = Assert.Single(_commands.Execute(input));

        Assert.False(ran);
        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Equal("Usage: tp <col> <row>", line.Text);
    }

    [Fact]
    public void A_CommandException_becomes_an_error_line_with_its_message()
    {
        _commands.Register("fail", "", "Fails.", _ => throw new CommandException("No NPC called Bob."));

        var line = Assert.Single(_commands.Execute("fail"));

        Assert.Equal(new ConsoleLine("No NPC called Bob.", LineKind.Error), line);
    }

    [Fact]
    public void A_bug_in_a_command_is_caught_and_reported_not_thrown()
    {
        _commands.Register("crash", "", "Crashes.", _ => throw new InvalidOperationException("oops"));

        var line = Assert.Single(_commands.Execute("crash"));

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("crash", line.Text);
        Assert.Contains("oops", line.Text);
    }

    [Fact]
    public void A_bug_in_a_lazy_command_is_caught_too()
    {
        // `yield return` handlers only run when enumerated - after Run has returned.
        static IEnumerable<string> Lazy() { yield return "first"; throw new InvalidOperationException("late"); }
        _commands.Register("lazy", "", "Fails halfway.", _ => Lazy());

        var line = Assert.Single(_commands.Execute("lazy"));

        Assert.Equal(LineKind.Error, line.Kind);
        Assert.Contains("late", line.Text);
    }

    // ------------------------------------------------------------------ registering and help

    [Fact]
    public void Registering_a_name_twice_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _commands.Register("ECHO", "", "Again.", _ => Array.Empty<string>()));
    }

    [Fact]
    public void Help_lists_every_command_with_its_usage_and_help_in_name_order()
    {
        _commands.Register("tp", "<col> <row>", "Moves the player.", _ => Array.Empty<string>(), 2, 2);

        var lines = Texts(_commands.Execute("help"));

        Assert.Equal(3, lines.Length);   // echo, help, tp
        Assert.StartsWith("echo <word>...", lines[0]);
        Assert.StartsWith("help", lines[1]);
        Assert.StartsWith("tp <col> <row>", lines[2]);
        Assert.Contains("Moves the player.", lines[2]);
    }
}
