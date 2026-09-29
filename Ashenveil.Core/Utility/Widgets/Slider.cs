using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// A draggable 0..1 value (volume, brightness). A progress bar with a handle: press on it and
    /// drag; the value follows the mouse until you let go, even if the mouse leaves the bar.
    /// </summary>
    public class Slider : Widget
    {
        private string _colour, _handle;
        private Action<float> _onChanged;
        private bool _dragging;
        public float Value { get; private set; }

        public Slider(Rectangle bounds, string colour, float value, Action<float> onChanged,
                      string handle = "scrollbar_brown_small")
            : base(bounds)
        {
            _colour = colour;
            _handle = handle;
            Value = MathHelper.Clamp(value, 0f, 1f);
            _onChanged = onChanged;
        }

        public override void Update(MouseState mouse, MouseState prev)
        {
            base.Update(mouse, prev);                          // sets Hovered

            // Pressed this frame (not held from before) while over the bar -> start dragging.
            bool pressed = mouse.LeftButton == ButtonState.Pressed;
            if (Hovered && pressed && prev.LeftButton == ButtonState.Released) _dragging = true;
            if (!pressed || !Visible) _dragging = false;

            if (_dragging)
            {
                float v = MathHelper.Clamp((mouse.X - Bounds.X) / (float)Bounds.Width, 0f, 1f);
                if (v != Value)
                {
                    Value = v;
                    _onChanged?.Invoke(Value);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible) return;
            UI.DrawProgressBar(spriteBatch, Bounds, _colour, Value);

            // Handle: a scrollbar nub a bit taller than the bar, centred on the value point.
            Rectangle src = AshenveilGame.UiAtlas[_handle];
            int h = Bounds.Height + 12;
            int w = h * src.Width / src.Height;
            int cx = Bounds.X + (int)(Bounds.Width * Value);
            var handle = new Rectangle(cx - w / 2, Bounds.Center.Y - h / 2, w, h);

            Color tint = Hovered || _dragging ? new Color(210, 210, 210) : Color.White;
            AshenveilGame.UiAtlas.Draw(spriteBatch, _handle, handle, tint);
        }
    }
}
