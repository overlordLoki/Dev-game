using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ashenveil.Editor.Core
{
    /// <summary>
    /// Reads and writes level files in the game's Content/levels folder.
    ///
    /// Levels are stored as the raw JSON the editor produced, not as a re-serialised
    /// C# object: the UI owns the schema, and round-tripping through a DTO here would
    /// silently drop any field the UI adds before this class learns about it. What
    /// this class does guarantee is that the text is valid JSON with the keys the
    /// game loader needs.
    /// </summary>
    public class LevelStore
    {
        private readonly GameProject _project;

        public LevelStore(GameProject project) => _project = project;

        public List<string> List()
        {
            if (!Directory.Exists(_project.LevelsDir)) return new List<string>();
            return Directory.EnumerateFiles(_project.LevelsDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }

        public bool Exists(string name) => File.Exists(_project.LevelPath(Safe(name)));

        public string Read(string name)
        {
            string path = _project.LevelPath(Safe(name));
            if (!File.Exists(path)) throw new FileNotFoundException($"No level named '{name}'.", path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Writes a level, creating the levels folder if needed. The write goes to a
        /// temp file first and is then moved into place, so a crash mid-write can't
        /// leave a half-written level behind.
        /// </summary>
        public void Write(string name, string json)
        {
            Validate(json);

            string path = _project.LevelPath(Safe(name));
            Directory.CreateDirectory(_project.LevelsDir);

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }

        public void Delete(string name)
        {
            string path = _project.LevelPath(Safe(name));
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Rejects anything the game loader would choke on.</summary>
        private static void Validate(string json)
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Level is not valid JSON: " + ex.Message);
            }

            if (node is not JsonObject obj)
                throw new InvalidDataException("Level must be a JSON object.");

            foreach (string required in new[] { "cols", "rows", "tiles" })
                if (!obj.ContainsKey(required))
                    throw new InvalidDataException($"Level is missing required field '{required}'.");
        }

        /// <summary>
        /// Keeps a level name to a bare filename. Guards against a name like
        /// "../../something" writing outside the levels folder.
        /// </summary>
        private static string Safe(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Level name cannot be empty.", nameof(name));

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains(".."))
                throw new ArgumentException($"Invalid level name '{name}'.", nameof(name));

            return name;
        }
    }
}
