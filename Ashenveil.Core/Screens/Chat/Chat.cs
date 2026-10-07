using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Dialogue;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Utility.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens.Chat
{
    /// <summary>
    /// A dialogue box pushed on top of the World, showing a Conversation the player is part of.
    /// The screen only reads and advances the conversation; it doesn't own the lines. The world keeps drawing underneath but stops
    /// updating (the ScreenManager only updates the top screen), so nobody wanders off mid-talk.
    /// Click Next, or press Space / Enter / E, to advance; Escape or the last line closes it.
    /// </summary>
    public class ChatScreen : IScreen
    {
        private const int Margin = 40, Pad = 20, BoxH = 200, PortraitSize = 160, BtnW = 120, BtnH = 40;
        public List<Widget> Widgets { get; set;} = new List<Widget>();
        private SpriteFont font;
        private MouseState prevMouse;
        private KeyboardState prevKb;

        private Conversation _conversation;
        private Action _onClose;
        private Image _portrait;
        private Label _name, _text;
        private Button _next;

        public ChatScreen(SpriteFont font, Conversation conversation, Action onClose, int screenWidth, int screenHeight)
        {
            this.font = font;
            _conversation = conversation;
            _onClose = onClose;

            // Start from what's held right now, so the E that opened the chat isn't also read
            // as "advance" on our first frame.
            prevMouse = Mouse.GetState();
            prevKb = Keyboard.GetState();

            // Box along the bottom; portrait on its left, name + text to the right, Next bottom-right.
            var box = new Rectangle(Margin, screenHeight - BoxH - Margin, screenWidth - Margin * 2, BoxH);
            var face = new Rectangle(box.X + Pad, box.Y + Pad, PortraitSize, PortraitSize);
            int textX = face.Right + Pad;
            int textW = box.Right - Pad - textX;
            var nameRect = new Rectangle(textX, box.Y + Pad, textW, 30);
            var btnRect = new Rectangle(box.Right - Pad - BtnW, box.Bottom - Pad - BtnH, BtnW, BtnH);
            var textRect = new Rectangle(textX, nameRect.Bottom + 8, textW, btnRect.Top - nameRect.Bottom - 16);

            _portrait = new Image(face, null);
            _name = new Label(nameRect, "", Color.Gold, centred: false);
            _text = new Label(textRect, "", Color.White, centred: false, wrap: true);
            _next = new Button(btnRect, "Next", "button_brown", Advance);

            Widgets.Add(new Panel(box, "panel_brown_dark"));
            Widgets.Add(_portrait);
            Widgets.Add(_name);
            Widgets.Add(_text);
            Widgets.Add(_next);

            // Lines don't say who is speaking yet, so every line is shown as the first NPC
            // in the conversation (the player has no name or portrait).
            NPC speaker = conversation.Participants.OfType<NPC>().FirstOrDefault();
            _name.Text = speaker?.Name ?? "";
            _portrait.Texture = speaker?.Portrait == null ? null : Textures.Get(speaker.Portrait);

            Show();
        }

        private void Show()
        {
            int index = _conversation.CurrentLineIndex;
            _text.Text = _conversation.Lines[index];
            bool lastLine = index == _conversation.Lines.Length - 1;
            // On the last line: "..." if the LLM is still writing more, else "Close".
            _next.Text = _conversation.IsLoading && lastLine ? "..." : lastLine ? "Close" : "Next";
        }

        private void Advance()
        {
            // Still waiting on the LLM and sitting on the last line we have (the greeting):
            // there's nothing to advance to yet, so don't let the player close it early.
            if (_conversation.IsLoading && _conversation.CurrentLineIndex >= _conversation.Lines.Length - 1)
                return;

            _conversation.CurrentLineIndex++;
            if (_conversation.IsFinished) _onClose();
            else Show();
        }

        // Walking away mid-talk: skip to the end so the conversation reads as finished.
        private void End()
        {
            _conversation.CurrentLineIndex = _conversation.Lines.Length;
            _onClose();
        }

        public void Update(GameTime gameTime)
        {
            // Lines can grow under us while the LLM replies, so re-sync the box each frame
            // (cheap: two string assignments). This is what turns "..." into "Next" once the
            // generated lines arrive, with no event wiring.
            Show();

            var mouse = Mouse.GetState();
            foreach (var w in Widgets) w.Update(mouse, prevMouse);
            prevMouse = mouse;

            var kb = Keyboard.GetState();
            if (Pressed(kb, Keys.Escape)) End();
            else if (Pressed(kb, Keys.Space) || Pressed(kb, Keys.Enter) || Pressed(kb, Keys.E)) Advance();
            prevKb = kb;
        }

        private bool Pressed(KeyboardState kb, Keys key) => kb.IsKeyDown(key) && prevKb.IsKeyUp(key);

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var w in Widgets) w.Draw(spriteBatch, font);
        }
    }
}
