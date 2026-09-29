using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core
{
    // One texture plus named rectangles on it. Nothing is cut up; you draw
    // part of the texture by passing a sourceRectangle to SpriteBatch.Draw.
    public class TextureAtlas
    {
        public Texture2D Texture { get; }
        private readonly Dictionary<string, Rectangle> _regions = new();

        private TextureAtlas(Texture2D texture) => Texture = texture;

        // xmlPath is relative to the Content folder, e.g. "Sprites/UI/spritesheet-default.xml"
        public static TextureAtlas FromXml(ContentManager content, string xmlPath)
        {
            // TitleContainer works on Desktop AND Android (File.Open doesn't on Android).
            using Stream stream = TitleContainer.OpenStream(Path.Combine(content.RootDirectory, xmlPath));
            XElement root = XDocument.Load(stream).Root;

            // imagePath="spritesheet-default.png" is next to the xml; Content.Load wants no extension.
            string folder = Path.GetDirectoryName(xmlPath);
            string image = Path.GetFileNameWithoutExtension(root.Attribute("imagePath").Value);
            var atlas = new TextureAtlas(content.Load<Texture2D>(Path.Combine(folder, image).Replace('\\', '/')));

            foreach (XElement sub in root.Elements("SubTexture"))
            {
                // "button_brownimg.png" -> "button_brown"
                string name = sub.Attribute("name").Value;
                if (name.EndsWith("img.png")) name = name[..^"img.png".Length];

                atlas._regions[name] = new Rectangle(
                    (int)sub.Attribute("x"), (int)sub.Attribute("y"),
                    (int)sub.Attribute("width"), (int)sub.Attribute("height"));
            }
            return atlas;
        }

        public Rectangle this[string name] => _regions[name];

        public void Draw(SpriteBatch sb, string name, Vector2 position, Color? tint = null, float scale = 1f)
            => sb.Draw(Texture, position, _regions[name], tint ?? Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        // Stretch a region to fill a rectangle (fine for bars and patterns; see note below for panels)
        public void Draw(SpriteBatch sb, string name, Rectangle destination, Color? tint = null)
            => sb.Draw(Texture, destination, _regions[name], tint ?? Color.White);
    }
}