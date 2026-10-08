using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ashenveil.Core.Dialogue;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Utility;
using Ashenveil.Core.Utility.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens.Chat
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
        private const int Gap = 8, MessageGap = 6, InputPad = 12, MaxDraft = 200;
        public List<Widget> Widgets { get; set;} = new List<Widget>();
        private SpriteFont font;
        private MouseState prevMouse;
        private KeyboardState prevKb;

        private Conversation _conversation;
        private Action _onClose;
        private GameWindow _window;
        private NPC _speaker;
        private Rectangle _log, _input;

        // What the player has typed but not sent yet.
        private string _draft = "";
        // Each message word-wrapped to the log's width, in step with Conversation.Messages.
        // Messages only ever get added, so a wrapped one never needs redoing.
        private List<List<string>> _wrapped = new List<List<string>>();
        // The E that opened the chat is still held on our first frames; typing stays off
        // until it's let go, so holding it doesn't fill the box with e's.
        private bool _waitForRelease = true;
        private double _time;

        public ChatScreen(SpriteFont font, Conversation conversation, GameWindow window, Action onClose, int screenWidth, int screenHeight)
        {
            this.font = font;
            _conversation = conversation;
            _window = window;
            _onClose = onClose;

            // Start from what's held right now, so the click or key that opened the chat
            // isn't also read as a press on our first frame.
            prevMouse = Mouse.GetState();
            prevKb = Keyboard.GetState();

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
            _input = new Rectangle(colX, rowY, sendRect.X - Gap - colX, BtnH);
            _log = new Rectangle(colX, box.Y + Pad, box.Right - Pad - colX, rowY - Gap - (box.Y + Pad));

            // The portrait is whoever the player walked up to (the first NPC in the conversation).
            _speaker = conversation.Participants.OfType<NPC>().FirstOrDefault();

            Widgets.Add(new Panel(box, "panel_brown_dark"));
            Widgets.Add(new Image(face, _speaker?.Portrait == null ? null : Textures.Get(_speaker.Portrait)));
            Widgets.Add(new Label(nameRect, _speaker?.Name ?? "", Color.Gold));
            Widgets.Add(new Panel(_input, "button_brown", border: 6));
            Widgets.Add(new Button(sendRect, "Send", "button_brown", Send));
            Widgets.Add(new Button(leaveRect, "Leave", "button_brown", Close));

            // Typed characters arrive as events, already shifted and key-repeated by the OS.
            _window.TextInput += OnTextInput;
        }

        private void OnTextInput(object sender, TextInputEventArgs e)
        {
            if (_waitForRelease) return;

            if (e.Key == Keys.Enter || e.Character == '\r') Send();
            else if (e.Key == Keys.Back || e.Character == '\b')
            {
                if (_draft.Length > 0) _draft = _draft[..^1];
            }
            // Only what the font can draw: DrawString throws on a glyph it doesn't have.
            else if (_draft.Length < MaxDraft && font.Characters.Contains(e.Character))
                _draft += e.Character;
        }

        private void Send()
        {
            // The draft is kept while a reply is on its way, so it can be sent once it lands.
            if (_conversation.IsLoading || _draft.Trim().Length == 0) return;
            _conversation.Say(Conversation.PlayerName, _draft.Trim());
            _draft = "";
        }

        // Walking away: mark the conversation finished so the world releases the NPCs.
        private void Close()
        {
            _window.TextInput -= OnTextInput;   // or this screen keeps hearing keystrokes after it's gone
            _conversation.End();
            _onClose();
        }

        public void Update(GameTime gameTime)
        {
            _time = gameTime.TotalGameTime.TotalSeconds;

            // The world isn't updating while we're on top, so collecting the LLM's reply is our job.
            _conversation.Poll();

            var mouse = Mouse.GetState();
            foreach (var w in Widgets) w.Update(mouse, prevMouse);
            prevMouse = mouse;

            var kb = Keyboard.GetState();
            if (_waitForRelease && kb.IsKeyUp(Keys.E)) _waitForRelease = false;
            if (Pressed(kb, Keys.Escape)) Close();
            prevKb = kb;
        }

        private bool Pressed(KeyboardState kb, Keys key) => kb.IsKeyDown(key) && prevKb.IsKeyUp(key);

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var w in Widgets) w.Draw(spriteBatch, font);
            DrawLog(spriteBatch);
            DrawInput(spriteBatch);
        }

        // Newest message at the bottom, working upwards until the log is full. Older messages
        // simply fall off the top, so there's no scroll position to keep.
        private void DrawLog(SpriteBatch spriteBatch)
        {
            var messages = _conversation.Messages;
            while (_wrapped.Count < messages.Count)
                _wrapped.Add(UI.WrapText(font, Printable(Format(messages[_wrapped.Count])), _log.Width));

            float y = _log.Bottom;

            // While the LLM is writing: a reply-shaped line of dots that count up, 1 to 3.
            if (_conversation.IsLoading && _speaker != null)
            {
                y -= font.LineSpacing;
                string name = _speaker.Name + ":";
                spriteBatch.DrawString(font, name, new Vector2(_log.X, (int)y), Color.Gold);
                spriteBatch.DrawString(font, " " + new string('.', 1 + (int)(_time * 3) % 3),
                    new Vector2((int)(_log.X + font.MeasureString(name).X), (int)y), Color.White);
                y -= MessageGap;
            }

            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var lines = _wrapped[i];
                y -= lines.Count * font.LineSpacing;
                if (y < _log.Top) break;

                // The speaker's name is coloured, the rest of the line isn't, so the first
                // line is drawn in two pieces. A note from the game has no name: all grey.
                string name = messages[i].Speaker == null ? "" : Printable(messages[i].Speaker) + ":";
                Color nameColor = messages[i].Speaker == Conversation.PlayerName ? Color.LightSkyBlue : Color.Gold;
                Color textColor = messages[i].Speaker == null ? Color.LightGray : Color.White;

                for (int l = 0; l < lines.Count; l++)
                {
                    var pos = new Vector2(_log.X, (int)(y + l * font.LineSpacing));
                    if (l == 0 && name.Length > 0 && lines[0].StartsWith(name))
                    {
                        spriteBatch.DrawString(font, name, pos, nameColor);
                        pos.X = (int)(pos.X + font.MeasureString(name).X);
                        spriteBatch.DrawString(font, lines[0].Substring(name.Length), pos, textColor);
                    }
                    else spriteBatch.DrawString(font, lines[l], pos, textColor);
                }
                y -= MessageGap;
            }
        }

        private void DrawInput(SpriteBatch spriteBatch)
        {
            // Cursor blinks twice a second.
            string cursor = _time % 1 < 0.5 ? "_" : "";

            // A draft longer than the field shows its end, where the typing is happening.
            string shown = _draft;
            int room = _input.Width - InputPad * 2;
            while (shown.Length > 0 && font.MeasureString(shown + "_").X > room) shown = shown.Substring(1);

            var pos = new Vector2(_input.X + InputPad, (int)(_input.Y + (_input.Height - font.LineSpacing) / 2f));
            spriteBatch.DrawString(font, shown + cursor, pos, Color.Black);
        }

        private static string Format(ChatMessage message) =>
            message.Speaker == null ? message.Text : $"{message.Speaker}: {message.Text}";

        // The LLM writes whatever it likes; the font only has so many glyphs, and DrawString
        // throws on one it doesn't have. Swap the usual suspects for plain ones, "?" for the rest.
        private string Printable(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char raw in text)
            {
                char c = raw switch
                {
                    '‘' or '’' => '\'',
                    '“' or '”' => '"',
                    '–' or '—' => '-',
                    _ => raw,
                };
                if (c == '…') sb.Append("...");
                else sb.Append(c == '\n' || font.Characters.Contains(c) ? c : '?');
            }
            return sb.ToString();
        }
    }
}
