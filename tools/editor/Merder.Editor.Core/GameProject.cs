using System.IO;

namespace Merder.Editor.Core
{
    /// <summary>
    /// Knows where things live inside a merder game folder. Every path the editor
    /// touches comes from here, so the layout is described in exactly one place.
    /// </summary>
    public class GameProject
    {
        public string Root { get; }

        public GameProject(string root) => Root = root;

        public string ContentDir => Path.Combine(Root, "merder.Core", "Content");
        public string SpritesDir => Path.Combine(ContentDir, "Sprites");
        public string LevelsDir  => Path.Combine(ContentDir, "levels");

        public string LevelPath(string name) => Path.Combine(LevelsDir, name + ".json");

        // Game source files the editor reads to build its palette. See GameCatalogue.
        public string AssetsPath    => Path.Combine(ContentDir, "Assets.cs");
        public string MapLoaderPath => Path.Combine(Root, "merder.Core", "Levels", "MapLoader.cs");
        public string TileMapPath   => Path.Combine(Root, "merder.Core", "Tiles", "TileMap.cs");
    }
}
