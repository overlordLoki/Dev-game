using System.Collections.Generic;
using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Screens
{
    public interface IScreen
    {
        List<Widget> Widgets { get; set; }
        void Update(GameTime gameTime);
        void Draw(SpriteBatch spriteBatch, Texture2D pixel);

        /// The matrix the screen's sprite batch is begun with. Menus and HUDs draw in
        /// screen space and want the default; the world overrides it with its camera
        /// so it can keep drawing in world coordinates.
        Matrix Transform => Matrix.Identity;

        /// One typed character, from window.TextInput (already shifted and key-repeated by
        /// the OS; '\r' is Enter, '\b' Backspace). Only the top screen gets it, like Update.
        /// Most screens don't take typing, so the default ignores it.
        void OnTextInput(char c) { }
    }
}