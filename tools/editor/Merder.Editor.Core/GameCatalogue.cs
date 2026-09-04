using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Merder.Editor.Core
{
    /// <summary>A tile the game knows how to draw, ready to paint with.</summary>
    public class TileEntry
    {
        /// <summary>The id written into the level JSON ("grass").</summary>
        public string Id { get; set; } = "";
        /// <summary>Human label for the palette ("Dirt Road").</summary>
        public string Name { get; set; } = "";
        /// <summary>Content path from Assets.cs ("Sprites/Tiles/grass"), or "" if unmapped.</summary>
        public string AssetPath { get; set; } = "";
        /// <summary>Loadable image url, or "" when the PNG is missing.</summary>
        public string Url { get; set; } = "";
    }

    public class VarietyEntry
    {
        public int Value { get; set; }
        public string Label { get; set; } = "";
        public string AssetPath { get; set; } = "";
        public string Url { get; set; } = "";
    }

    /// <summary>An object type the game can place ("tree"), with its varieties.</summary>
    public class ObjectEntry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<VarietyEntry> Varieties { get; set; } = new();
    }

    public class Catalogue
    {
        public List<TileEntry> Tiles { get; set; } = new();
        public List<ObjectEntry> Objects { get; set; } = new();
        /// <summary>Anything that didn't line up, surfaced in the UI rather than swallowed.</summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Builds the editor's palette by reading the game's own source, so the editor
    /// never carries a second copy of "what tiles exist". Adding a tile to the game
    /// makes it appear here with no editor change.
    ///
    /// Three files are read, and they chain together:
    ///   MapLoader.ParseTile   "grass"          -> TileType.Grass
    ///   TileMap._assetNames   TileType.Grass   -> Assets.Grass
    ///   Assets.cs             Assets.Grass     -> "Sprites/Tiles/grass"
    ///
    /// This is regex over source, not a compiler. It is deliberately forgiving: a
    /// line it cannot read is reported as a warning and skipped, never thrown. If
    /// the game's formatting drifts far enough that parsing breaks, the editor
    /// still opens and says why the palette is short.
    /// </summary>
    public static class GameCatalogue
    {
        public static Catalogue Build(GameProject project)
        {
            var catalogue = new Catalogue();

            var assets = ParseAssets(project, catalogue.Warnings);
            BuildTiles(project, assets, catalogue);
            BuildObjects(project, assets, catalogue);

            return catalogue;
        }

        // ---------------------------------------------------------------- Assets.cs

        /// <summary>One constant from Assets.cs.</summary>
        private record AssetConst(string Name, string Path, string Section);

        private static readonly Regex ConstLine =
            new(@"public\s+const\s+string\s+(\w+)\s*=\s*""([^""]+)""", RegexOptions.Compiled);

        /// <summary>
        /// Reads Assets.cs into constant-name -> (content path, section). "Section" comes
        /// from the //Tiles and //Objects comments, which is what tells the editor whether
        /// a sprite is something you paint or something you place.
        /// </summary>
        private static Dictionary<string, AssetConst> ParseAssets(GameProject project, List<string> warnings)
        {
            var result = new Dictionary<string, AssetConst>(StringComparer.Ordinal);

            string path = Path.Combine(project.ContentDir, "Assets.cs");
            if (!File.Exists(path))
            {
                warnings.Add("Assets.cs not found - the palette will be empty.");
                return result;
            }

            string section = "";
            foreach (string line in File.ReadLines(path))
            {
                string trimmed = line.Trim();

                // A comment line starting a group: "//Tiles", "// Objects".
                if (trimmed.StartsWith("//"))
                {
                    string label = trimmed.TrimStart('/').Trim();
                    if (label.Equals("Tiles", StringComparison.OrdinalIgnoreCase)) section = "tiles";
                    else if (label.Equals("Objects", StringComparison.OrdinalIgnoreCase)) section = "objects";
                    continue;
                }

                var match = ConstLine.Match(line);
                if (match.Success)
                {
                    string name = match.Groups[1].Value;
                    result[name] = new AssetConst(name, match.Groups[2].Value, section);
                }
            }

            return result;
        }

        // ------------------------------------------------------------------- tiles

        private static readonly Regex ParseTileArm =
            new(@"""(\w+)""\s*=>\s*TileType\.(\w+)", RegexOptions.Compiled);

        private static readonly Regex AssetNameEntry =
            new(@"\[TileType\.(\w+)\]\s*=\s*Assets\.(\w+)", RegexOptions.Compiled);

        private static void BuildTiles(GameProject project, Dictionary<string, AssetConst> assets, Catalogue catalogue)
        {
            // json id -> TileType name, in the order MapLoader lists them.
            var idToType = ReadPairs(project.MapLoaderPath, ParseTileArm, catalogue.Warnings, "MapLoader.cs");

            // TileType name -> Assets constant name.
            var typeToAsset = ReadPairs(project.TileMapPath, AssetNameEntry, catalogue.Warnings, "TileMap.cs")
                .GroupBy(p => p.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);

            foreach (var (id, typeName) in idToType)
            {
                // Duplicates would show the same tile twice in the palette. (ParseTile's
                // "_ => TileType.Grass" fallback arm has no string literal, so the regex
                // skips it already.)
                if (catalogue.Tiles.Any(t => t.Id == id)) continue;

                var entry = new TileEntry { Id = id, Name = Humanise(id) };

                if (typeToAsset.TryGetValue(typeName, out string? constName) &&
                    assets.TryGetValue(constName, out var asset))
                {
                    entry.AssetPath = asset.Path;
                    entry.Url = SpriteUrl(project, asset.Path, catalogue.Warnings);
                }
                else
                {
                    catalogue.Warnings.Add($"Tile '{id}' (TileType.{typeName}) has no art mapped in TileMap/Assets.");
                }

                catalogue.Tiles.Add(entry);
            }
        }

        // ----------------------------------------------------------------- objects

        /// <summary>
        /// Object art is grouped by the constant's name: Tree_Small / Tree_Medium /
        /// Tree_Large become one "tree" type with three varieties, numbered in the
        /// order they are declared in Assets.cs - which is the order Tree's own
        /// variety switch uses. A constant with no underscore becomes a single-variety
        /// type of its own.
        /// </summary>
        private static void BuildObjects(GameProject project, Dictionary<string, AssetConst> assets, Catalogue catalogue)
        {
            foreach (var asset in assets.Values.Where(a => a.Section == "objects"))
            {
                int underscore = asset.Name.IndexOf('_');
                string typeName = underscore < 0 ? asset.Name : asset.Name[..underscore];
                string label = underscore < 0 ? "Default" : asset.Name[(underscore + 1)..].Replace('_', ' ');

                string id = typeName.ToLowerInvariant();
                var entry = catalogue.Objects.FirstOrDefault(o => o.Id == id);
                if (entry == null)
                {
                    entry = new ObjectEntry { Id = id, Name = Humanise(id) };
                    catalogue.Objects.Add(entry);
                }

                entry.Varieties.Add(new VarietyEntry
                {
                    Value     = entry.Varieties.Count + 1,
                    Label     = label,
                    AssetPath = asset.Path,
                    Url       = SpriteUrl(project, asset.Path, catalogue.Warnings),
                });
            }
        }

        // ------------------------------------------------------------------ shared

        /// <summary>
        /// Runs a two-group regex over a file and returns every match in order.
        /// A missing file is a warning, not a crash - the rest of the palette still builds.
        /// </summary>
        private static List<KeyValuePair<string, string>> ReadPairs(
            string path, Regex pattern, List<string> warnings, string label)
        {
            var pairs = new List<KeyValuePair<string, string>>();

            if (!File.Exists(path))
            {
                warnings.Add($"{label} not found - some palette entries will be missing.");
                return pairs;
            }

            foreach (Match match in pattern.Matches(File.ReadAllText(path)))
                pairs.Add(new KeyValuePair<string, string>(match.Groups[1].Value, match.Groups[2].Value));

            if (pairs.Count == 0)
                warnings.Add($"Nothing recognised in {label} - has its formatting changed?");

            return pairs;
        }

        /// <summary>
        /// Turns a content path ("Sprites/Tiles/grass") into a url the web view can load,
        /// checking the PNG is actually there so a missing file shows as a warning rather
        /// than a silently broken image.
        /// </summary>
        private static string SpriteUrl(GameProject project, string assetPath, List<string> warnings)
        {
            const string prefix = "Sprites/";
            string relative = assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? assetPath[prefix.Length..]
                : assetPath;

            string file = Path.Combine(project.SpritesDir,
                relative.Replace('/', Path.DirectorySeparatorChar) + ".png");

            if (!File.Exists(file))
            {
                warnings.Add($"Missing art: {assetPath}.png");
                return "";
            }

            return "sprite:///" + relative + ".png";
        }

        /// <summary>"dirt_road" -> "Dirt Road", for palette labels.</summary>
        private static string Humanise(string id) =>
            string.Join(" ", id.Split('_', StringSplitOptions.RemoveEmptyEntries)
                               .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
