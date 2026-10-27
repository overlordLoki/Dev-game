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
using Ashenveil.Core.Saving;
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
        private SpriteFont _font;
        // The game being played. Null on the main menu until New Game or Continue builds one.
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
        // Every save, in %LOCALAPPDATA%\Ashenveil\saves. Only the pause menu's Save (and the console's `save`) writes to it.
        private SaveLibrary _saves;
        private SaveSlotsScreen _slots;
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
            _gameplayScreen?.UpdateLayout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
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
            _font = font;
            _saves = new SaveLibrary(SaveStore.DefaultFolder);
            _pause = new Pause(font,
                () => _screenManager.Pop(),   // Resume
                SaveFromPause,                // Save
                LoadFromPause,                // Load
                MenuFromPause,                // Main Menu
                QuitFromPause,                // Quit to Desktop
                w, h);
            // Opened from the main menu's Continue and the pause menu's Save and Load.
            _slots = new SaveSlotsScreen(font, SaveToNewSlot, SlotPicked, SlotDeleted, () => _screenManager.Pop());

            // No World yet: New Game or Continue builds one (see Play).
            Debug.Pixel = _pixel;

            // The dev console: its commands, then the screen. Built once and kept, so its
            // output and history survive closing it. The world commands ask for the world each
            // time they run, since Continue and `load` swap in a new one.
            var commands = new CommandRegistry();
            WorldCommands.Register(commands, () => world);
            SaveCommands.Register(commands,
                _saves.List,
                () => (_saves.TrySaveNew(world, out var message), message),
                save => (_saves.TrySaveOver(world, save, out var message), message),
                LoadFromConsole);
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
                _saves.HasSaves ? Continue : null,           // Continue (pick a save), only if there are any
                NewGame,                                     // New Game
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

        // ------------------------------------------------------------------ playing and saving

        /// <summary>
        /// Starts playing <paramref name="next"/>: builds the screen that shows it and puts it on
        /// top of the main menu. New Game, Continue and the console's `load` all end here.
        /// </summary>
        private void Play(World next)
        {
            world = next;
            _gameplayScreen = new GameplayScreen(world,
                OpenPause,
                // Built fresh each time, from the window as it is now, so the box fits after a resize.
                convo => _screenManager.Push(new ChatScreen(_font, convo,
                    () => _screenManager.Pop(),
                    GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height)));
            // Cell size before the first frame.
            _gameplayScreen.UpdateLayout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
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

        // Saving is never automatic, so quitting with play since the last save asks first.
        private void QuitFromPause()
        {
            if (!world.HasUnsavedPlay) Exit();
            else Confirm("You've played since you last saved. Quit without saving?", "Quit", Exit);
        }

        /// <summary>
        /// Asks a yes/no question over whatever's on top. The no button just closes the
        /// question; the yes button closes it, then does <paramref name="onYes"/>.
        /// </summary>
        private void Confirm(string question, string yesText, Action onYes)
        {
            _screenManager.Push(new ConfirmScreen(_font, question,
                yesText, () => { _screenManager.Pop(); onYes(); },
                "Cancel", () => _screenManager.Pop(),
                GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        }

        // ------------------------------------------------------------------ the save slots screen

        // The main menu's Continue, and the pause menu's Load: pick a save to carry on.
        private void Continue() => OpenSlots(SaveSlotsScreen.Purpose.Load);
        private void LoadFromPause() => OpenSlots(SaveSlotsScreen.Purpose.Load);

        private void MenuFromPause()
        {
            if (!world.HasUnsavedPlay) BackToMenu();
            else Confirm("You've played since you last saved. Leave without saving?", "Leave", BackToMenu);
        }
        private void BackToMenu()
        {
            while (_screenManager.Current != null && _screenManager.Current != _menu) _screenManager.Pop();
        }
        // The pause menu's Save. Checked before the list opens, so the player is never shown
        // saves they can't save over.
        private void SaveFromPause()
        {
            if (_saves.CanSave(world, out var why)) OpenSlots(SaveSlotsScreen.Purpose.Save);
            else _pause.ShowMessage(why, good: false);
        }

        private void OpenSlots(SaveSlotsScreen.Purpose purpose)
        {
            _slots.Open(purpose, _saves.List(), GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _screenManager.Push(_slots);
        }

        // "+ New save": save, then back to the pause menu saying so.
        private void SaveToNewSlot()
        {
            if (_saves.TrySaveNew(world, out var message)) Saved(message);
            else _slots.ShowMessage(message, good: false);
        }

        // A save was picked: overwrite it, or carry it on - each asking first.
        private void SlotPicked(SaveSummary save)
        {
            if (_slots.OpenFor == SaveSlotsScreen.Purpose.Save)
            {
                Confirm("Overwrite this save?", "Overwrite", () =>
                {
                    if (_saves.TrySaveOver(world, save, out var message)) Saved(message);
                    else
                    {
                        _slots.Show(_saves.List());   // it may have been damaged, or gone
                        _slots.ShowMessage(message, good: false);
                    }
                });
            }
            // Loading throws away everything since the last save, so with unsaved play it asks
            // first, as quitting does. From the main menu there's no game to lose.
            else if (world != null && world.HasUnsavedPlay)
                Confirm("You've played since you last saved. Load this save and lose that?", "Load", () => LoadSlot(save));
            else
                LoadSlot(save);
        }

        private void SlotDeleted(SaveSummary save)
        {
            Confirm("Delete this save? This can't be undone.", "Delete", () =>
            {
                bool deleted = _saves.TryDelete(save, out var problem);
                _slots.Show(_saves.List());
                _slots.ShowMessage(deleted ? "Save deleted." : problem, deleted);
            });
        }

        // Saved from the slots screen: close it and say so on the pause menu underneath.
        private void Saved(string message)
        {
            _screenManager.Pop();
            _pause.ShowMessage(message, good: true);
        }

        // Carries on the chosen save, or stays on the list saying why it can't.
        private void LoadSlot(SaveSummary save)
        {
            if (!LoadSave(save, out var problem)) _slots.ShowMessage(problem, good: false);
        }

        /// <summary>
        /// Throws away the game being played (if any) and carries on the saved one: closes every
        /// screen down to the main menu (the game, and the pause menu, list or console over it),
        /// then plays the loaded world. False, with the reason, if the save can't be loaded -
        /// then nothing is closed and everything carries on as it was.
        /// </summary>
        private bool LoadSave(SaveSummary save, out string problem)
        {
            if (!_saves.TryLoad(save, out var loaded, out problem)) return false;
            while (_screenManager.Current != null && _screenManager.Current != _menu) _screenManager.Pop();
            Play(loaded);
            return true;
        }

        // The console's `load <n>`: no question asked, it's a dev tool. It closes the console
        // with everything else, so its answer is there next time the console opens.
        private (bool ok, string message) LoadFromConsole(SaveSummary save) =>
            LoadSave(save, out var problem) ? (true, "Loaded the save.") : (false, problem);

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