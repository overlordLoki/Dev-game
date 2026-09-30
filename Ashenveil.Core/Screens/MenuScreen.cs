using System;
using System.Collections.Generic;
using Ashenveil.Core.Utility.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    public class Menu : IScreen
    {
        public List<Widget> Widgets { get; set;} = new List<Widget>();
        private SpriteFont font;
        private List<Widget> widgets = new();
        private MouseState prevMouse;

        public Menu(SpriteFont font, Action action, Action onSettings, int screenWidth, int screenHeight)
        {
            this.font = font;

            int btnW = 200, btnH = 50, gap = 20;
            int centreX = screenWidth / 2 - btnW / 2;
            var newGameRect  = new Rectangle(centreX, screenHeight / 2 - btnH / 2, btnW, btnH);
            var settingsRect = new Rectangle(centreX, newGameRect.Bottom + gap, btnW, btnH);

            // Adding a button is now one line here — no field, no hit-test, no Draw call.
            widgets.Add(new Button(newGameRect,  "New Game", "button_brown", action));
            widgets.Add(new Button(settingsRect, "Settings", "button_brown", onSettings));
        }

        public void Update(GameTime gameTime)
        {
            var mouse = Mouse.GetState();
            foreach (var w in widgets) w.Update(mouse, prevMouse);
            prevMouse = mouse;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var w in widgets) w.Draw(spriteBatch, font);
        }
    }
}
