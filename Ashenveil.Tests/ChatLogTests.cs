using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// The ChatLog widget's bookkeeping (drawing needs a font, so it's checked by hand), and
/// Printable, which makes LLM text safe and readable for the game's font.
/// </summary>
public class ChatLogTests
{
    private static ChatLog NewLog(int max = int.MaxValue) => new(new Rectangle(0, 0, 400, 200), max);

    [Fact]
    public void Entries_are_kept_until_the_cap_then_the_oldest_go()
    {
        var log = NewLog(max: 3);

        for (int i = 0; i < 5; i++) log.Add($"line {i}", Color.White);

        Assert.Equal(3, log.Count);
    }

    [Fact]
    public void Clear_empties_it()
    {
        var log = NewLog();
        log.Add("a", Color.White);
        log.Add("b", Color.White, "Sir Lancelot", Color.Gold);

        log.Clear();

        Assert.Equal(0, log.Count);
    }

    [Theory]
    [InlineData("It’s ‘fine’", "It's 'fine'")]
    [InlineData("“Halt,” he said", "\"Halt,\" he said")]
    [InlineData("north – or south — then", "north - or south - then")]
    [InlineData("Wait…", "Wait...")]
    [InlineData("Plain text stays. 123!?", "Plain text stays. 123!?")]
    public void Printable_swaps_fancy_punctuation_for_plain(string input, string expected)
    {
        Assert.Equal(expected, ChatLog.Printable(input));
    }
}
