using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace merder.Core.Utility
{
    public static class UI
    {
        public static void DrawButton(SpriteBatch spriteBatch, Rectangle button, String text, Color color, Texture2D pixel, SpriteFont font)
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
    }
}