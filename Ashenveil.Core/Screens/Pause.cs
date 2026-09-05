using System;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
        public Pause(SpriteFont font, Action onResume, Action onQuit, int screenWidth, int screenHeight)
        {
            this.font = font;
            this.onResume = onResume;
            this.onQuit = onQuit;
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;

            int btnW = 200, btnH = 50;
            int centreX = screenWidth / 2 - btnW / 2;

            resumeButton = new Rectangle(centreX, screenHeight / 2 - btnH, btnW, btnH);
        }
        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            UI.DrawButton(spriteBatch, resumeButton, "Resume", Color.Green, pixel, font);
        }

        public void Update(GameTime gameTime)
        {


        }
        
    }
}