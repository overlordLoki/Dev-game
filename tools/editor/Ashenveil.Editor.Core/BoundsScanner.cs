using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Ashenveil.Editor.Core
{
    /// <summary>One collision box, as fractions of the target's base rectangle.</summary>
    public class BoxDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }
    }

    /// <summary>One thing with a collision shape you can tune.</summary>
    public class BoundsTarget
    {
        /// <summary>Class name - TreeSmall, Player. The key into bounds.json.</summary>
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>"object" or "entity" - only affects how it's grouped in the UI.</summary>
        public string Kind { get; set; } = "";
        /// <summary>Path to the data file the boxes live in, for display.</summary>
        public string File { get; set; } = "";

        /// <summary>
        /// What the fractions are measured against. "cell" for objects, which are sized
        /// by the grid; "sprite" for entities, which are sized by their own Width/Height.
        /// The preview has to honour this or the box it draws is a lie.
        /// </summary>
        public string Basis { get; set; } = "";

        /// <summary>
        /// How big the art is drawn relative to a cell: objects at 1.5, entities at 0.9.
        /// Taken from the game's own draw code so the preview matches what you see in game.
        /// </summary>
        public float SpriteScale { get; set; } = 1f;

        /// <summary>Art to preview behind the boxes, or "" when the class has no fixed sprite.</summary>
        public string SpriteUrl { get; set; } = "";
        /// <summary>Frames across, when the art is a sprite sheet (1 for a plain image).</summary>
        public int SpriteFrames { get; set; } = 1;

        /// <summary>The boxes currently declared for this class, in file order.</summary>
        public List<BoxDto> Boxes { get; set; } = new();
    }

    public class BoundsCatalogue
    {
        public List<BoundsTarget> Targets { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Lists everything in the game that can carry a collision shape and attaches the
    /// boxes each one currently has. The list of things comes from the game's source -
    /// the concrete object classes under Objects/ and the entity classes under Entities/ -
    /// so a new class shows up here with no editor change. The boxes themselves come from
    /// Content/bounds.json (see <see cref="BoundsFile"/>); a class with no entry yet is
    /// still listed, just with an empty shape waiting to be drawn.
    ///
    /// This is regex over source, not a compiler: it reads class names and which sprite a
    /// class draws, and is deliberately forgiving - a file it can't read is skipped.
    /// </summary>
    public static class BoundsScanner
    {
        private static readonly Regex ClassDecl = new(
            @"\bclass\s+(\w+)\s*(?::\s*([\w\s,]+))?", RegexOptions.Compiled);

        // : base(col, row, Assets.Tree_Small, rotate)
        private static readonly Regex BaseAsset = new(
            @"base\s*\([^)]*Assets\.(\w+)", RegexOptions.Compiled);

        // Any Assets.X mention, for classes that don't chain to a base constructor.
        private static readonly Regex AnyAsset = new(
            @"Assets\.(\w+)", RegexOptions.Compiled);

        // public static readonly SpriteSheetInfo PLAYERIDLE = new("Sprites/Player/D_Idle", 4, 32);
        private static readonly Regex SheetDecl = new(
            @"SpriteSheetInfo\s+(\w+)\s*=\s*new\s*\(\s*""([^""]+)""\s*,\s*(\d+)\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled);

        public static BoundsCatalogue Build(GameProject project)
        {
            var catalogue = new BoundsCatalogue();
            var assets = GameCatalogue.ParseAssets(project, catalogue.Warnings);
            var boxes = BoundsFile.Read(project);

            ScanObjects(project, assets, boxes, catalogue);
            ScanEntities(project, assets, boxes, catalogue);

            if (catalogue.Targets.Count == 0)
                catalogue.Warnings.Add("No object or entity classes found - has the source layout changed?");

            return catalogue;
        }

        // ------------------------------------------------------------------ objects

        private static void ScanObjects(GameProject project,
            Dictionary<string, GameCatalogue.AssetConst> assets, JsonObject boxes, BoundsCatalogue catalogue)
        {
            string dir = Path.Combine(project.CoreDir, "Objects");
            if (!Directory.Exists(dir))
            {
                catalogue.Warnings.Add("No Objects folder found.");
                return;
            }

            // Recursive: object families live in their own folders (Objects/Tree,
            // Objects/Bush), so a top-level-only scan finds nothing at all.
            foreach (string file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).OrderBy(f => f))
            {
                string text = System.IO.File.ReadAllText(file);
                string className = Path.GetFileNameWithoutExtension(file);

                // Concrete classes only; skip interfaces and the abstract bases (GameObject,
                // Tree, Bush) - those are shared code, not placeable things.
                if (!Regex.IsMatch(text, @"\bclass\s+\w+")) continue;    // interface/enum file
                if (text.Contains("abstract class")) continue;
                if (!ClassDecl.IsMatch(text)) continue;

                var target = new BoundsTarget
                {
                    Id          = className,
                    Name        = Humanise(className),
                    Kind        = "object",
                    File        = Relative(project, project.BoundsPath),
                    Basis       = "cell",
                    SpriteScale = 1.5f,     // GameObject.Draw: size = Layout.CellSize * 1.5f
                    Boxes       = BoundsFile.BoxesFor(boxes, className),
                };

                string? constName = FindAssetConst(text, className, assets);
                if (constName != null)
                    target.SpriteUrl = GameCatalogue.SpriteUrl(project, assets[constName].Path, catalogue.Warnings);
                else
                    catalogue.Warnings.Add(
                        $"{className}: couldn't tell which sprite it uses - the boxes are still " +
                        "tunable, just without a picture behind them.");

                catalogue.Targets.Add(target);
            }

            if (!catalogue.Targets.Any(t => t.Kind == "object"))
                catalogue.Warnings.Add("No concrete object classes found under Objects/.");
        }

        /// <summary>
        /// Works out which sprite a class draws, in decreasing order of confidence:
        /// the constant it passes to its base constructor, any Assets constant it names
        /// at all, then a constant matching its own class name. The last one covers
        /// classes that take their asset as a constructor parameter (Well), where the
        /// source simply doesn't say which art it ends up with.
        /// </summary>
        private static string? FindAssetConst(string text, string className,
            Dictionary<string, GameCatalogue.AssetConst> assets)
        {
            var baseMatch = BaseAsset.Match(text);
            if (baseMatch.Success && assets.ContainsKey(baseMatch.Groups[1].Value))
                return baseMatch.Groups[1].Value;

            foreach (Match match in AnyAsset.Matches(text))
                if (assets.ContainsKey(match.Groups[1].Value))
                    return match.Groups[1].Value;

            // Well -> Assets.Well, Bush_Small -> a class called BushSmall.
            string flattened = className.Replace("_", "");
            foreach (var name in assets.Keys)
                if (string.Equals(name.Replace("_", ""), flattened, StringComparison.OrdinalIgnoreCase))
                    return name;

            return null;
        }

        // ----------------------------------------------------------------- entities

        private static void ScanEntities(GameProject project,
            Dictionary<string, GameCatalogue.AssetConst> assets, JsonObject boxes, BoundsCatalogue catalogue)
        {
            string dir = Path.Combine(project.CoreDir, "Entities");
            if (!Directory.Exists(dir))
            {
                catalogue.Warnings.Add("No Entities folder found.");
                return;
            }

            foreach (string file in Directory.EnumerateFiles(dir, "*.cs").OrderBy(f => f))
            {
                string className = Path.GetFileNameWithoutExtension(file);
                string text = System.IO.File.ReadAllText(file);

                // Skip interface files by their content, not the filename's first letter -
                // an entity called "Imp" or "Ice" must not be mistaken for an interface.
                if (!Regex.IsMatch(text, @"\bclass\s+\w+")) continue;
                if (text.Contains("abstract class")) continue;

                catalogue.Targets.Add(new BoundsTarget
                {
                    Id           = className,
                    Name         = Humanise(className),
                    Kind         = "entity",
                    File         = Relative(project, project.BoundsPath),
                    Basis        = "sprite",
                    SpriteScale  = 0.9f,     // Player/NPC: Width = Height = CellSize * 0.9f
                    SpriteUrl    = EntitySprite(project, className, catalogue, out int frames),
                    SpriteFrames = frames,
                    Boxes        = BoundsFile.BoxesFor(boxes, className),
                });
            }
        }

        /// <summary>
        /// Entities are drawn from animation sheets, not single images. Finds the sheet
        /// whose name looks like this entity's and reports its frame count so the preview
        /// can show just the first frame. Returns "" when there's nothing obvious - the
        /// boxes are still tunable, they just get a plain backdrop.
        /// </summary>
        private static string EntitySprite(GameProject project, string className,
            BoundsCatalogue catalogue, out int frames)
        {
            frames = 1;

            string assetsFile = project.AssetsPath;
            if (!System.IO.File.Exists(assetsFile)) return "";

            foreach (Match match in SheetDecl.Matches(System.IO.File.ReadAllText(assetsFile)))
            {
                string name = match.Groups[1].Value;
                if (!name.StartsWith(className, StringComparison.OrdinalIgnoreCase)) continue;

                frames = int.TryParse(match.Groups[3].Value, out int f) && f > 0 ? f : 1;
                return GameCatalogue.SpriteUrl(project, match.Groups[2].Value, catalogue.Warnings);
            }

            return "";
        }

        // ------------------------------------------------------------------- shared

        private static string Relative(GameProject project, string file) =>
            Path.GetRelativePath(project.Root, file).Replace(Path.DirectorySeparatorChar, '/');

        /// <summary>"TreeSmall" -> "Tree Small".</summary>
        private static string Humanise(string name) =>
            Regex.Replace(name, "(?<!^)([A-Z])", " $1");
    }
}
