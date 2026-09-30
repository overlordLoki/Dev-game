using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// Text in a box. One line: centred by default, or left-aligned (vertically centred either
    /// way). With wrap on it becomes a paragraph: word-wrapped to the box width, drawn from the
    /// top-left down. Text past the bottom of the box is still drawn — size the box to fit.
    /// </summary>
    public class Label : Widget
    {
        public string Text { get; set; }
        public Color Color { get; set; }
        private bool _centred, _wrap;

        public Label(Rectangle bounds, string text, Color color, bool centred = true, bool wrap = false)
            : base(bounds)
        {
            Text = text;
            Color = color;
            _centred = centred;
            _wrap = wrap;
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible || string.IsNullOrEmpty(Text)) return;

            if (_wrap)
            {
                float y = Bounds.Y;
                foreach (string line in UI.WrapText(font, Text, Bounds.Width))
                {
                    spriteBatch.DrawString(font, line, new Vector2(Bounds.X, (int)y), Color);
                    y += font.LineSpacing;
                }
                return;
            }

            // Cast to int so the text lands on whole pixels (fractions make it blurry).
            Vector2 size = font.MeasureString(Text);
            float x = _centred ? Bounds.X + (Bounds.Width - size.X) / 2 : Bounds.X;
            var pos = new Vector2((int)x, (int)(Bounds.Y + (Bounds.Height - size.Y) / 2));

            spriteBatch.DrawString(font, Text, pos, Color);
        }
    }
}
