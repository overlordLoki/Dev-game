using System;
using System.IO;
using System.Text.Json;

namespace Ashenveil.Core.Config
{
    /// <summary>
    /// Reads and writes settings.json in one folder - %LOCALAPPDATA%\Ashenveil in the game, a
    /// temp folder in the tests. The install folder is never used: it's read-only content (and
    /// not writable at all on Android).
    ///
    /// Load never throws: a missing, unreadable or broken file gives the defaults, so a bad
    /// settings file can't stop the game starting. Save writes settings.json.tmp first and then
    /// swaps it in, so a crash halfway through a write leaves the old file whole. The save
    /// system is meant to reuse this folder and approach.
    /// </summary>
    public class SettingsStore
    {
        public const string FileName = "settings.json";

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            // It's a file people may edit by hand.
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>The folder the game keeps its own files in: %LOCALAPPDATA%\Ashenveil.</summary>
        public static string DefaultFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ashenveil");

        public string Folder { get; }
        public string FilePath => Path.Combine(Folder, FileName);

        public SettingsStore(string folder) => Folder = folder;

        /// <summary>
        /// The saved settings, or the defaults if there's no file or it can't be read. Keys the
        /// game doesn't know are ignored; missing ones keep their defaults; an older version is
        /// upgraded; a stored address that isn't valid is replaced by the default.
        /// </summary>
        public GameSettings Load()
        {
            GameSettings s;
            try
            {
                s = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath), Json);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                // FileNotFoundException and DirectoryNotFoundException are IOExceptions too.
                return new GameSettings();
            }
            if (s == null) return new GameSettings();   // the file said "null"

            Upgrade(s);
            s.backendUrl = BackendUrl.TryNormalize(s.backendUrl, out var url, out _) ? url : GameSettings.DefaultBackendUrl;
            return s;
        }

        /// <summary>
        /// Writes the settings, creating the folder if needed. Throws IOException (or
        /// UnauthorizedAccessException) if it can't - the caller decides how to say so.
        /// </summary>
        public void Save(GameSettings settings)
        {
            Directory.CreateDirectory(Folder);
            settings.version = GameSettings.CurrentVersion;

            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
            File.Move(tmp, FilePath, overwrite: true);   // a rename: the old file or the new, never half of one
        }

        // One step per version, oldest first. Version 1 is the first, so there's nothing to
        // convert yet (a file with no version key reads as 1, since every file was written with
        // one). A file from a newer game is read as best it can be, and saved back as this version.
        private static void Upgrade(GameSettings s)
        {
            // if (s.version < 2) { ...rename or convert a setting... }
            s.version = GameSettings.CurrentVersion;
        }
    }
}
