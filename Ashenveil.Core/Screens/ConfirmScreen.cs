using System;
using System.Collections.Generic;
using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// A question with two answers, over whatever screen asked it ("Quit without saving?").
    /// It only reports the answer: whoever pushed it decides what each one does, including
    /// popping it again.
    /// </summary>
    public class ConfirmScreen : IScreen
    {
        public List<Widget> Widgets { get; set; } = new List<Widget>();
        private readonly SpriteFont _font;
        private MouseState _prevMouse;

        public ConfirmScreen(SpriteFont font, string question, string yesText, Action onYes, string noText, Action onNo,
                             int screenWidth, int screenHeight)
        {
            _font = font;
            // Start from what's held right now, so the click that opened this isn't also read as an answer.
            _prevMouse = AshenveilGame.Input.Mouse;

            int boxW = 520, boxH = 200, btnW = 180, btnH = 50, pad = 24;
            var box = new Rectangle(screenWidth / 2 - boxW / 2, screenHeight / 2 - boxH / 2, boxW, boxH);
            var questionRect = new Rectangle(box.X + pad, box.Y + pad, boxW - pad * 2, boxH - btnH - pad * 3);
            var yesRect = new Rectangle(box.X + pad, box.Bottom - pad - btnH, btnW, btnH);
            var noRect = new Rectangle(box.Right - pad - btnW, yesRect.Y, btnW, btnH);

            Widgets.Add(new Panel(box, "panel_brown_dark"));
            Widgets.Add(new Label(questionRect, question, Color.White, wrap: true));
            Widgets.Add(new Button(yesRect, yesText, "button_brown", onYes));
            Widgets.Add(new Button(noRect, noText, "button_brown", onNo));
        }

        public void Update(GameTime gameTime)
        {
            var mouse = AshenveilGame.Input.Mouse;
            // A copy: an answer can pop this screen, and the caller may then change the list.
            foreach (var w in Widgets.ToArray()) w.Update(mouse, _prevMouse);
            _prevMouse = mouse;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var w in Widgets) w.Draw(spriteBatch, _font);
        }
    }
}
