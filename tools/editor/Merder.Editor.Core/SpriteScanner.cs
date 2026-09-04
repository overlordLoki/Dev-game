using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Merder.Editor.Core
{
    /// <summary>One PNG found under Content/Sprites.</summary>
    public class SpriteInfo
    {
        /// <summary>Path under Sprites/ without extension, forward slashes: "Tiles/grass".</summary>
        public string Key { get; set; } = "";
        /// <summary>Folder the sprite sits in ("Tiles", "Objects", ...). "" for loose files.</summary>
        public string Group { get; set; } = "";
        /// <summary>URL the web view can load directly, served by the app's scheme handler.</summary>
        public string Url { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Replaces build-sprites.py: walks Content/Sprites and reports every PNG.
    /// Drop art in the folder and it shows up — no regeneration step.
    /// </summary>
    public static class SpriteScanner
    {
        public static List<SpriteInfo> Scan(GameProject project)
        {
            var result = new List<SpriteInfo>();
            if (!Directory.Exists(project.SpritesDir)) return result;

            foreach (var file in Directory.EnumerateFiles(project.SpritesDir, "*.png", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(project.SpritesDir, file).Replace(Path.DirectorySeparatorChar, '/');
                string key = rel.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? rel[..^4]
                    : rel;

                int slash = key.LastIndexOf('/');
                var (w, h) = ReadPngSize(file);

                result.Add(new SpriteInfo
                {
                    Key    = key,
                    Group  = slash < 0 ? "" : key[..slash],
                    Url    = "sprite:///" + Uri.EscapeDataString(rel).Replace("%2F", "/"),
                    Width  = w,
                    Height = h,
                });
            }

            return result.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Pulls width/height straight out of the PNG IHDR chunk — the first 24 bytes
        /// of a PNG are signature (8) + chunk length/type (8) + width/height (8),
        /// both big-endian. Cheaper than decoding the image.
        /// </summary>
        private static (int, int) ReadPngSize(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                Span<byte> head = stackalloc byte[24];
                if (fs.Read(head) < 24) return (0, 0);

                int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
                int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
                return (w, h);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}
