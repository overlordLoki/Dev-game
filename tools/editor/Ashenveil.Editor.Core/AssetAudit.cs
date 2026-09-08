using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ashenveil.Editor.Core
{
    /// <summary>
    /// The result of cross-referencing the three places a sprite has to be accounted
    /// for. Content names here are extension-free ("Sprites/Tiles/grass"), the same
    /// form Assets.cs and Content.Load use.
    /// </summary>
    public class AssetReport
    {
        /// <summary>
        /// On disk but not in the .mgcb — will crash the game with "file not found"
        /// if loaded. These are the ones the registrar can fix.
        /// </summary>
        public List<string> NotRegistered { get; set; } = new();

        /// <summary>Referenced in Assets.cs but no PNG on disk — a typo or a moved file.</summary>
        public List<string> MissingFile { get; set; } = new();

        /// <summary>On disk but no Assets.cs constant points at it — art present, not wired up.</summary>
        public List<string> NotInAssets { get; set; } = new();

        /// <summary>True when there's a .mgcb to write to and something to add.</summary>
        public bool CanRegister => NotRegistered.Count > 0;
    }

    /// <summary>
    /// Diagnoses the three-way agreement a sprite needs: the PNG exists, the .mgcb
    /// builds it, and Assets.cs names it. The missing-from-.mgcb case is the one that
    /// crashes the game at load, so it's the one the registrar can act on.
    ///
    /// Read-only. Nothing here writes; see MgcbRegistrar for the fix.
    /// </summary>
    public static class AssetAudit
    {
        // Any "Sprites/..." content path quoted in Assets.cs — covers both
        // `const string X = "Sprites/..."` and `new SpriteSheetInfo("Sprites/...", ...)`.
        private static readonly Regex QuotedContentPath = new(
            @"""(Sprites/[^""]+)""", RegexOptions.Compiled);

        // A build line in the .mgcb: /build:Sprites/Tiles/grass.png
        private static readonly Regex BuildLine = new(
            @"^/build:\s*(Sprites/[^\s]+\.png)\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static AssetReport Run(GameProject project)
        {
            var report = new AssetReport();

            // On disk: every PNG under Content/Sprites, as an extension-free content name.
            var onDisk = SpriteScanner.Scan(project)
                .Select(s => "Sprites/" + s.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // In Assets.cs: every quoted content path.
            var inAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(project.AssetsPath))
                foreach (Match m in QuotedContentPath.Matches(File.ReadAllText(project.AssetsPath)))
                    inAssets.Add(m.Groups[1].Value);

            // In the .mgcb: every built sprite, extension stripped to match the others.
            var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? mgcb = FindMgcb(project);
            if (mgcb != null)
                foreach (Match m in BuildLine.Matches(File.ReadAllText(mgcb)))
                    registered.Add(m.Groups[1].Value[..^4]);   // drop ".png"

            report.NotRegistered = onDisk.Where(p => !registered.Contains(p)).OrderBy(p => p).ToList();
            report.MissingFile   = inAssets.Where(p => !onDisk.Contains(p)).OrderBy(p => p).ToList();
            report.NotInAssets   = onDisk.Where(p => !inAssets.Contains(p)).OrderBy(p => p).ToList();

            return report;
        }

        /// <summary>The single .mgcb in the Content folder, or null if there isn't one.</summary>
        internal static string? FindMgcb(GameProject project)
        {
            if (!Directory.Exists(project.ContentDir)) return null;
            return Directory.EnumerateFiles(project.ContentDir, "*.mgcb").FirstOrDefault();
        }
    }
}
