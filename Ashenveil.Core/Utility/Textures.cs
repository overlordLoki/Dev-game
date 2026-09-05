using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core
{
    /// <summary>
    /// Thin shared accessor over MonoGame's ContentManager for textures.
    ///
    /// Why this exists: only AshenveilGame owns the ContentManager, but many
    /// classes (Tree, TileMap, entities) need textures. Instead of loading
    /// everything up-front in LoadContent and passing dictionaries around,
    /// any class can call Textures.Get(name) here.
    ///
    /// Note: ContentManager already caches internally — asking for the same
    /// name twice loads from disk once and returns the same instance. So
    /// Get() is cheap to call repeatedly; no extra caching needed here.
    ///
    /// Trade-off: this is global state. Convenient, but it hides who depends
    /// on content. Fine for a solo project; the stricter approach would pass
    /// the ContentManager in explicitly instead of using a static.
    /// </summary>
    public static class Textures
    {
        private static ContentManager _content;

        // Call once, early in LoadContent, before anything asks for a texture.
        public static void Init(ContentManager content) => _content = content;

        public static Texture2D Get(string name) => _content.Load<Texture2D>(name);
    }
}
