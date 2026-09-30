using System;
using System.Collections.Generic;
using Ashenveil.Core.Utility.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    public class Pause : IScreen
    {
        public List<Widget> Widgets { get; set;} = new List<Widget>();
        private SpriteFont font;
        private List<Widget> widgets = new();
        private MouseState prevMouse;

        public Pause(SpriteFont font, Action onResume, Action onQuit, int screenWidth, int screenHeight)
        {
            this.font = font;

            int btnW = 200, btnH = 50, gap = 20;
            int centreX = screenWidth / 2 - btnW / 2;
            var resumeRect = new Rectangle(centreX, screenHeight / 2 - btnH, btnW, btnH);
            var quitRect   = new Rectangle(centreX, resumeRect.Bottom + gap, btnW, btnH);

            widgets.Add(new Button(resumeRect, "Resume", "button_brown", onResume));
            widgets.Add(new Button(quitRect,   "Quit to Desktop", "button_brown", onQuit));
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
