using System;
using Ashenveil.Core.Localization;
using System.Collections.Generic;
using System.Globalization;
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
namespace Ashenveil.Core
{
    /// <summary>
    /// The main class for the game, responsible for managing game components, settings, 
    /// and platform-specific configurations.
    /// </summary>


    public class AshenveilGame : Game
    {
        //UI fields
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        public World world;
        private GameplayScreen _gameplayScreen;
        private Menu _menu;
        private SettingsScreen _settings;
        private Pause _pause;
        private ConsoleScreen _console;
        private ScreenManager _screenManager = new();
        private KeyboardState _prevKb;
        private ButtonState _prevPadBack = ButtonState.Released;
        // The chosen settings: loaded first thing in Initialize, changed by the settings
        // screen and the console's `server` command, saved by CommitSettings.
        private SettingsStore _settingsStore;
        private GameSettings _config;
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
        /// Initializes a new instance of the game. Configures platform-specific settings, 
        /// initializes services like settings and leaderboard managers, and sets up the 
        /// screen manager for screen transitions.
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
        private void OnResize(object sender, EventArgs e)
        {
            if (_pixel == null) return;
            UiScale.Update(GraphicsDevice.Viewport.Height);
            // WorldBounds recomputes itself from Layout, so a resize only needs the layout
            // refreshed - no bounds object to rebuild anymore.
            _gameplayScreen.UpdateLayout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        /// <summary>
        /// Initializes the game, including setting up localization and adding the 
        /// initial screens to the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            // Settings before base.Initialize, which runs LoadContent: everything built there
            // should see the saved values, not the defaults.
            _settingsStore = new SettingsStore(SettingsStore.DefaultFolder);
            _config = _settingsStore.Load();
            ApplySettings();

            base.Initialize();

            // Load supported languages and set the default language.
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();
            var languages = new List<CultureInfo>();
            for (int i = 0; i < cultures.Count; i++)
            {
                languages.Add(cultures[i]);
            }

            // TODO You should load this from a settings file or similar,
            // based on what the user or operating system selected.
            var selectedLanguage = LocalizationManager.DEFAULT_CULTURE_CODE;
            LocalizationManager.SetCulture(selectedLanguage);


            //init game logic and Entites

        }

        /// <summary>
        /// Loads game content, such as textures and particle systems.
        /// </summary>
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData([Color.White]);
            Textures.Init(Content);   // must run before anything calls Textures.Get
            UiScale.Update(GraphicsDevice.Viewport.Height);
            var h = GraphicsDevice.Viewport.Height;
            var w = GraphicsDevice.Viewport.Width;
            var font = Content.Load<SpriteFont>("Fonts/Hud");
            font.DefaultCharacter = '?';   // draw '?' for a character the font lacks, instead of throwing
            _pause = new Pause(font,
                () => _screenManager.Pop(),   // Resume
                Exit,                         // Quit to Desktop
                w, h);

            // The game itself, then the screen that shows and plays it.
            this.world = new World(new Location("Medows"));
            _gameplayScreen = new GameplayScreen(world,
                () => _screenManager.Push(_pause),
                // Built fresh each time, from the window as it is now, so the box fits after a resize.
                convo => _screenManager.Push(new ChatScreen(font, convo,
                    () => _screenManager.Pop(),
                    GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height)));
            _gameplayScreen.UpdateLayout(w, h);   // set initial cell size before the first frame
            this.world.Init();  // spawn the player and set up the level on first load
            Debug.Pixel = _pixel;

            // The dev console: its commands, then the screen. Built once and kept, so its
            // output and history survive closing it.
            var commands = new CommandRegistry();
            WorldCommands.Register(commands, world);
            BackendCommands.Register(commands, () => _config.backendUrl, SetBackendUrl,
                DialogueApi.CheckHealthAsync, line => _console.WriteLater(line));
            _console = new ConsoleScreen(font, commands, () => _screenManager.Pop(), w, h);

            // create screens first
            _settings = new SettingsScreen(font, _config, CommitSettings, DialogueApi.CheckHealthAsync,
                Debug.DevModeAllowed, () => _screenManager.Pop());

            //UI
            UiAtlas = TextureAtlas.FromXml(Content, Assets.UiAtlas);

            _menu = new Menu(
                font,
                () => _screenManager.Push(_gameplayScreen),  // New Game
                () =>                                        // Settings
                {
                    _settings.Open(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                    _screenManager.Push(_settings);
                },
                w, h);

            // start on the menu
            _screenManager.Push(_menu);
            base.LoadContent();
        }

        /// <summary>
        /// Puts the settings into effect. Runs at startup and after every change, so each setting
        /// has exactly one place that makes it happen.
        /// </summary>
        private void ApplySettings()
        {
            DialogueApi.BaseUrl = _config.backendUrl;
            Debug.DEVMODE = _config.devMode && Debug.DevModeAllowed;
        }

        /// <summary>
        /// Applies then saves the settings. Applied even if the save fails, so the change still
        /// works this session; the IOException is thrown on for the caller to report.
        /// </summary>
        private void CommitSettings()
        {
            ApplySettings();
            _settingsStore.Save(_config);
        }

        // The console's `server <url>`: the URL is already normalised by BackendCommands.
        private void SetBackendUrl(string url)
        {
            _config.backendUrl = url;
            CommitSettings();
        }

        /// <summary>
        /// Updates the game's logic, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for game updates.
        /// </param>
        protected override void Update(GameTime gameTime)
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
                    _screenManager.Push(_pause);
            }

            // The dev console, only over the world (not the menu, chat or pause) and only in dev mode.
            bool consolePressed = Array.Exists(ConsoleScreen.ToggleKeys, k => kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k));
            if (consolePressed && Debug.DEVMODE)
            {
                if (_screenManager.Current == _console)
                    _screenManager.Pop();
                else if (_screenManager.Current == _gameplayScreen)
                {
                    _console.Open(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                    _screenManager.Push(_console);
                }
            }
            _prevKb = kb;
            _prevPadBack = padBack;

            _screenManager.Update(gameTime);
            base.Update(gameTime);
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