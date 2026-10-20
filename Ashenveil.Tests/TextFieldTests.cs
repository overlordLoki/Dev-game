using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// TextField takes typed characters through Type() - the screen passes on what
/// window.TextInput gives it - so it's tested without a window or a Draw. '\r' is Enter,
/// '\b' is Backspace, as MonoGame sends them. Focus comes from clicks in Update.
/// </summary>
public class TextFieldTests
{
    private static readonly Rectangle Box = new(100, 100, 200, 40);

    private string? _submitted;   // null until something is submitted

    private TextField NewField(int maxLength = 200, bool focused = true) =>
        new(Box, "button_brown", text => _submitted = text, maxLength: maxLength) { Focused = focused };

    private static void TypeAll(TextField field, string chars)
    {
        foreach (char c in chars) field.Type(c);
    }

    // A full left-click at (x, y): held last frame, released this one.
    private static void ClickAt(TextField field, int x, int y)
    {
        var prev = new MouseState(x, y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        var now = new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        field.Update(now, prev);
    }

    // ------------------------------------------------------------------ typing

    [Fact]
    public void Typed_characters_go_on_the_end()
    {
        var field = NewField();

        TypeAll(field, "Hi there");

        Assert.Equal("Hi there", field.Text);
    }

    [Fact]
    public void Backspace_takes_the_last_character_off()
    {
        var field = NewField();
        TypeAll(field, "Hey");

        field.Type('\b');

        Assert.Equal("He", field.Text);
    }

    [Fact]
    public void Backspace_on_an_empty_field_does_nothing()
    {
        var field = NewField();

        field.Type('\b');

        Assert.Equal("", field.Text);
    }

    [Fact]
    public void Other_control_characters_are_ignored()
    {
        var field = NewField();

        TypeAll(field, "a\tb\u001bc");   // tab and Escape

        Assert.Equal("abc", field.Text);
    }

    [Fact]
    public void Typing_stops_at_the_max_length()
    {
        var field = NewField(maxLength: 3);

        TypeAll(field, "abcdef");

        Assert.Equal("abc", field.Text);
    }

    [Fact]
    public void Backspace_still_works_at_the_max_length()
    {
        var field = NewField(maxLength: 3);
        TypeAll(field, "abc");

        field.Type('\b');
        field.Type('x');

        Assert.Equal("abx", field.Text);
    }

    [Fact]
    public void Characters_the_font_lacks_are_still_accepted()
    {
        // The font draws them as its DefaultCharacter; the field doesn't filter.
        var field = NewField();

        TypeAll(field, "é€");

        Assert.Equal("é€", field.Text);
    }

    // ------------------------------------------------------------------ submitting

    [Fact]
    public void Enter_submits_the_text()
    {
        var field = NewField();
        TypeAll(field, "hello");

        field.Type('\r');

        Assert.Equal("hello", _submitted);
    }

    [Fact]
    public void Enter_leaves_the_text_for_the_owner_to_clear()
    {
        var field = NewField();
        TypeAll(field, "hello");

        field.Type('\r');

        Assert.Equal("hello", field.Text);
    }

    [Fact]
    public void Enter_is_not_typed_into_the_text()
    {
        var field = NewField();

        TypeAll(field, "a\rb");

        Assert.Equal("ab", field.Text);
    }

    [Fact]
    public void Enter_with_no_handler_does_nothing()
    {
        var field = new TextField(Box, "button_brown") { Focused = true };
        TypeAll(field, "hi");

        field.Type('\r');   // must not throw

        Assert.Equal("hi", field.Text);
    }

    // ------------------------------------------------------------------ when it ignores typing

    [Fact]
    public void An_unfocused_field_ignores_typing_and_Enter()
    {
        var field = NewField(focused: false);

        TypeAll(field, "abc\r");

        Assert.Equal("", field.Text);
        Assert.Null(_submitted);
    }

    [Fact]
    public void A_hidden_field_ignores_typing()
    {
        var field = NewField();
        field.Visible = false;

        TypeAll(field, "abc");

        Assert.Equal("", field.Text);
    }

    // ------------------------------------------------------------------ focus by clicking

    [Fact]
    public void Clicking_inside_focuses_it()
    {
        var field = NewField(focused: false);

        ClickAt(field, 150, 120);

        Assert.True(field.Focused);
    }

    [Fact]
    public void Clicking_outside_unfocuses_it()
    {
        var field = NewField();

        ClickAt(field, 10, 10);

        Assert.False(field.Focused);
    }

    [Fact]
    public void Moving_the_mouse_without_clicking_keeps_the_focus()
    {
        var field = NewField();
        var away = new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

        field.Update(away, away);

        Assert.True(field.Focused);
    }
}
