namespace Ashenveil.Core.Config
{
    /// <summary>
    /// What the player (or developer) has chosen, as saved in settings.json. Plain data like the
    /// level DTOs: property names are the JSON keys, and the initial values are the defaults, so a
    /// key missing from the file keeps its default. Loading and saving is SettingsStore's job;
    /// putting the values into effect is AshenveilGame.ApplySettings.
    ///
    /// Belongs to the machine, not to a playthrough, so it's a separate file from save games.
    /// </summary>
    public class GameSettings
    {
        /// <summary>
        /// Bumped when a setting is renamed or changes meaning, so SettingsStore can upgrade an
        /// older file. Adding a setting doesn't need a bump: a missing key just gets its default.
        /// </summary>
        public const int CurrentVersion = 1;

        public const string DefaultBackendUrl = "http://127.0.0.1:8000";

        public int version { get; set; } = CurrentVersion;

        /// <summary>The Python backend (TestAPI/main.py), without a trailing slash.</summary>
        public string backendUrl { get; set; } = DefaultBackendUrl;

        /// <summary>
        /// Dev-only tools, like the ` console. Only takes effect where dev mode is allowed
        /// (Debug builds, or a Release build started with --dev): see Debug.DevModeAllowed.
        /// </summary>
#if DEBUG
        public bool devMode { get; set; } = true;
#else
        public bool devMode { get; set; } = false;
#endif

        public GameSettings Clone() => (GameSettings)MemberwiseClone();
    }
}
