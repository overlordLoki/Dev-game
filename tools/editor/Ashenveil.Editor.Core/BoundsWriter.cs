using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ashenveil.Editor.Core
{
    /// <summary>
    /// Writes tuned collision-box numbers back into the game's source.
    ///
    /// This is the only part of the editor that edits game code, so it is deliberately
    /// timid. It will do exactly two things:
    ///
    ///   1. Replace the numeric literal on a line it has already matched and re-verified.
    ///   2. Add one `protected override float X =&gt; 0.00f;` line to a Tree subclass that
    ///      currently inherits that value.
    ///
    /// It never reformats, never reorders, and never touches a line whose shape it
    /// doesn't recognise. If the file changed since it was scanned, the write is refused
    /// rather than applied to the wrong line.
    /// </summary>
    public static class BoundsWriter
    {
        private static readonly Regex OverrideKnob = new(
            @"^(\s*protected\s+(?:override|virtual)\s+float\s+(\w+)\s*=>\s*)(-?\d*\.?\d+)(f\s*;.*)$",
            RegexOptions.Compiled);

        private static readonly Regex ConstKnob = new(
            @"^(\s*const\s+float\s+(\w+)\s*=\s*)(-?\d*\.?\d+)(f\s*;.*)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Applies new values to one target. `values` is keyed by canonical knob name
        /// (width, height, footInset, xShift); unknown keys are ignored, missing ones are
        /// left alone. Returns the knobs that actually changed.
        /// </summary>
        public static List<string> Apply(GameProject project, BoundsTarget target,
                                         Dictionary<string, float> values)
        {
            string path = Path.Combine(project.Root, target.File.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new FileNotFoundException($"{target.File} is gone.", path);

            var lines = File.ReadAllLines(path).ToList();
            var changed = new List<string>();

            foreach (var knob in target.Knobs)
            {
                if (!values.TryGetValue(knob.Key, out float wanted)) continue;
                if (Same(wanted, knob.Value)) continue;

                if (knob.Declared) ReplaceLiteral(lines, knob, wanted, target, path);
                else InsertOverride(lines, knob, wanted, target, path);

                changed.Add(knob.Key);
            }

            if (changed.Count > 0)
                WriteAtomic(path, lines);

            return changed;
        }

        /// <summary>
        /// Swaps the number on the knob's own line, after checking the line still says
        /// what the scan said it said. That check is what makes this safe to run against
        /// a file edited in the meantime: it fails loudly instead of corrupting code.
        /// </summary>
        private static void ReplaceLiteral(List<string> lines, BoundsKnob knob, float wanted,
                                           BoundsTarget target, string path)
        {
            int index = knob.Line - 1;
            if (index < 0 || index >= lines.Count)
                throw new InvalidDataException(
                    $"{target.File} has changed since it was loaded (line {knob.Line} is gone). Reload and try again.");

            var pattern = target.Style() == "override" ? OverrideKnob : ConstKnob;
            var match = pattern.Match(lines[index]);

            if (!match.Success || match.Groups[2].Value != knob.Identifier)
                throw new InvalidDataException(
                    $"{target.File} line {knob.Line} no longer declares {knob.Identifier}. Reload and try again.");

            lines[index] = match.Groups[1].Value + Format(wanted) + match.Groups[4].Value;
        }

        /// <summary>
        /// Adds a knob a Tree subclass was inheriting. The new line goes directly after
        /// the last property the class already declares, copying its indentation, so the
        /// result reads like the surrounding code.
        /// </summary>
        private static void InsertOverride(List<string> lines, BoundsKnob knob, float wanted,
                                           BoundsTarget target, string path)
        {
            if (target.Style() != "override")
                throw new InvalidDataException(
                    $"{knob.Identifier} isn't declared in {target.File} and can't be added automatically.");

            int last = -1;
            string indent = "        ";

            for (int i = 0; i < lines.Count; i++)
            {
                var match = OverrideKnob.Match(lines[i]);
                if (!match.Success) continue;
                last = i;
                indent = match.Groups[1].Value[..(match.Groups[1].Value.Length - match.Groups[1].Value.TrimStart().Length)];
            }

            if (last < 0)
                throw new InvalidDataException(
                    $"Couldn't find where to add {knob.Identifier} in {target.File}.");

            lines.Insert(last + 1,
                $"{indent}protected override float {knob.Identifier} => {Format(wanted)}f;");
        }

        /// <summary>
        /// Same temp-file-then-move dance the level saver uses: a crash mid-write must not
        /// leave a half-written source file behind.
        /// </summary>
        private static void WriteAtomic(string path, List<string> lines)
        {
            string tmp = path + ".tmp";
            File.WriteAllLines(tmp, lines);
            File.Move(tmp, path, overwrite: true);
        }

        /// <summary>Two decimals, invariant - matches how the values are already written.</summary>
        private static string Format(float value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);

        private static bool Same(float a, float b) => Math.Abs(a - b) < 0.0005f;
    }

    internal static class BoundsTargetExtensions
    {
        /// <summary>
        /// Which of the two source shapes this target uses. Objects state their box with
        /// property overrides; entities with local consts inside the Bounds getter.
        /// </summary>
        public static string Style(this BoundsTarget target) =>
            target.Kind == "object" ? "override" : "const";
    }
}
