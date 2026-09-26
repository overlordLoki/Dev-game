using System;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    public class Pause : IScreen
    {
        private Action onResume;
        private Action onQuit;
        SpriteFont font;
        private int screenWidth;
        private int screenHeight;
        //buttons
        private Rectangle resumeButton;
        private Rectangle quitButton;
        //Mouse
        private MouseState prevMouse;

        public Pause(SpriteFont font, Action onResume, Action onQuit, int screenWidth, int screenHeight)
        {
            this.font = font;
            this.onResume = onResume;
            this.onQuit = onQuit;
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;

            int btnW = 200, btnH = 50, gap = 20;
            int centreX = screenWidth / 2 - btnW / 2;

            resumeButton = new Rectangle(centreX, screenHeight / 2 - btnH, btnW, btnH);
            quitButton = new Rectangle(centreX, resumeButton.Bottom + gap, btnW, btnH);
        }
        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            UI.DrawButton(spriteBatch, resumeButton, "Resume", Color.Green, pixel, font);
            UI.DrawButton(spriteBatch, quitButton, "Quit to Desktop", Color.Red, pixel, font);
        }

        public void Update(GameTime gameTime)
        {
            var mouse = Mouse.GetState();
            if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
            {
                if (resumeButton.Contains(mouse.X, mouse.Y))
                {
                    onResume();
                }
                if (quitButton.Contains(mouse.X, mouse.Y))
                {
                    onQuit();
                }
            }
            prevMouse = mouse;
        }

    }
}