using System;
using System.Collections.Generic;
using Ashenveil.Core.UI.Widgets;
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
        private Label _message;

        /// <param name="onContinue">Carries on the saved game. Null when there's no save: no Continue button.</param>
        public Menu(SpriteFont font, Action onContinue, Action action, Action onSettings, int screenWidth, int screenHeight)
        {
            this.font = font;

            int btnW = 200, btnH = 50, gap = 20;
            int centreX = screenWidth / 2 - btnW / 2;
            var continueRect = new Rectangle(centreX, screenHeight / 2 - btnH / 2 - btnH - gap, btnW, btnH);
            var newGameRect  = new Rectangle(centreX, screenHeight / 2 - btnH / 2, btnW, btnH);
            var settingsRect = new Rectangle(centreX, newGameRect.Bottom + gap, btnW, btnH);
            var messageRect  = new Rectangle(0, settingsRect.Bottom + gap, screenWidth, btnH);

            // Adding a button is now one line here — no field, no hit-test, no Draw call.
            if (onContinue != null) widgets.Add(new Button(continueRect, "Continue", "button_brown", onContinue));
            widgets.Add(new Button(newGameRect,  "New Game", "button_brown", action));
            widgets.Add(new Button(settingsRect, "Settings", "button_brown", onSettings));
            _message = new Label(messageRect, "", Color.Salmon);
            widgets.Add(_message);
        }

        /// <summary>A line under the buttons, such as why Continue didn't work. Empty hides it.</summary>
        public string Message { set => _message.Text = value; }

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
