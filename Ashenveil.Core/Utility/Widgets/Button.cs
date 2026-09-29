using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Utility.Widgets
{
    public class Button : Widget
    {
        private Rectangle _bounds;
        private string _text, _sprite;
        private Action _onClick;
        private bool _hovered;

        public Button(Rectangle bounds, string text, string sprite, Action onClick)
        {
            _bounds = bounds;
            _text = text;
            _sprite = sprite;
            _onClick = onClick;
        }

        public override void Update(MouseState mouse, MouseState prev)
        {
            _hovered = _bounds.Contains(mouse.X, mouse.Y);
            // Fire on release over the button (a full click), not on press.
            if (_hovered && mouse.LeftButton == ButtonState.Released
                        && prev.LeftButton == ButtonState.Pressed)
                _onClick();
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font) =>
            UI.DrawButton(spriteBatch, _bounds, _text, _sprite, font, Color.Black, _hovered);
    }
}
