using System;
using System.Collections.Generic;
using Ashenveil.Core.Utility.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    public class SettingsScreen : IScreen
    {
        private SpriteFont font;
        private List<Widget> widgets = new();
        private MouseState prevMouse;

        public SettingsScreen(SpriteFont font, Action onBack, int screenWidth, int screenHeight)
        {
            this.font = font;

            int btnW = 200, btnH = 50;
            int centreX = screenWidth / 2 - btnW / 2;
            var backRect = new Rectangle(centreX, screenHeight - btnH - 40, btnW, btnH);

            widgets.Add(new Button(backRect, "Back", "button_brown", onBack));
        }

        public void Update(GameTime gameTime)
        {
            var mouse = Mouse.GetState();
            foreach (var w in widgets) w.Update(mouse, prevMouse);
            prevMouse = mouse;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.DrawString(font, "Settings", new Vector2(40, 40), Color.White);
            foreach (var w in widgets) w.Draw(spriteBatch, font);
        }
    }
}
