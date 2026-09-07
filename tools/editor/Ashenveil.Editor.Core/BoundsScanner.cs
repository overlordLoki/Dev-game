using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ashenveil.Editor.Core
{
    /// <summary>One tunable number in a collision box.</summary>
    public class BoundsKnob
    {
        /// <summary>Canonical name the editor uses: width, height, footInset or xShift.</summary>
        public string Key { get; set; } = "";
        /// <summary>What the game calls it in this file: TrunkW, boxW, ...</summary>
        public string Identifier { get; set; } = "";
        public float Value { get; set; }
        /// <summary>
        /// False when the class inherits the value instead of stating it (a Tree subclass
        /// that never overrides FootInset). Patching one of these has to add a line rather
        /// than replace a number.
        /// </summary>
        public bool Declared { get; set; }
        /// <summary>1-based line of the declaration, when Declared.</summary>
        public int Line { get; set; }
    }

    /// <summary>One thing with a collision box you can tune.</summary>
    public class BoundsTarget
    {
        /// <summary>Class name - TreeSmall, Player. Also the id the patch route takes.</summary>
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>"object" or "entity" - only affects how it's grouped in the UI.</summary>
        public string Kind { get; set; } = "";
        /// <summary>Path relative to the repo root, for display.</summary>
        public string File { get; set; } = "";

        /// <summary>
        /// What the fractions are measured against. "cell" for objects, which are sized
        /// by the grid; "sprite" for entities, which are sized by their own Width/Height.
        /// The preview has to honour this or the box it draws is a lie.
        /// </summary>
        public string Basis { get; set; } = "";

        /// <summary>
        /// How big the art is drawn relative to a cell: trees at 1.5, entities at 0.9.
        /// Taken from the game's own draw code so the preview matches what you see in game.
        /// </summary>
        public float SpriteScale { get; set; } = 1f;

        /// <summary>Art to preview behind the box, or "" when the class has no fixed sprite.</summary>
        public string SpriteUrl { get; set; } = "";
        /// <summary>Frames across, when the art is a sprite sheet (1 for a plain image).</summary>
        public int SpriteFrames { get; set; } = 1;

        public List<BoundsKnob> Knobs { get; set; } = new();
    }

    public class BoundsCatalogue
    {
        public List<BoundsTarget> Targets { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Finds the collision boxes in the game's source so they can be dragged instead of
    /// guessed at, and writes the tuned numbers back.
    ///
    /// Every box in the game is the same four fractions - width, height, lift off the
    /// bottom, sideways shift - but they're written two ways:
    ///
    ///   Tree subclasses   protected override float TrunkW =&gt; 0.40f;
    ///   Entities          const float boxW = 0.35f;   (inside the Bounds getter)
    ///
    /// Both are recognised. Writing only ever replaces the number on a line that already
    /// matched, or adds one property line to a Tree subclass that inherits a default -
    /// it never restructures code it doesn't understand.
    /// </summary>
    public static class BoundsScanner
    {
        // TrunkW / boxW both mean "width", and so on. One vocabulary for the UI.
        private static readonly Dictionary<string, string> KeyOf = new(StringComparer.Ordinal)
        {
            ["TrunkW"] = "width",  ["boxW"] = "width",
            ["TrunkH"] = "height", ["boxH"] = "height",
            ["FootInset"] = "footInset", ["footInset"] = "footInset",
            ["XShift"] = "xShift", ["xShift"] = "xShift",
        };

        private static readonly string[] Order = { "width", "height", "footInset", "xShift" };

        // protected override float TrunkW => 0.40f;
        private static readonly Regex OverrideKnob = new(
            @"^\s*protected\s+(?:override|virtual)\s+float\s+(\w+)\s*=>\s*(-?\d*\.?\d+)f\s*;",
            RegexOptions.Compiled);

        // const float boxW = 0.35f;
        private static readonly Regex ConstKnob = new(
            @"^\s*const\s+float\s+(\w+)\s*=\s*(-?\d*\.?\d+)f\s*;",
            RegexOptions.Compiled);

        private static readonly Regex ClassDecl = new(
            @"\bclass\s+(\w+)\s*(?::\s*([\w\s,]+))?", RegexOptions.Compiled);

        // : base(col, row, Assets.Tree_Small, rotate)
        private static readonly Regex BaseAsset = new(
            @"base\s*\([^)]*Assets\.(\w+)", RegexOptions.Compiled);

        // public static readonly SpriteSheetInfo PLAYERIDLE = new("Sprites/Player/D_Idle", 4, 32);
        private static readonly Regex SheetDecl = new(
            @"SpriteSheetInfo\s+(\w+)\s*=\s*new\s*\(\s*""([^""]+)""\s*,\s*(\d+)\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled);

        public static BoundsCatalogue Build(GameProject project)
        {
            var catalogue = new BoundsCatalogue();
            var assets = GameCatalogue.ParseAssets(project, catalogue.Warnings);

            ScanObjects(project, assets, catalogue);
            ScanEntities(project, assets, catalogue);

            if (catalogue.Targets.Count == 0)
                catalogue.Warnings.Add("No collision boxes found - has the source layout changed?");

            return catalogue;
        }

        // ------------------------------------------------------------------ objects

        private static void ScanObjects(GameProject project,
            Dictionary<string, GameCatalogue.AssetConst> assets, BoundsCatalogue catalogue)
        {
            string dir = Path.Combine(project.CoreDir, "Objects");
            if (!Directory.Exists(dir))
            {
                catalogue.Warnings.Add("No Objects folder found.");
                return;
            }

            // Any abstract object base (Tree, and future ones) and the default knobs its
            // subclasses inherit. Keyed by class name so a subclass can find its own base
            // rather than assuming everything descends from Tree.
            var baseKnobs = new Dictionary<string, Dictionary<string, BoundsKnob>>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(dir, "*.cs"))
            {
                string text = System.IO.File.ReadAllText(file);
                if (!text.Contains("abstract class")) continue;
                var decl = ClassDecl.Match(text);
                if (!decl.Success) continue;
                var k = ReadKnobs(file, OverrideKnob);
                if (k.Count > 0) baseKnobs[decl.Groups[1].Value] = k;
            }

            foreach (string file in Directory.EnumerateFiles(dir, "*.cs").OrderBy(f => f))
            {
                string text = System.IO.File.ReadAllText(file);
                string className = Path.GetFileNameWithoutExtension(file);

                // Concrete classes only; skip interfaces and the abstract bases themselves.
                if (!Regex.IsMatch(text, @"\bclass\s+\w+")) continue;    // interface/enum file
                if (text.Contains("abstract class")) continue;
                var decl = ClassDecl.Match(text);
                if (!decl.Success) continue;

                var knobs = ReadKnobs(file, OverrideKnob);

                // Which abstract base (if any) this class inherits its defaults from.
                var bases = decl.Groups[2].Value.Split(',').Select(s => s.Trim());
                string? inheritedFrom = bases.FirstOrDefault(b => baseKnobs.ContainsKey(b));

                // Tunable only if it states its own knobs or inherits some from a base.
                if (knobs.Count == 0 && inheritedFrom == null) continue;

                // Fill in anything inherited rather than declared here.
                if (inheritedFrom != null)
                    foreach (var (id, knob) in baseKnobs[inheritedFrom])
                    {
                        if (knobs.ContainsKey(id)) continue;
                        knobs[id] = new BoundsKnob
                        {
                            Key = knob.Key, Identifier = id, Value = knob.Value, Declared = false,
                        };
                    }

                if (knobs.Count == 0) continue;

                var target = new BoundsTarget
                {
                    Id          = className,
                    Name        = Humanise(className),
                    Kind        = "object",
                    File        = Relative(project, file),
                    Basis       = "cell",
                    SpriteScale = 1.5f,     // Tree.Draw: size = Layout.CellSize * 1.5f
                    Knobs       = Sort(knobs.Values),
                };

                var asset = BaseAsset.Match(text);
                if (asset.Success && assets.TryGetValue(asset.Groups[1].Value, out var found))
                    target.SpriteUrl = GameCatalogue.SpriteUrl(project, found.Path, catalogue.Warnings);
                else
                    catalogue.Warnings.Add($"{className}: couldn't tell which sprite it uses.");

                catalogue.Targets.Add(target);
            }
        }

        // ----------------------------------------------------------------- entities

        private static void ScanEntities(GameProject project,
            Dictionary<string, GameCatalogue.AssetConst> assets, BoundsCatalogue catalogue)
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

                // Skip interface files by their content, not the filename's first letter —
                // an entity called "Imp" or "Ice" must not be mistaken for an interface.
                if (!Regex.IsMatch(text, @"\bclass\s+\w+")) continue;

                var knobs = ReadKnobs(file, ConstKnob);
                if (knobs.Count == 0)
                {
                    // The most likely reason is bare numbers inline in the Bounds getter,
                    // which there is no safe way to name or patch.
                    if (text.Contains("Rectangle Bounds"))
                        catalogue.Warnings.Add(
                            $"{className}: collision box uses inline numbers - " +
                            "give them named consts (boxW/boxH/footInset/xShift) to tune it here.");
                    continue;
                }

                catalogue.Targets.Add(new BoundsTarget
                {
                    Id          = className,
                    Name        = Humanise(className),
                    Kind        = "entity",
                    File        = Relative(project, file),
                    Basis       = "sprite",
                    SpriteScale = 0.9f,     // Player/NPC: Width = Height = CellSize * 0.9f
                    SpriteUrl   = EntitySprite(project, className, catalogue, out int frames),
                    SpriteFrames = frames,
                    Knobs       = Sort(knobs.Values),
                });
            }
        }

        /// <summary>
        /// Entities are drawn from animation sheets, not single images. Finds the sheet
        /// whose name looks like this entity's and reports its frame count so the preview
        /// can show just the first frame. Returns "" when there's nothing obvious - the
        /// box is still tunable, it just gets a plain backdrop.
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

        /// <summary>Reads every knob a file declares, keyed by its identifier.</summary>
        private static Dictionary<string, BoundsKnob> ReadKnobs(string file, Regex pattern)
        {
            var found = new Dictionary<string, BoundsKnob>(StringComparer.Ordinal);
            if (!System.IO.File.Exists(file)) return found;

            string[] lines = System.IO.File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = pattern.Match(lines[i]);
                if (!match.Success) continue;

                string id = match.Groups[1].Value;
                if (!KeyOf.TryGetValue(id, out string? key)) continue;
                if (!float.TryParse(match.Groups[2].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value)) continue;

                found[id] = new BoundsKnob
                {
                    Key = key, Identifier = id, Value = value, Declared = true, Line = i + 1,
                };
            }

            return found;
        }

        private static List<BoundsKnob> Sort(IEnumerable<BoundsKnob> knobs) =>
            knobs.OrderBy(k => Array.IndexOf(Order, k.Key)).ToList();

        private static string Relative(GameProject project, string file) =>
            Path.GetRelativePath(project.Root, file).Replace(Path.DirectorySeparatorChar, '/');

        /// <summary>"TreeSmall" -> "Tree Small".</summary>
        private static string Humanise(string name) =>
            Regex.Replace(name, "(?<!^)([A-Z])", " $1");
    }
}
