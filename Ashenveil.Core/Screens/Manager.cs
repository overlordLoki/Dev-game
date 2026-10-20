using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Screens
{
    public class ScreenManager
    {
        private Stack<IScreen> _screens = new();

        public void Push(IScreen screen) => _screens.Push(screen);
        public void Pop() { if (_screens.Count > 0) _screens.Pop(); }
        public IScreen Current => _screens.Count > 0 ? _screens.Peek() : null;

        public void Update(GameTime gameTime)
        {
            if (_screens.Count > 0)
                _screens.Peek().Update(gameTime);
        }

        // Typing goes to the top screen only, the same as Update: a screen underneath
        // (the world under the chat) never hears it.
        public void TextInput(char c) => Current?.OnTextInput(c);

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // One batch per screen: each screen picks its own transform, so the world
            // can be drawn through the camera while a menu stacked on top of it stays
            // pinned to the window.
            foreach (var screen in _screens.Reverse())
            {
                spriteBatch.Begin(transformMatrix: screen.Transform);
                screen.Draw(spriteBatch, pixel);
                spriteBatch.End();
            }
        }
    }
}