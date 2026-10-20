using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.UI.Widgets
{
    /// <summary>
    /// A one-line box you can type into. Click it to focus, click elsewhere to unfocus; while
    /// focused, typed characters go on the end, Backspace takes one off and Enter submits.
    /// The field doesn't listen to the keyboard itself: the owning screen subscribes to
    /// window.TextInput once and passes each character to Type(), so a test can type into it
    /// without a window. Enter doesn't clear the text - the submit handler decides that.
    /// </summary>
    public class TextField : Widget
    {
        private const int Pad = 12;

        private string _sprite, _placeholder;
        private int _maxLength;
        private Action<string> _onSubmit;

        public string Text { get; set; } = "";
        public bool Focused { get; set; }

        public TextField(Rectangle bounds, string sprite, Action<string> onSubmit = null,
                         string placeholder = "", int maxLength = 200)
            : base(bounds)
        {
            _sprite = sprite;
            _onSubmit = onSubmit;
            _placeholder = placeholder;
            _maxLength = maxLength;
        }

        /// <summary>
        /// One typed character, as window.TextInput gives it: '\r' (Enter) submits, '\b'
        /// (Backspace) deletes, other control characters are ignored, anything else is added.
        /// Characters the font lacks are still accepted - the font draws them as its
        /// DefaultCharacter (set when it's loaded).
        /// </summary>
        public void Type(char c)
        {
            if (!Focused || !Visible) return;

            if (c == '\r') _onSubmit?.Invoke(Text);
            else if (c == '\b') { if (Text.Length > 0) Text = Text[..^1]; }
            else if (!char.IsControl(c) && Text.Length < _maxLength) Text += c;
        }

        public override void Update(MouseState mouse, MouseState prev)
        {
            base.Update(mouse, prev);                          // sets Hovered
            if (Clicked(mouse, prev)) Focused = Hovered;       // click in = focus, click out = unfocus
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible) return;

            Color tint = Hovered && !Focused ? new Color(210, 210, 210) : Color.White;
            UIDraw.DrawNineSlice(spriteBatch, AshenveilGame.UiAtlas, _sprite, Bounds, border: 6, scale: 2, tint);

            var pos = new Vector2(Bounds.X + Pad, (int)(Bounds.Y + (Bounds.Height - font.LineSpacing) / 2f));

            // Empty and not focused: show the hint, greyed out.
            if (Text.Length == 0 && !Focused)
            {
                spriteBatch.DrawString(font, _placeholder, pos, Color.DimGray);
                return;
            }

            // Cursor blinks twice a second, only while focused.
            string cursor = Focused && Environment.TickCount64 % 1000 < 500 ? "_" : "";

            // Text longer than the box shows its end, where the typing is happening.
            string shown = Text;
            int room = Bounds.Width - Pad * 2;
            while (shown.Length > 0 && font.MeasureString(shown + "_").X > room) shown = shown.Substring(1);

            spriteBatch.DrawString(font, shown + cursor, pos, Color.Black);
        }
    }
}
