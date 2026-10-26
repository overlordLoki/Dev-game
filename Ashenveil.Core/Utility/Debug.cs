
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Utility
{
    public static class Debug
    {
        public static Texture2D Pixel;
        public static bool SHOWBOUNDS = false;
        // The nav grid's blocked cells and each NPC's path (the `nav` console command).
        public static bool SHOWNAV = false;
        // Dev-only tools, like the ` console. Set from the settings (GameSettings.devMode) by
        // AshenveilGame.ApplySettings, and only ever on where DevModeAllowed.
        public static bool DEVMODE = false;

        // Whether dev mode can be turned on at all, and so whether the settings screen offers
        // it: always in a Debug build; in a Release build only when started with --dev, so a
        // shipped build can be tested without players stumbling on the console.
#if DEBUG
        public static readonly bool DevModeAllowed = true;
#else
        public static readonly bool DevModeAllowed =
            System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "--dev");
#endif
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

        // A straight line between two screen points: the 1x1 pixel stretched to the line's
        // length and turned to its angle. The origin's 0.5 centres it across the line.
        public static void DrawLine(SpriteBatch sb, Vector2 from, Vector2 to, Color c, float thickness = 2f)
        {
            if (Pixel == null) return;
            Vector2 d = to - from;
            float angle = (float)System.Math.Atan2(d.Y, d.X);
            sb.Draw(Pixel, from, null, c, angle, new Vector2(0, 0.5f), new Vector2(d.Length(), thickness), SpriteEffects.None, 0f);
        }
    }
}