using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ashenveil.Core.DevConsole;
using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// The dev console: a dark panel over the top of the world with the output above and a text
    /// field below. Type a command and press Enter; Up/Down go through earlier commands; Escape
    /// (or the toggle key, handled by AshenveilGame) closes it. Pushed on the screen stack like
    /// the chat box, so the world stops updating while it's open.
    ///
    /// Kept for the whole game rather than built fresh on each open, so its output and history
    /// are still there next time. The commands themselves live in a CommandRegistry.
    /// </summary>
    public class ConsoleScreen : IScreen
    {
        /// <summary>
        /// The keys that open and close it: ` on a US layout is OemTilde, on a UK layout Oem8.
        /// </summary>
        public static readonly Keys[] ToggleKeys = { Keys.OemTilde, Keys.Oem8 };

        public const int MaxLines = 200;
        private const int Pad = 12, InputH = 40, Gap = 8, MinHeight = 260;

        public List<Widget> Widgets { get; set; } = new List<Widget>();
        private readonly SpriteFont _font;
        private readonly CommandRegistry _commands;
        private readonly Action _onClose;

        // What's been written, oldest first, capped at MaxLines. The source of truth: the
        // ChatLog only draws it, and is rebuilt from it when the window size changes.
        private readonly List<ConsoleLine> _lines = new();
        // Lines from other threads (a `health` answer), moved into _lines on the game's thread.
        private readonly ConcurrentQueue<ConsoleLine> _later = new();
        // Commands typed, oldest first. _historyIndex == _history.Count means "not browsing".
        private readonly List<string> _history = new();
        private int _historyIndex;

        private Rectangle _panel;
        private ChatLog _log;
        private TextField _field;
        private KeyboardState _prevKb;
        private MouseState _prevMouse;

        public ConsoleScreen(SpriteFont font, CommandRegistry commands, Action onClose, int screenWidth, int screenHeight)
        {
            _font = font;
            _commands = commands;
            _onClose = onClose;

            // `clear` is about this screen's output, so it's registered here rather than with
            // the world commands.
            _commands.Register("clear", "", "Clears the output.", _ => { Clear(); return Array.Empty<string>(); });

            Resize(screenWidth, screenHeight);
            Write("Dev console. Type 'help' for the commands; Esc or ` closes it.", LineKind.Event);
        }

        /// <summary>Everything written so far, oldest first (at most <see cref="MaxLines"/>).</summary>
        public IReadOnlyList<ConsoleLine> Lines => _lines;

        /// <summary>What's typed in the field right now.</summary>
        public string Typed => _field.Text;

        /// <summary>
        /// Call before pushing the screen: fits it to the window as it is now, and starts from
        /// the keys held now, so the key that opened it isn't also read as a press.
        /// </summary>
        public void Open(int screenWidth, int screenHeight)
        {
            Resize(screenWidth, screenHeight);
            _prevKb = AshenveilGame.Input.Keyboard;
            _prevMouse = AshenveilGame.Input.Mouse;
            _historyIndex = _history.Count;
            WritePending();
        }

        // Lays the panel out along the top of the screen. The log is rebuilt from _lines, which
        // is cheap at 200 lines and keeps the wrapping right for the new width.
        private void Resize(int screenWidth, int screenHeight)
        {
            int panelH = Math.Max(MinHeight, screenHeight * 45 / 100);
            _panel = new Rectangle(0, 0, screenWidth, panelH);
            var input = new Rectangle(Pad, panelH - Pad - InputH, screenWidth - Pad * 2, InputH);
            var logRect = new Rectangle(Pad, Pad, screenWidth - Pad * 2, input.Y - Gap - Pad);

            string typed = _field?.Text ?? "";
            _field = new TextField(input, "button_brown", Submit, placeholder: "Type a command...", maxLength: 200)
            {
                Text = typed,
                Focused = true,
            };
            _log = new ChatLog(logRect, MaxLines);
            foreach (var line in _lines) _log.Add(line.Text, ColorOf(line.Kind));

            Widgets = new List<Widget> { _log, _field };
        }

        // The ScreenManager hands typing to whichever screen is on top. The toggle key's own
        // character (` or ~) is never part of a command, and would otherwise be typed by the
        // very press that opened the console.
        public void OnTextInput(char c)
        {
            if (c == '`' || c == '~' || c == '¬') return;
            _field.Type(c);
        }

        /// <summary>Runs a typed line: echoes it, remembers it, writes what the command said.</summary>
        public void Submit(string text)
        {
            text = text.Trim();
            _field.Text = "";
            if (text.Length == 0) return;

            Write("> " + text, LineKind.Input);
            if (_history.Count == 0 || _history[^1] != text) _history.Add(text);   // no repeats in a row
            _historyIndex = _history.Count;

            foreach (var line in _commands.Execute(text)) Write(line.Text, line.Kind);
        }

        /// <summary>Adds a line to the output. Also how posted game events reach the console.</summary>
        public void Write(string text, LineKind kind)
        {
            _lines.Add(new ConsoleLine(text, kind));
            if (_lines.Count > MaxLines) _lines.RemoveAt(0);
            _log.Add(text, ColorOf(kind));
        }

        /// <summary>
        /// Adds a line from any thread: it's queued, and written on the game's thread at the next
        /// Update (or the next Open, if the console was closed meanwhile).
        /// </summary>
        public void WriteLater(ConsoleLine line) => _later.Enqueue(line);

        private void WritePending()
        {
            while (_later.TryDequeue(out var line)) Write(line.Text, line.Kind);
        }

        public void Clear()
        {
            _lines.Clear();
            _log.Clear();
        }

        /// <summary>Up: the command before the one shown. Stops at the oldest.</summary>
        public void RecallOlder()
        {
            if (_history.Count == 0) return;
            _historyIndex = Math.Max(0, _historyIndex - 1);
            _field.Text = _history[_historyIndex];
        }

        /// <summary>Down: the command after the one shown; past the newest, an empty field.</summary>
        public void RecallNewer()
        {
            if (_historyIndex >= _history.Count) return;
            _historyIndex++;
            _field.Text = _historyIndex < _history.Count ? _history[_historyIndex] : "";
        }

        public void Update(GameTime gameTime)
        {
            WritePending();
            var mouse = AshenveilGame.Input.Mouse;
            foreach (var w in Widgets) w.Update(mouse, _prevMouse);
            _field.Focused = true;   // the only field here: a click on the output mustn't unfocus it
            _prevMouse = mouse;

            var kb = AshenveilGame.Input.Keyboard;
            if (Pressed(kb, Keys.Up)) RecallOlder();
            if (Pressed(kb, Keys.Down)) RecallNewer();
            bool close = Pressed(kb, Keys.Escape);
            _prevKb = kb;

            if (close) _onClose();
        }

        private bool Pressed(KeyboardState kb, Keys key) => kb.IsKeyDown(key) && _prevKb.IsKeyUp(key);

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, _panel, Color.Black * 0.8f);   // translucent: the world shows through
            foreach (var w in Widgets) w.Draw(spriteBatch, _font);
        }

        private static Color ColorOf(LineKind kind) => kind switch
        {
            LineKind.Input => Color.LightSkyBlue,
            LineKind.Error => Color.Salmon,
            LineKind.Event => Color.Gold,
            _ => Color.White,
        };
    }
}
