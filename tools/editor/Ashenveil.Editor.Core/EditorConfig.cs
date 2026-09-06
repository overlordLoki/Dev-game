using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ashenveil.Editor.Core
{
    /// <summary>
    /// The "project" — which Ashenveil game folder the editor is pointed at.
    /// Persisted next to the app so it is remembered between runs.
    /// </summary>
    public class EditorConfig
    {
        /// <summary>Root of the game repo (the folder containing the .sln).</summary>
        public string GameFolder { get; set; } = "";

        /// <summary>
        /// True when GameFolder was worked out from where the editor is installed
        /// rather than read from the config file. Not persisted - it's a fact about
        /// this run, not a setting.
        /// </summary>
        [JsonIgnore]
        public bool AutoDetected { get; set; }

        private static string ConfigPath =>
            Path.Combine(AppContext.BaseDirectory, "editor-config.json");

        public static EditorConfig Load()
        {
            EditorConfig config;
            try
            {
                config = File.Exists(ConfigPath)
                    ? JsonSerializer.Deserialize<EditorConfig>(File.ReadAllText(ConfigPath)) ?? new EditorConfig()
                    : new EditorConfig();
            }
            catch
            {
                // A corrupt config should never stop the app starting; fall back to empty.
                config = new EditorConfig();
            }

            // The editor ships inside the game repo, so if nothing valid is saved we
            // can just look. A saved folder still wins - someone who deliberately
            // pointed this at another checkout keeps it.
            if (!IsGameFolder(config.GameFolder))
            {
                string? found = Discover();
                if (found != null)
                {
                    config.GameFolder = found;
                    config.AutoDetected = true;
                }
            }

            return config;
        }

        /// <summary>
        /// Finds the game folder by walking up from the running exe.
        ///
        /// The editor lives at &lt;repo&gt;/tools/editor/*.Editor.App, and runs from somewhere
        /// below that again (bin/Debug/net10.0). Walking up and testing each parent means
        /// the nesting depth doesn't matter - Debug, Release, a published folder, or
        /// `dotnet run` all land on the same answer.
        /// </summary>
        public static string? Discover() => ResolveGameFolder(AppContext.BaseDirectory);

        public void Save()
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, opts));
        }

        /// <summary>True when GameFolder points at something that looks like the Ashenveil repo.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(GameFolder) && IsGameFolder(GameFolder);

        /// <summary>
        /// A folder counts as the game if it contains a core project - a subfolder with
        /// Content/Sprites in it. Cheap sanity check so a mis-picked folder fails loudly
        /// instead of writing levels into a random directory. The core project's name
        /// isn't assumed, so renaming the game doesn't break this.
        /// </summary>
        public static bool IsGameFolder(string path) => GameProject.FindCoreDir(path) != null;

        /// <summary>
        /// Returns the repo root for a folder picked anywhere inside the game, or null
        /// if it isn't in one. Picking Content or Ashenveil.Core is an easy slip in a file
        /// dialog, and the answer is unambiguous, so walk up rather than refusing.
        /// </summary>
        public static string? ResolveGameFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            for (var dir = new DirectoryInfo(path); dir != null; dir = dir.Parent)
                if (IsGameFolder(dir.FullName))
                    return dir.FullName;
            return null;
        }
    }
}
