using System;
using Ashenveil.Core.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Ashenveil.Core.Screens;
using Ashenveil.Core.Utility;
using Ashenveil.Core.Utility.Input;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Graphics;
using Ashenveil.Core.DevConsole;
using Ashenveil.Core.Config;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Saving;
namespace Ashenveil.Core
{
    /// <summary>
    /// The main class for the game: builds the screens and wires them together, and owns the
    /// game being played. Saving and loading from the menus is <see cref="SaveFlow"/>; the
    /// settings are <see cref="AppSettings"/>.
    /// </summary>
    public class AshenveilGame : Game
    {
        //UI fields
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private SpriteFont _font;
        // The game being played. Null on the main menu until New Game or Continue builds one.
        public World world;
        private GameplayScreen _gameplayScreen;
        private Menu _menu;
        private SettingsScreen _settingsScreen;
        private Pause _pause;
        private ConsoleScreen _console;
        private SaveFlow _saveFlow;
        private ScreenManager _screenManager = new();
        private KeyboardState _prevKb;
        private ButtonState _prevPadBack = ButtonState.Released;
        private AppSettings _settings;
        // Resources for drawing.
        private GraphicsDeviceManager graphicsDeviceManager;
        public static TextureAtlas UiAtlas { get; private set; }
        public static IInput Input { get; set; } = new LiveInput();

        /// <summary>
        /// Indicates if the game is running on a mobile platform.
        /// </summary>
        public readonly static bool IsMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        /// <summary>
        /// Indicates if the game is running on a desktop platform.
        /// </summary>
        public readonly static bool IsDesktop = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

        /// <summary>
        /// Initializes a new instance of the game: the window, and the graphics device as a service.
        /// </summary>
        public AshenveilGame()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreferredBackBufferWidth  = 1280;
            graphicsDeviceManager.PreferredBackBufferHeight = 720;
            graphicsDeviceManager.ApplyChanges();
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
            // Subscribed once, here, for the game's lifetime: screens get typing through
            // IScreen.OnTextInput, so none of them has to subscribe or remember to unsubscribe.
            Window.TextInput += (s, e) => _screenManager.TextInput(e.Character);
            IsMouseVisible = true;
            // Share GraphicsDeviceManager as a service.
            Services.AddService(typeof(GraphicsDeviceManager), graphicsDeviceManager);

            Content.RootDirectory = "Content";

            // Configure screen orientations.
            graphicsDeviceManager.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
        }

        private int ScreenWidth => GraphicsDevice.Viewport.Width;
        private int ScreenHeight => GraphicsDevice.Viewport.Height;

        private void OnResize(object sender, EventArgs e)
        {
            if (_pixel == null) return;
            UiScale.Update(ScreenHeight);
            // WorldBounds recomputes itself from Layout, so a resize only needs the layout
            // refreshed - no bounds object to rebuild anymore.
            _gameplayScreen?.UpdateLayout(ScreenWidth, ScreenHeight);
        }

        /// <summary>
        /// Initializes the game: the settings, then (through base.Initialize) the content, then
        /// the language.
        /// </summary>
        protected override void Initialize()
        {
            // Settings before base.Initialize, which runs LoadContent: everything built there
            // should see the saved values, not the defaults.
            _settings = new AppSettings(new SettingsStore(SettingsStore.DefaultFolder));
            _settings.Apply();

            base.Initialize();

            // TODO You should load this from a settings file or similar,
            // based on what the user or operating system selected.
            LocalizationManager.SetCulture(LocalizationManager.DEFAULT_CULTURE_CODE);
        }

        /// <summary>
        /// Loads game content, such as textures and fonts, and builds the screens.
        /// </summary>
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData([Color.White]);
            Textures.Init(Content);   // must run before anything calls Textures.Get
            UiScale.Update(ScreenHeight);
            var font = Content.Load<SpriteFont>("Fonts/Hud");
            font.DefaultCharacter = '?';   // draw '?' for a character the font lacks, instead of throwing
            _font = font;

            // Opened from the main menu's Continue and the pause menu's Save and Load.
            _saveFlow = new SaveFlow(_screenManager, font, GraphicsDevice,
                new SaveLibrary(SaveStore.DefaultFolder),
                () => world, Play, BackToMenu,
                (message, good) => _pause.ShowMessage(message, good));
            _pause = new Pause(font,
                () => _screenManager.Pop(),   // Resume
                _saveFlow.SaveFromPause,      // Save
                _saveFlow.OpenLoad,           // Load
                MenuFromPause,                // Main Menu
                QuitFromPause,                // Quit to Desktop
                ScreenWidth, ScreenHeight);

            // No World yet: New Game or Continue builds one (see Play).
            Debug.Pixel = _pixel;

            _console = BuildConsole(font);

            // create screens first
            _settingsScreen = new SettingsScreen(font, _settings.Values, _settings.Commit, DialogueApi.CheckHealthAsync,
                Debug.DevModeAllowed, () => _screenManager.Pop());

            //UI
            UiAtlas = TextureAtlas.FromXml(Content, Assets.UiAtlas);

            _menu = new Menu(
                font,
                _saveFlow.OpenLoad,                          // Continue: the save list ("No saves yet." if empty)
                NewGame,                                     // New Game
                () =>                                        // Settings
                {
                    _settingsScreen.Open(ScreenWidth, ScreenHeight);
                    _screenManager.Push(_settingsScreen);
                },
                Exit,                                        // Quit Game
                ScreenWidth, ScreenHeight);

            // start on the menu
            _screenManager.Push(_menu);
            base.LoadContent();
        }

        /// <summary>
        /// The dev console: its commands, then the screen. Built once and kept, so its output and
        /// history survive closing it. The world commands ask for the world each time they run,
        /// since Continue and `load` swap in a new one.
        /// </summary>
        private ConsoleScreen BuildConsole(SpriteFont font)
        {
            var saves = _saveFlow.Saves;
            var commands = new CommandRegistry();
            WorldCommands.Register(commands, () => world);
            SaveCommands.Register(commands,
                saves.List,
                () => (saves.TrySaveNew(world, out var message), message),
                save => (saves.TrySaveOver(world, save, out var message), message),
                _saveFlow.LoadFromConsole);
            BackendCommands.Register(commands, () => _settings.Values.backendUrl, _settings.SetBackendUrl,
                DialogueApi.CheckHealthAsync, line => _console.WriteLater(line));
            return new ConsoleScreen(font, commands, () => _screenManager.Pop(), ScreenWidth, ScreenHeight);
        }

        // ------------------------------------------------------------------ playing

        /// <summary>
        /// Starts playing <paramref name="next"/>: builds the screen that shows it and puts it on
        /// top of the main menu. New Game, Continue and the console's `load` all end here.
        /// </summary>
        private void Play(World next)
        {
            world = next;
            // Everything that happens shows in the console as it happens, open or not.
            world.Events.Posted += e => _console.Write(WorldCommands.Describe(e), LineKind.Event);
            _gameplayScreen = new GameplayScreen(world,
                OpenPause,
                // Built fresh each time, from the window as it is now, so the box fits after a resize.
                convo => _screenManager.Push(new ChatScreen(_font, convo,
                    () => _screenManager.Pop(),
                    ScreenWidth, ScreenHeight)));
            // Cell size before the first frame.
            _gameplayScreen.UpdateLayout(ScreenWidth, ScreenHeight);
            _screenManager.Push(_gameplayScreen);
        }

        // A fresh game: the player on Medows' spawn point, its starting NPCs spawned.
        private void NewGame()
        {
            var fresh = new World(new Location("Medows"));
            fresh.Init();
            Play(fresh);
        }

        private void OpenPause()
        {
            _pause.ClearMessage();
            _screenManager.Push(_pause);
        }

        // Saving is never automatic, so leaving with play since the last save asks first.
        private void QuitFromPause()
        {
            if (!world.HasUnsavedPlay) Exit();
            else Confirm("You've played since you last saved. Quit without saving?", "Quit", Exit);
        }

        private void MenuFromPause()
        {
            if (!world.HasUnsavedPlay) BackToMenu();
            else Confirm("You've played since you last saved. Leave without saving?", "Leave", BackToMenu);
        }

        // Closes every screen down to the main menu.
        private void BackToMenu()
        {
            while (_screenManager.Current != null && _screenManager.Current != _menu) _screenManager.Pop();
        }

        private void Confirm(string question, string yesText, Action onYes) =>
            ConfirmScreen.Ask(_screenManager, _font, question, yesText, onYes, ScreenWidth, ScreenHeight);

        // ------------------------------------------------------------------ the frame

        /// <summary>
        /// Updates the game's logic, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for game updates.
        /// </param>
        protected override void Update(GameTime gameTime)
        {
            HandleToggleKeys();
            _screenManager.Update(gameTime);
            base.Update(gameTime);
        }

        // The keys that open and close screens from anywhere: pause, and the dev console.
        private void HandleToggleKeys()
        {
            // Toggle the pause menu with the Back button (GamePad) or Escape key (Keyboard).
            var kb = Input.Keyboard;
            var padBack = GamePad.GetState(PlayerIndex.One).Buttons.Back;
            bool pausePressed = (kb.IsKeyDown(Keys.Escape) && !_prevKb.IsKeyDown(Keys.Escape))
                || (padBack == ButtonState.Pressed && _prevPadBack == ButtonState.Released);
            if (pausePressed)
            {
                if (_screenManager.Current == _pause)
                    _screenManager.Pop();
                else if (_screenManager.Current == _gameplayScreen)
                    OpenPause();
            }

            // The dev console, only over the world (not the menu, chat or pause) and only in dev mode.
            bool consolePressed = Array.Exists(ConsoleScreen.ToggleKeys, k => kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k));
            if (consolePressed && Debug.DEVMODE)
            {
                if (_screenManager.Current == _console)
                    _screenManager.Pop();
                else if (_screenManager.Current == _gameplayScreen)
                {
                    _console.Open(ScreenWidth, ScreenHeight);
                    _screenManager.Push(_console);
                }
            }
            _prevKb = kb;
            _prevPadBack = padBack;
        }

        /// <summary>
        /// Draws the game's graphics, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for rendering.
        /// </param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // ScreenManager begins/ends a batch per screen so each can supply its own
            // transform (the world uses its camera's).
            _screenManager.Draw(_spriteBatch, _pixel);
            base.Draw(gameTime);
        }
    }
}
