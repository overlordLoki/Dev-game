using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace merder.Core.Screens
{
    public class SettingsScreen : IScreen
    {
        private SpriteFont font;
        private Rectangle backButton;
        private Action onBack;
        private MouseState prevMouse;

        public SettingsScreen(SpriteFont font, Action onBack, int screenWidth, int screenHeight)
        {
            this.font = font;
            this.onBack = onBack;

            int btnW = 200, btnH = 50;
            int centreX = screenWidth / 2 - btnW / 2;
            backButton = new Rectangle(centreX, screenHeight - btnH - 40, btnW, btnH);
        }

        public void Update(GameTime gameTime)
        {
            var mouse = Mouse.GetState();
            if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
            {
                if (backButton.Contains(mouse.X, mouse.Y))
                {
                    onBack();
                }
            }
            prevMouse = mouse;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // title
            spriteBatch.DrawString(font, "Settings", new Vector2(40, 40), Color.White);

            DrawButton(spriteBatch, backButton, "Back", Color.Gray, pixel);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle button, String text, Color color, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, button, color);
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPos = new Vector2(
                button.X + (button.Width - textSize.X) / 2,
                button.Y + (button.Height - textSize.Y) / 2
            );
            spriteBatch.DrawString(font, text, textPos, Color.Black);
        }
    }
}
