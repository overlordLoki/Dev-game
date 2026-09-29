using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ashenveil.Core.Utility;
namespace Ashenveil.Core.Screens
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

            UI.DrawButtonPixel(spriteBatch, backButton, "Back", Color.Gray, pixel, font);
        }
    }
}
