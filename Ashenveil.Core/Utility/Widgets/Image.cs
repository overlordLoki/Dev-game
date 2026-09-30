using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// A whole texture (a portrait, an item icon) shrunk to fit inside Bounds without
    /// stretching, centred in the leftover space. For atlas sprites use a Panel instead.
    /// </summary>
    public class Image : Widget
    {
        public Texture2D Texture { get; set; }

        public Image(Rectangle bounds, Texture2D texture)
            : base(bounds)
        {
            Texture = texture;
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible || Texture == null) return;

            // Scale by whichever side hits the box first, so the whole image shows.
            float scale = System.Math.Min(Bounds.Width / (float)Texture.Width, Bounds.Height / (float)Texture.Height);
            int w = (int)(Texture.Width * scale), h = (int)(Texture.Height * scale);
            var dest = new Rectangle(Bounds.X + (Bounds.Width - w) / 2, Bounds.Y + (Bounds.Height - h) / 2, w, h);

            spriteBatch.Draw(Texture, dest, Color.White);
        }
    }
}
