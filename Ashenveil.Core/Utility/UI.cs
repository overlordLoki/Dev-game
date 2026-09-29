using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Ashenveil.Core.Utility
{
    public static class UI
    {
        public static void DrawButtonPixel(SpriteBatch spriteBatch, Rectangle button, String text, Color color, Texture2D pixel, SpriteFont font)
        {
            //draw the button on the screen
            spriteBatch.Draw(pixel, button, color);
            //draw the string
            // text centred on the button
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPos = new Vector2(
                button.X + (button.Width - textSize.X) / 2,
                button.Y + (button.Height - textSize.Y) / 2
            );
            spriteBatch.DrawString(font, text, textPos, Color.Black);
        }
        // Draws an atlas region into any size of rectangle without warping its corners.
        // border = how many pixels of the SOURCE sprite are corner/edge.
        // scale  = how big those corners are drawn on screen (2 = double-size pixels).
        public static void DrawNineSlice(SpriteBatch spriteBatch, TextureAtlas atlas, string region,
                                        Rectangle dest, int border, int scale, Color color)
        {
            Rectangle src = atlas[region];

            // Corner size on screen. Clamp it so a tiny button doesn't make the corners overlap.
            int db = Math.Min(border * scale, Math.Min(dest.Width, dest.Height) / 2);

            // The 4 cut lines across and down, in the source and in the destination.
            int[] sx = { src.Left,  src.Left  + border, src.Right  - border, src.Right  };
            int[] sy = { src.Top,   src.Top   + border, src.Bottom - border, src.Bottom };
            int[] dx = { dest.Left, dest.Left + db,     dest.Right  - db,    dest.Right  };
            int[] dy = { dest.Top,  dest.Top  + db,     dest.Bottom - db,    dest.Bottom };

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var s = new Rectangle(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]);
                    var d = new Rectangle(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]);
                    spriteBatch.Draw(atlas.Texture, d, s, color);
                }
            }
        }
        public static void DrawButton(SpriteBatch spriteBatch, Rectangle button, string text,
                              string sprite, SpriteFont font, Color color, bool hovered = false)
        {
            // Slightly darker when the mouse is over it: cheap hover feedback without a second sprite.
            Color tint = hovered ? new Color(210, 210, 210) : Color.White;

            DrawNineSlice(spriteBatch, AshenveilGame.UiAtlas, sprite, button, border: 6, scale: 2, tint);

            // Centre the text. Cast to int so it lands on whole pixels (fractions make text blurry).
            Vector2 size = font.MeasureString(text);
            var pos = new Vector2(
                (int)(button.X + (button.Width  - size.X) / 2),
                (int)(button.Y + (button.Height - size.Y) / 2));

            spriteBatch.DrawString(font, text, pos, color);
        }


    }
}