using System;
using System.Collections.Generic;
using Ashenveil.Core.UI.Widgets;
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
        private Label _message;

        /// <param name="onSave">The only way to save: there's no autosave and no save on quit.</param>
        /// <param name="onLoad">Throws this game away for the saved one (asking first if there's unsaved play).</param>
        public Pause(SpriteFont font, Action onResume, Action onSave, Action onLoad, Action onQuit, int screenWidth, int screenHeight)
        {
            this.font = font;

            // Four buttons, the middle of the stack on the middle of the screen.
            int btnW = 200, btnH = 50, gap = 20;
            int centreX = screenWidth / 2 - btnW / 2;
            var resumeRect  = new Rectangle(centreX, screenHeight / 2 - btnH * 2 - gap * 3 / 2, btnW, btnH);
            var saveRect    = new Rectangle(centreX, resumeRect.Bottom + gap, btnW, btnH);
            var loadRect    = new Rectangle(centreX, saveRect.Bottom + gap, btnW, btnH);
            var quitRect    = new Rectangle(centreX, loadRect.Bottom + gap, btnW, btnH);
            var messageRect = new Rectangle(0, quitRect.Bottom + gap, screenWidth, btnH);

            widgets.Add(new Button(resumeRect, "Resume", "button_brown", onResume));
            widgets.Add(new Button(saveRect,   "Save", "button_brown", onSave));
            widgets.Add(new Button(loadRect,   "Load", "button_brown", onLoad));
            widgets.Add(new Button(quitRect,   "Quit to Desktop", "button_brown", onQuit));
            _message = new Label(messageRect, "", Color.White);
            widgets.Add(_message);
        }

        /// <summary>
        /// A line under the buttons: how the last save or load went. Cleared each time the menu opens,
        /// so an old "Game saved." isn't mistaken for a new one.
        /// </summary>
        public void ShowMessage(string text, bool good)
        {
            _message.Text = text;
            _message.Color = good ? Color.LightGreen : Color.Salmon;
        }

        public void ClearMessage() => _message.Text = "";

        public void Update(GameTime gameTime)
        {
            var mouse = AshenveilGame.Input.Mouse;
            foreach (var w in widgets) w.Update(mouse, prevMouse);
            prevMouse = mouse;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var w in widgets) w.Draw(spriteBatch, font);
        }
    }
}
