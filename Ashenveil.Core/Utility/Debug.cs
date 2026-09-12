
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Utility
{
    public static class Debug
    {
        public static Texture2D Pixel;
        public static bool SHOWBOUNDS = false;
        // Draws a hollow outline so you can still see the sprite inside.
        public static void DrawRect(SpriteBatch sb, Rectangle r, Color c, int thickness = 2)
        {
            if (SHOWBOUNDS){
                if (Pixel == null) return;
                sb.Draw(Pixel, new Rectangle(r.X, r.Y, r.Width, thickness), c);                    // top
                sb.Draw(Pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), c);   // bottom
                sb.Draw(Pixel, new Rectangle(r.X, r.Y, thickness, r.Height), c);                   // left
                sb.Draw(Pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), c);   // right
            }
        }
    }
}