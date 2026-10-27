using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities;
using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// A chat box pushed on top of the World, showing a Conversation the player is part of.
    /// The screen only reads the conversation and says the player's lines into it; it doesn't own the
    /// transcript. The world keeps drawing underneath but stops updating (the ScreenManager only updates
    /// the top screen), so nobody wanders off mid-talk.
    /// Type and press Enter (or click Send) to talk; Escape or Leave closes it.
    /// </summary>
    public class ChatScreen : IScreen
    {
        private const int Margin = 40, Pad = 20, BoxH = 260, PortraitSize = 160, BtnW = 90, BtnH = 40;
        private const int Gap = 8;
        public List<Widget> Widgets { get; set;} = new List<Widget>();
        private SpriteFont font;
        private MouseState prevMouse;
        private KeyboardState prevKb;

        private Conversation _conversation;
        private Action _onClose;
        private NPC _speaker;
        // The player typing here. Taken from the conversation rather than the World, so it's
        // whichever player is in this chat.
        private Player _player;
        private ChatLog _log;
        private TextField _textField;

        // How many of Conversation.Messages are already in the log. Messages only ever get
        // added, so each new one is copied in once.
        private int _shown;
        // The E that opened the chat is still held on our first frames; typing stays off
        // until it's let go, so holding it doesn't fill the box with e's.
        private bool _waitForRelease = true;
        private double _time;

        public ChatScreen(SpriteFont font, Conversation conversation, Action onClose, int screenWidth, int screenHeight)
        {
            this.font = font;
            _conversation = conversation;
            _onClose = onClose;

            // Start from what's held right now, so the click or key that opened the chat
            // isn't also read as a press on our first frame.
            prevMouse = AshenveilGame.Input.Mouse;
            prevKb = AshenveilGame.Input.Keyboard;

            // Box along the bottom. Portrait and name on its left; to the right the transcript,
            // with the input row (text field, Send, Leave) underneath it.
            int boxH = Math.Max(BoxH, screenHeight * 45 / 100);
            var box = new Rectangle(Margin, screenHeight - boxH - Margin, screenWidth - Margin * 2, boxH);
            var face = new Rectangle(box.X + Pad, box.Y + Pad, PortraitSize, PortraitSize);
            var nameRect = new Rectangle(face.X, face.Bottom + Gap, PortraitSize, 30);
            int colX = face.Right + Pad;
            int rowY = box.Bottom - Pad - BtnH;
            var leaveRect = new Rectangle(box.Right - Pad - BtnW, rowY, BtnW, BtnH);
            var sendRect = new Rectangle(leaveRect.X - Gap - BtnW, rowY, BtnW, BtnH);
            var input = new Rectangle(colX, rowY, sendRect.X - Gap - colX, BtnH);
            var logRect = new Rectangle(colX, box.Y + Pad, box.Right - Pad - colX, rowY - Gap - (box.Y + Pad));

            // The portrait is whoever the player walked up to (the first NPC in the conversation).
            _speaker = conversation.Participants.OfType<NPC>().FirstOrDefault();
            _player = conversation.Participants.OfType<Player>().First();   // only opened for the player's chats

            _textField = new TextField(input, "button_brown", _ => Send(), placeholder: "Say something...", maxLength: 200)
            {
                Focused = true,   // the chat opens ready to type
            };
            _log = new ChatLog(logRect);

            // Drawn in this order, so the box panel goes first and everything sits on top of it.
            Widgets.Add(new Panel(box, "panel_brown_dark"));
            Widgets.Add(new Image(face, _speaker?.Portrait == null ? null : Textures.Get(_speaker.Portrait)));
            Widgets.Add(new Label(nameRect, _speaker?.Name ?? "", Color.Gold));
            Widgets.Add(_log);
            Widgets.Add(_textField);
            Widgets.Add(new Button(sendRect, "Send", "button_brown", Send));
            Widgets.Add(new Button(leaveRect, "Leave", "button_brown", Close));

            ShowNewMessages();   // the opening note is already there
        }

        // The ScreenManager hands typing to whichever screen is on top.
        public void OnTextInput(char c)
        {
            if (_waitForRelease) return;   // the E that opened the chat is still held
            _textField.Type(c);            // Enter, Backspace and the length limit are the field's job
        }

        private void Send()
        {
            if (_conversation.IsLoading || _textField.Text.Trim().Length == 0) return;
            _conversation.Say(_player.Name, _textField.Text.Trim());
            _textField.Text = "";   // the owner decides to clear, not the widget
        }

        // Walking away: mark the conversation finished so the world releases the NPCs.
        private void Close()
        {
            _conversation.End();
            _onClose();
        }

        public void Update(GameTime gameTime)
        {
            _time = gameTime.TotalGameTime.TotalSeconds;

            // The world isn't updating while we're on top, so collecting the LLM's reply is our job.
            _conversation.Poll();
            ShowNewMessages();

            // While the LLM is writing: a reply-shaped line of dots that count up, 1 to 3.
            _log.Pending = _conversation.IsLoading && _speaker != null
                ? (_speaker.Name, Color.Gold, new string('.', 1 + (int)(_time * 3) % 3), Color.White)
                : null;

            var mouse = AshenveilGame.Input.Mouse;
            foreach (var w in Widgets) w.Update(mouse, prevMouse);
            _textField.Focused = true;   // the only field here: a click on Send or the log mustn't unfocus it
            prevMouse = mouse;

            var kb = AshenveilGame.Input.Keyboard;
            if (_waitForRelease && kb.IsKeyUp(Keys.E)) _waitForRelease = false;
            if (Pressed(kb, Keys.Escape)) Close();
            prevKb = kb;
        }

        private bool Pressed(KeyboardState kb, Keys key) => kb.IsKeyDown(key) && prevKb.IsKeyUp(key);

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var w in Widgets) w.Draw(spriteBatch, font);
        }

        // Copies messages the log hasn't got yet into it. The player's name is blue, an NPC's
        // gold; a note from the game has no name and is all grey.
        private void ShowNewMessages()
        {
            var messages = _conversation.Messages;
            for (; _shown < messages.Count; _shown++)
            {
                var m = messages[_shown];
                if (m.Speaker == null) _log.Add(m.Text, Color.LightGray);
                else _log.Add(m.Text, Color.White, m.Speaker,
                              m.Speaker == _player.Name ? Color.LightSkyBlue : Color.Gold);
            }
        }
    }
}
