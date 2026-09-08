using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ashenveil.Editor.Core
{
    /// <summary>
    /// Appends content-pipeline entries to the game's .mgcb so a sprite dropped in
    /// Content/Sprites actually gets built into an .xnb. This is the auto-fix that
    /// replaces hand-editing the .mgcb.
    ///
    /// It is deliberately additive and timid: it only ever appends
    /// blocks for files that are on disk and not already registered, it never rewrites
    /// or reorders existing entries, and it writes atomically so a crash mid-write
    /// can't leave a half-broken pipeline file.
    /// </summary>
    public static class MgcbRegistrar
    {
        /// <summary>
        /// Registers the given content names (extension-free, e.g.
        /// "Sprites/Tiles/turn_dirt_road"). Returns the ones actually added. Silently
        /// skips any that are already registered or have no PNG on disk — the report
        /// that produced the list already knows which are which, and being idempotent
        /// means calling twice is harmless.
        /// </summary>
        public static List<string> Register(GameProject project, IEnumerable<string> contentNames)
        {
            string mgcb = AssetAudit.FindMgcb(project)
                ?? throw new FileNotFoundException("No .mgcb found in the Content folder.");

            string existing = File.ReadAllText(mgcb);
            var alreadyBuilt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(existing, @"^/build:\s*(\S+)\s*$", RegexOptions.Multiline))
                alreadyBuilt.Add(m.Groups[1].Value);

            var added = new List<string>();
            var blocks = new StringBuilder();

            foreach (string name in contentNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string buildPath = name + ".png";                       // Sprites/Tiles/x.png
                if (alreadyBuilt.Contains(buildPath)) continue;         // don't double-register

                string file = Path.Combine(project.ContentDir,
                    buildPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file)) continue;                       // never register a phantom

                blocks.Append(Block(buildPath));
                added.Add(name);
            }

            if (added.Count == 0) return added;

            // Append, keeping exactly one blank line between the old content and the new.
            var sb = new StringBuilder(existing.TrimEnd('\r', '\n'));
            sb.Append('\n');
            sb.Append(blocks);

            WriteAtomic(mgcb, sb.ToString());
            return added;
        }

        /// <summary>
        /// The standard pixel-art texture block — the same one that was being pasted by
        /// hand. Color (not Compressed) keeps pixels crisp; ColorKeyEnabled off because
        /// the PNGs already carry alpha.
        /// </summary>
        private static string Block(string buildPath) =>
            "\n#begin " + buildPath + "\n" +
            "/importer:TextureImporter\n" +
            "/processor:TextureProcessor\n" +
            "/processorParam:ColorKeyEnabled=False\n" +
            "/processorParam:TextureFormat=Color\n" +
            "/build:" + buildPath + "\n";

        private static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, path, overwrite: true);
        }
    }
}
