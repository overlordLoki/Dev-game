using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace merder.Core.Screens
{
    public class ScreenManager
    {
        private Stack<IScreen> _screens = new();

        public void Push(IScreen screen) => _screens.Push(screen);
        public void Pop() { if (_screens.Count > 0) _screens.Pop(); }

        public void Update(GameTime gameTime)
        {
            if (_screens.Count > 0)
                _screens.Peek().Update(gameTime);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var screen in _screens.Reverse())
                screen.Draw(spriteBatch, pixel);
        }
    }
}