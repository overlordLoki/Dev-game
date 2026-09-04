using System;
using merder.Core.Localization;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using merder.Core.Entities;
using merder.Core.Screens;
using merder.Core.Tiles;
using merder.Core.Utility;
namespace merder.Core
{
    /// <summary>
    /// The main class for the game, responsible for managing game components, settings, 
    /// and platform-specific configurations.
    /// </summary>


    public class merderGame : Game
    {
        //UI fields
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        public World world;
        private Menu _menu;
        private SettingsScreen _settings;
        private ScreenManager _screenManager = new();
        // Resources for drawing.
        private GraphicsDeviceManager graphicsDeviceManager;

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
        public merderGame()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreferredBackBufferWidth  = 1280;
            graphicsDeviceManager.PreferredBackBufferHeight = 720;
            graphicsDeviceManager.ApplyChanges();
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
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
            Settings.UpdateScale(GraphicsDevice.Viewport.Height);
            this.world.gamebox = new Gamebox(_pixel, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            this.world.UpdateLayout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        /// <summary>
        /// Initializes the game, including setting up localization and adding the 
        /// initial screens to the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
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
            Settings.UpdateScale(GraphicsDevice.Viewport.Height);
            var h = GraphicsDevice.Viewport.Height;
            var w = GraphicsDevice.Viewport.Width;
            var font = Content.Load<SpriteFont>("Fonts/Hud");
            var pause = new Pause(font,
                () => _screenManager.Pop(),   // Resume
                () => { /* Quit — TODO */ },
                w, h);
            this.world = new World(new Gamebox(_pixel, w, h), new Player(new Vector2(100, 100),0), _pixel, ()=> _screenManager.Push(pause));
            this.world.UpdateLayout(w, h);   // set initial cell size before the first frame
            Debug.Pixel = _pixel;

            // create screens first
            _settings = new SettingsScreen(font, () => _screenManager.Pop(), w, h);

            _menu = new Menu(
                font,
                () => _screenManager.Push(world),      // New Game
                () => _screenManager.Push(_settings),  // Settings
                w, h);

            // start on the menu
            _screenManager.Push(_menu);
            base.LoadContent();
        }

        /// <summary>
        /// Updates the game's logic, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for game updates.
        /// </param>
        protected override void Update(GameTime gameTime)
        {
            // Exit the game if the Back button (GamePad) or Escape key (Keyboard) is pressed.
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

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
            // Clears the screen with the MonoGame orange color before drawing.
            GraphicsDevice.Clear(Color.MonoGameOrange);

            // TODO: Add your drawing code here
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();
            _screenManager.Draw(_spriteBatch, _pixel);
            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}