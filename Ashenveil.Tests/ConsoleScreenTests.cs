using Ashenveil.Core;
using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Screens;
using Ashenveil.Core.Utility.Input;

namespace Ashenveil.Tests;

/// <summary>
/// The console screen without a window: typing goes in through OnTextInput (as the
/// ScreenManager would pass it), Enter is '\r', and the output is read from Lines. Drawing
/// isn't tested - there's no font - so the screen is built with a null one.
/// </summary>
public class ConsoleScreenTests : IDisposable
{
    private readonly CommandRegistry _commands = new();
    private readonly ConsoleScreen _console;
    private bool _closed;

    public ConsoleScreenTests()
    {
        AshenveilGame.Input = new FakeInput();
        _commands.Register("ping", "", "Answers pong.", _ => new[] { "pong" });
        _console = new ConsoleScreen(null, _commands, () => _closed = true, 1280, 720);
        _console.Open(1280, 720);
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private void Type(string text)
    {
        foreach (char c in text) _console.OnTextInput(c);
    }

    private ConsoleLine[] AfterWelcome => _console.Lines.Skip(1).ToArray();

    [Fact]
    public void It_opens_with_a_welcome_that_mentions_help()
    {
        var welcome = Assert.Single(_console.Lines);

        Assert.Equal(LineKind.Event, welcome.Kind);
        Assert.Contains("help", welcome.Text);
    }

    [Fact]
    public void Typing_a_command_and_Enter_echoes_it_and_writes_its_output()
    {
        Type("ping\r");

        Assert.Equal(new[]
        {
            new ConsoleLine("> ping", LineKind.Input),
            new ConsoleLine("pong", LineKind.Output),
        }, AfterWelcome);
        Assert.Equal("", _console.Typed);   // ready for the next one
    }

    [Fact]
    public void The_toggle_keys_character_is_never_typed()
    {
        Type("`p~i¬ng");

        Assert.Equal("ping", _console.Typed);
    }

    [Fact]
    public void Errors_come_back_as_error_lines()
    {
        Type("nope\r");

        Assert.Equal(LineKind.Error, AfterWelcome[1].Kind);
    }

    [Fact]
    public void Enter_on_an_empty_line_writes_nothing()
    {
        Type("   \r");

        Assert.Empty(AfterWelcome);
    }

    [Fact]
    public void Clear_empties_the_output()
    {
        Type("ping\r");
        Type("clear\r");

        Assert.Empty(_console.Lines);
    }

    [Fact]
    public void Output_is_capped_oldest_first()
    {
        for (int i = 0; i < ConsoleScreen.MaxLines + 10; i++) _console.Write($"line {i}", LineKind.Output);

        Assert.Equal(ConsoleScreen.MaxLines, _console.Lines.Count);
        Assert.Equal($"line {ConsoleScreen.MaxLines + 9}", _console.Lines[^1].Text);
    }

    [Fact]
    public void The_output_survives_closing_and_opening_again()
    {
        Type("ping\r");

        _console.Open(800, 600);   // a different window size, too

        Assert.Equal("pong", _console.Lines[^1].Text);
    }

    // ------------------------------------------------------------------ history

    [Fact]
    public void Up_goes_back_through_earlier_commands_and_stops_at_the_oldest()
    {
        Type("ping\r");
        Type("help\r");

        _console.RecallOlder();
        Assert.Equal("help", _console.Typed);
        _console.RecallOlder();
        Assert.Equal("ping", _console.Typed);
        _console.RecallOlder();
        Assert.Equal("ping", _console.Typed);
    }

    [Fact]
    public void Down_comes_forward_again_to_an_empty_line()
    {
        Type("ping\r");
        Type("help\r");
        _console.RecallOlder();
        _console.RecallOlder();

        _console.RecallNewer();
        Assert.Equal("help", _console.Typed);
        _console.RecallNewer();
        Assert.Equal("", _console.Typed);
    }

    [Fact]
    public void The_same_command_twice_in_a_row_is_remembered_once()
    {
        Type("ping\r");
        Type("ping\r");

        _console.RecallOlder();
        _console.RecallOlder();

        Assert.Equal("ping", _console.Typed);   // nothing older than the one ping
    }

    [Fact]
    public void History_with_nothing_typed_yet_does_nothing()
    {
        _console.RecallOlder();
        _console.RecallNewer();

        Assert.Equal("", _console.Typed);
    }

    // ------------------------------------------------------------------ closing

    [Fact]
    public void Escape_closes_it()
    {
        var input = (FakeInput)AshenveilGame.Input;
        _console.Update(new Microsoft.Xna.Framework.GameTime());
        input.Hold(Microsoft.Xna.Framework.Input.Keys.Escape);

        _console.Update(new Microsoft.Xna.Framework.GameTime());

        Assert.True(_closed);
    }

    [Fact]
    public void Up_and_Down_keys_browse_the_history()
    {
        var input = (FakeInput)AshenveilGame.Input;
        Type("ping\r");

        input.Hold(Microsoft.Xna.Framework.Input.Keys.Up);
        _console.Update(new Microsoft.Xna.Framework.GameTime());

        Assert.Equal("ping", _console.Typed);
    }
}
