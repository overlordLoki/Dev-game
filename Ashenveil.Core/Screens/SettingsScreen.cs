using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ashenveil.Core.Config;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// Rows of label plus widget over the chosen <see cref="GameSettings"/>:
    ///  - Backend server: a text field and "Test connection", which calls /health on whatever is
    ///    typed (saved or not) and shows the answer underneath. The address is only taken on
    ///    Enter or Back, so half-typed addresses are never used.
    ///  - Dev mode: a checkbox, applied the moment it's clicked. Only shown where dev mode is
    ///    allowed at all (Debug builds, or Release started with --dev).
    /// Back (or Escape) takes the address and saves. Changing a setting goes through the commit
    /// action (AshenveilGame.CommitSettings: apply, then save), which throws if it can't save.
    ///
    /// Built once and kept; <see cref="Open"/> lays it out for the window and refills the field
    /// from the settings each time it's shown.
    /// </summary>
    public class SettingsScreen : IScreen
    {
        private const int Margin = 40, LabelW = 240, Gap = 16, RowH = 50, TestW = 240, BoxH = 32;

        private static readonly Color Hint = Color.LightGray, Good = Color.LightGreen, Bad = Color.Salmon, Waiting = Color.Gold;

        public List<Widget> Widgets { get; set; } = new List<Widget>();
        private readonly SpriteFont _font;
        private readonly GameSettings _settings;
        private readonly Action _commit;
        private readonly Func<string, Task<HealthResult>> _checkHealth;
        private readonly bool _showDevMode;
        private readonly Action _onBack;

        private TextField _url;
        private Label _status;
        private Task<HealthResult> _health;   // the check in flight, if any
        private bool _saveFailed;             // the last save failed: Back again leaves anyway
        private MouseState _prevMouse;
        private KeyboardState _prevKb;

        public SettingsScreen(SpriteFont font, GameSettings settings, Action commit,
                              Func<string, Task<HealthResult>> checkHealth, bool showDevMode, Action onBack)
        {
            _font = font;
            _settings = settings;
            _commit = commit;
            _checkHealth = checkHealth;
            _showDevMode = showDevMode;
            _onBack = onBack;
        }

        /// <summary>
        /// Call before pushing the screen: lays it out for the window as it is now, shows the
        /// saved values, and starts from the keys and mouse held now so the click that opened it
        /// isn't also read here.
        /// </summary>
        public void Open(int screenWidth, int screenHeight)
        {
            int fieldX = Margin + LabelW + Gap;
            int fieldW = Math.Clamp(screenWidth - fieldX - Gap - TestW - Margin, 200, 520);
            int y = 120;

            var urlRow = new Rectangle(fieldX, y, fieldW, RowH);
            _url = new TextField(urlRow, "button_brown", _ => TakeUrl(),
                                 placeholder: GameSettings.DefaultBackendUrl, maxLength: 200)
            {
                Text = _settings.backendUrl,
            };
            _status = new Label(new Rectangle(fieldX, urlRow.Bottom + 10, screenWidth - fieldX - Margin, 60),
                                "Enter or Back saves the address. Test connection checks it first.",
                                Hint, centred: false, wrap: true);

            Widgets = new List<Widget>
            {
                new Label(new Rectangle(Margin, y, LabelW, RowH), "Backend server", Color.White, centred: false),
                _url,
                new Button(new Rectangle(urlRow.Right + Gap, y, TestW, RowH), "Test connection", "button_brown", TestConnection),
                _status,
            };

            if (_showDevMode)
            {
                y = urlRow.Bottom + 90;   // below the status line
                Widgets.Add(new Label(new Rectangle(Margin, y, LabelW, BoxH), "Dev mode", Color.White, centred: false));
                Widgets.Add(new Checkbox(new Rectangle(fieldX, y, fieldW, BoxH), "The ` console and other dev tools",
                                         "brown", _settings.devMode, SetDevMode));
            }

            int btnW = 200, btnH = 50;
            Widgets.Add(new Button(new Rectangle(screenWidth / 2 - btnW / 2, screenHeight - btnH - 40, btnW, btnH),
                                   "Back", "button_brown", Back));

            _health = null;
            _saveFailed = false;
            _prevMouse = AshenveilGame.Input.Mouse;
            _prevKb = AshenveilGame.Input.Keyboard;
        }

        /// <summary>What's typed in the address field right now.</summary>
        public string TypedUrl => _url.Text;

        /// <summary>The line under the address: hints, the test result, or what went wrong.</summary>
        public string Status => _status.Text;

        // Only the focused field takes it; clicking the field focuses it.
        public void OnTextInput(char c) => _url.Type(c);

        /// <summary>
        /// Takes the typed address into the settings and saves, if it's valid and new. False
        /// (with the reason shown) if it isn't valid or couldn't be saved.
        /// </summary>
        public bool TakeUrl()
        {
            if (!BackendUrl.TryNormalize(_url.Text, out var url, out var error))
            {
                Show(error, Bad);
                return false;
            }
            _url.Text = url;   // show it as it's stored, e.g. with the http:// added
            if (url == _settings.backendUrl) return true;

            _settings.backendUrl = url;
            if (!Commit()) return false;
            Show($"Saved. Requests now go to {url}.", Good);
            return true;
        }

        /// <summary>Calls /health on the typed address, without taking it. The answer shows when it comes.</summary>
        public void TestConnection()
        {
            if (!BackendUrl.TryNormalize(_url.Text, out var url, out var error))
            {
                Show(error, Bad);
                return;
            }
            Show($"Checking {url}...", Waiting);
            _health = _checkHealth(url);
        }

        public void SetDevMode(bool on)
        {
            _settings.devMode = on;
            if (Commit()) Show(on ? "Dev mode on: ` opens the console in game." : "Dev mode off.", Hint);
        }

        /// <summary>Takes the address and leaves. Stays, showing why, if the address is invalid or saving failed.</summary>
        public void Back()
        {
            // After a failed save, a second Back leaves anyway: the change still works this
            // session, it just won't be there next time.
            bool failedBefore = _saveFailed;   // read first: TakeUrl may fail a save and set it
            if (TakeUrl() || failedBefore) _onBack();
        }

        // Apply and save. A failed save is shown, not thrown: settings are never worth a crash.
        private bool Commit()
        {
            try
            {
                _commit();
                _saveFailed = false;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _saveFailed = true;
                Show($"Couldn't save the settings ({e.Message}). They apply until you quit. Back again to leave.", Bad);
                return false;
            }
        }

        private void Show(string text, Color color)
        {
            _status.Text = text;
            _status.Color = color;
        }

        public void Update(GameTime gameTime)
        {
            var mouse = AshenveilGame.Input.Mouse;
            foreach (var w in Widgets) w.Update(mouse, _prevMouse);
            _prevMouse = mouse;

            // The health check finishes on another thread; it's picked up here, on the game's.
            if (_health is { IsCompleted: true } done)
            {
                _health = null;
                var result = done.IsCompletedSuccessfully
                    ? done.Result
                    : new HealthResult(HealthStatus.Error, null, done.Exception?.GetBaseException().Message ?? "The check was cancelled.");
                Show(result.Message, result.Ok ? Good : Bad);
            }

            var kb = AshenveilGame.Input.Keyboard;
            bool escape = kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape);
            _prevKb = kb;
            if (escape) Back();
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.DrawString(_font, "Settings", new Vector2(Margin, 40), Color.White);
            foreach (var w in Widgets) w.Draw(spriteBatch, _font);
        }
    }
}
