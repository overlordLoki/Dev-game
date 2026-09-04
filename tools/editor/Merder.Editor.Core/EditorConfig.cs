using System;
using System.IO;
using System.Text.Json;

namespace Merder.Editor.Core
{
    /// <summary>
    /// The "project" — which merder game folder the editor is pointed at.
    /// Persisted next to the app so it is remembered between runs.
    /// </summary>
    public class EditorConfig
    {
        /// <summary>Root of the game repo (the folder containing merder.sln).</summary>
        public string GameFolder { get; set; } = "";

        private static string ConfigPath =>
            Path.Combine(AppContext.BaseDirectory, "editor-config.json");

        public static EditorConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<EditorConfig>(File.ReadAllText(ConfigPath))
                           ?? new EditorConfig();
            }
            catch
            {
                // A corrupt config should never stop the app starting; fall back to empty.
            }
            return new EditorConfig();
        }

        public void Save()
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, opts));
        }

        /// <summary>True when GameFolder points at something that looks like the merder repo.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(GameFolder) && IsGameFolder(GameFolder);

        /// <summary>
        /// A folder counts as the game if it has merder.Core/Content underneath it.
        /// Cheap sanity check so a mis-picked folder fails loudly instead of writing
        /// levels into a random directory.
        /// </summary>
        public static bool IsGameFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return Directory.Exists(Path.Combine(path, "merder.Core", "Content"));
        }
    }
}
