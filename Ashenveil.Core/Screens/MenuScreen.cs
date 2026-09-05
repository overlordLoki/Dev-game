using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ashenveil.Core.Utility;
namespace Ashenveil.Core.Screens
{
    public class Menu :IScreen
    {
        //UI for a button
        private SpriteFont font;
        private Rectangle newGameButton;
        private Rectangle settingsButton;
        private Action onNewGame;
        private Action onSettings;
        //Mouse
        private MouseState prevMouse;

        public Menu(SpriteFont font, Action action, Action onSettings, int screenWidth, int screenHeight)
        {
            this.font = font;
            this.onNewGame = action;
            this.onSettings = onSettings;
            
            int btnW = 200, btnH = 50, gap = 20;
            int centreX = screenWidth / 2 - btnW / 2;

            newGameButton = new Rectangle(centreX, screenHeight / 2 - btnH / 2, btnW, btnH);
            settingsButton = new Rectangle(centreX, newGameButton.Bottom + gap, btnW, btnH);
        }
        public void Update(GameTime gameTime)
        {
            var mouse = Mouse.GetState();
            if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)//they left clicked somthing
            {
                //was it this button?
                if (newGameButton.Contains(mouse.X, mouse.Y))
                {
                    onNewGame();
                }
                //was it settings?
                if (settingsButton.Contains(mouse.X, mouse.Y))
                {
                    onSettings();
                }
            }
            prevMouse = mouse;
        }
        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            //draw the button
            UI.DrawButton(spriteBatch, newGameButton, "New Game", Color.Green, pixel,font);
            UI.DrawButton(spriteBatch, settingsButton, "Settings", Color.Blue, pixel,font);
        }

    }
}