using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Ashenveil.Core.Collision
{
    /// <summary>
    /// The collision boxes for every object and entity, loaded once from
    /// Content/bounds.json and keyed by class name (TreeSmall, Well, Player). The editor
    /// under tools/editor writes this file; the game only ever reads it.
    ///
    /// A class with no entry gets an empty list - it simply has no collision box yet,
    /// which the debug outlines make obvious rather than the game guessing a shape.
    /// The file is read from the exe's folder the same way MapLoader reads levels, so it
    /// must be copied next to the exe (see the DesktopGL project's csproj).
    /// </summary>
    public static class BoundsStore
    {
        private static readonly IReadOnlyList<Box> None = Array.Empty<Box>();
        private static Dictionary<string, List<Box>> _boxes;

        /// <summary>The boxes declared for a class, or an empty list if it has none.</summary>
        public static IReadOnlyList<Box> For(string className)
        {
            EnsureLoaded();
            return _boxes.TryGetValue(className, out var list) ? list : None;
        }

        private static void EnsureLoaded()
        {
            if (_boxes != null) return;
            _boxes = new Dictionary<string, List<Box>>(StringComparer.Ordinal);

            string path = Path.Combine(AppContext.BaseDirectory, "Content", "bounds.json");
            if (!File.Exists(path)) return;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                var list = new List<Box>();
                if (entry.Value.TryGetProperty("boxes", out var boxes)
                    && boxes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in boxes.EnumerateArray())
                        list.Add(new Box(F(b, "x"), F(b, "y"), F(b, "w"), F(b, "h")));
                }
                _boxes[entry.Name] = list;
            }
        }

        private static float F(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? (float)v.GetDouble()
                : 0f;
    }
}
