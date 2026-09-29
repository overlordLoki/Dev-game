using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Utility.Widgets
{
    public class Button : Widget
    {
        private string _text, _sprite;
        private Action _onClick;

        public Button(Rectangle bounds, string text, string sprite, Action onClick)
            : base(bounds)
        {
            _text = text;
            _sprite = sprite;
            _onClick = onClick;
        }

        public override void Update(MouseState mouse, MouseState prev)
        {
            base.Update(mouse, prev);                          // sets Hovered
            if (Hovered && Clicked(mouse, prev)) _onClick?.Invoke();
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible) return;
            UI.DrawButton(spriteBatch, Bounds, _text, _sprite, font, Color.Black, Hovered);
        }
    }
}
