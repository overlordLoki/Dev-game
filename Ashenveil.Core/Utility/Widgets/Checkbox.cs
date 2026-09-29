using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// A tick box with its text to the right. The box is a square the height of Bounds; clicking
    /// anywhere in Bounds (box or text) toggles it. colour = "beige", "brown" or "grey".
    /// </summary>
    public class Checkbox : Widget
    {
        private string _text, _colour;
        private Action<bool> _onChanged;
        private bool _useCross;
        public bool Checked { get; set; }

        public Checkbox(Rectangle bounds, string text, string colour, bool isChecked,
                        Action<bool> onChanged, bool useCross = false)
            : base(bounds)
        {
            _text = text;
            _colour = colour;
            Checked = isChecked;
            _onChanged = onChanged;
            _useCross = useCross;   // draw an X instead of a tick when checked
        }

        public override void Update(MouseState mouse, MouseState prev)
        {
            base.Update(mouse, prev);                          // sets Hovered
            if (Hovered && Clicked(mouse, prev))
            {
                Checked = !Checked;
                _onChanged?.Invoke(Checked);
            }
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible) return;

            string state = !Checked ? "empty" : _useCross ? "cross" : "checked";
            Color tint = Hovered ? new Color(210, 210, 210) : Color.White;
            var box = new Rectangle(Bounds.X, Bounds.Y, Bounds.Height, Bounds.Height);
            AshenveilGame.UiAtlas.Draw(spriteBatch, $"checkbox_{_colour}_{state}", box, tint);

            Vector2 size = font.MeasureString(_text);
            var pos = new Vector2(box.Right + 10, (int)(Bounds.Y + (Bounds.Height - size.Y) / 2));
            spriteBatch.DrawString(font, _text, pos, Color.White);
        }
    }
}
