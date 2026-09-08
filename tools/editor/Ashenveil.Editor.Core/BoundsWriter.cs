using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ashenveil.Editor.Core
{
    /// <summary>
    /// Reads and writes the game's collision boxes, which live as plain data in
    /// Content/bounds.json rather than in source. This replaced a timid source-patcher:
    /// once a shape can be any number of boxes, a data file is both simpler and safer
    /// than rewriting C# - there is no line to re-verify and nothing to restructure.
    ///
    /// The file is one object keyed by class name:
    ///
    ///   { "TreeSmall": { "basis": "cell", "boxes": [ {"x":..,"y":..,"w":..,"h":..} ] } }
    ///
    /// Read-modify-write preserves any entry (and any key) this editor doesn't touch, so
    /// hand-added notes or classes the scanner doesn't know about survive a save.
    /// </summary>
    public static class BoundsFile
    {
        /// <summary>The whole file as a mutable tree, or an empty object when absent.</summary>
        public static JsonObject Read(GameProject project)
        {
            if (!File.Exists(project.BoundsPath)) return new JsonObject();
            try
            {
                return JsonNode.Parse(File.ReadAllText(project.BoundsPath)) as JsonObject
                       ?? new JsonObject();
            }
            catch (JsonException)
            {
                return new JsonObject();   // corrupt file: start clean rather than crash the editor
            }
        }

        /// <summary>The boxes declared for one class, in file order.</summary>
        public static List<BoxDto> BoxesFor(JsonObject root, string id)
        {
            var boxes = new List<BoxDto>();
            if (root[id] is JsonObject entry && entry["boxes"] is JsonArray arr)
                foreach (var node in arr)
                    boxes.Add(new BoxDto
                    {
                        X = F(node, "x"),
                        Y = F(node, "y"),
                        W = F(node, "w"),
                        H = F(node, "h"),
                    });
            return boxes;
        }

        /// <summary>
        /// Replaces one class's boxes and writes the file back. The write goes to a temp
        /// file first and is then moved into place, matching how levels are saved.
        /// </summary>
        public static void Save(GameProject project, string id, string basis, List<BoxDto> boxes)
        {
            var root = Read(project);

            var arr = new JsonArray();
            foreach (var b in boxes)
                arr.Add(new JsonObject
                {
                    ["x"] = Round(b.X),
                    ["y"] = Round(b.Y),
                    ["w"] = Round(b.W),
                    ["h"] = Round(b.H),
                });

            root[id] = new JsonObject { ["basis"] = basis, ["boxes"] = arr };

            Directory.CreateDirectory(Path.GetDirectoryName(project.BoundsPath)!);
            var opts = new JsonSerializerOptions { WriteIndented = true };

            string tmp = project.BoundsPath + ".tmp";
            File.WriteAllText(tmp, root.ToJsonString(opts));
            File.Move(tmp, project.BoundsPath, overwrite: true);
        }

        private static float F(JsonNode? node, string key)
        {
            var v = node?[key];
            return v != null ? (float)v.GetValue<double>() : 0f;
        }

        // Two decimals is plenty for a fraction and keeps the file readable.
        private static double Round(float value) => System.Math.Round(value, 2);
    }
}
