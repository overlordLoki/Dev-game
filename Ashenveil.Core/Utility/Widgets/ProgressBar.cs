using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// A horizontal fill bar (health, loading, XP). Set Value 0..1 from whatever it shows.
    /// colour = "blue", "green", "red" or "white". Ignores input.
    /// </summary>
    public class ProgressBar : Widget
    {
        private string _colour;
        private float _value;

        public float Value
        {
            get => _value;
            set => _value = MathHelper.Clamp(value, 0f, 1f);
        }

        public ProgressBar(Rectangle bounds, string colour, float value = 1f)
            : base(bounds)
        {
            _colour = colour;
            Value = value;
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible) return;
            UI.DrawProgressBar(spriteBatch, Bounds, _colour, _value);
        }
    }
}
