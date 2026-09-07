using System;
using System.IO;
using System.Linq;

namespace Ashenveil.Editor.Core
{
    /// <summary>
    /// Knows where things live inside a game folder. Every path the editor touches
    /// comes from here, so the layout is described in exactly one place.
    ///
    /// The name of the core project is not hardcoded - it's found by looking for the
    /// subfolder that holds Content/Sprites. Renaming the game (merder.Core ->
    /// Ashenveil.Core) then costs nothing here.
    /// </summary>
    public class GameProject
    {
        public string Root { get; }

        /// <summary>
        /// The core project folder, e.g. "&lt;root&gt;/Ashenveil.Core". Resolved once at
        /// construction; falls back to Root so the path properties still return
        /// something sane for an invalid folder rather than throwing.
        /// </summary>
        public string CoreDir { get; }

        public GameProject(string root)
        {
            Root = root;
            CoreDir = FindCoreDir(root) ?? root;
        }

        public string ContentDir => Path.Combine(CoreDir, "Content");
        public string SpritesDir => Path.Combine(ContentDir, "Sprites");
        public string LevelsDir  => Path.Combine(ContentDir, "levels");

        public string LevelPath(string name) => Path.Combine(LevelsDir, name + ".json");

        // Game source files the editor reads to build its palette. See GameCatalogue.
        public string AssetsPath    => Path.Combine(ContentDir, "Assets.cs");
        public string MapLoaderPath   => Path.Combine(CoreDir, "Levels", "MapLoader.cs");
        public string TileCatalogPath => Path.Combine(CoreDir, "Tiles", "TileCatalog.cs");
        public string TileTypePath    => Path.Combine(CoreDir, "Tiles", "ITile.cs");
        public string TileMapPath   => Path.Combine(CoreDir, "Tiles", "TileMap.cs");

        /// <summary>
        /// Finds the core project inside a repo root: the immediate subfolder that has
        /// Content/Sprites in it. Returns null when there isn't one, which is what makes
        /// a folder "not the game".
        ///
        /// Content/Sprites is the marker rather than Content alone because several
        /// projects in a MonoGame solution carry a Content folder - only the shared core
        /// one holds the art. If more than one matches, a name ending in ".Core" wins,
        /// then alphabetical order, so the answer never depends on directory ordering.
        /// </summary>
        public static string? FindCoreDir(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;

            try
            {
                return Directory.EnumerateDirectories(root)
                    .Where(d => Directory.Exists(Path.Combine(d, "Content", "Sprites")))
                    .OrderByDescending(d => Path.GetFileName(d)!
                        .EndsWith(".Core", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            }
            catch (UnauthorizedAccessException)
            {
                return null;   // a folder we can't read is not a folder we can edit
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
