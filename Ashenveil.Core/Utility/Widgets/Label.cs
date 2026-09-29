using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>Plain text in a box. Centred by default, or left-aligned (vertically centred either way).</summary>
    public class Label : Widget
    {
        public string Text { get; set; }
        public Color Color { get; set; }
        private bool _centred;

        public Label(Rectangle bounds, string text, Color color, bool centred = true)
            : base(bounds)
        {
            Text = text;
            Color = color;
            _centred = centred;
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible || string.IsNullOrEmpty(Text)) return;

            // Cast to int so the text lands on whole pixels (fractions make it blurry).
            Vector2 size = font.MeasureString(Text);
            float x = _centred ? Bounds.X + (Bounds.Width - size.X) / 2 : Bounds.X;
            var pos = new Vector2((int)x, (int)(Bounds.Y + (Bounds.Height - size.Y) / 2));

            spriteBatch.DrawString(font, Text, pos, Color);
        }
    }
}
