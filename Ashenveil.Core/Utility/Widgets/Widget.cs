using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// A UI element a screen owns — a button today, a label or panel later. A screen keeps
    /// a List&lt;Widget&gt; and loops it: Update for input, Draw to paint. The base fixes that
    /// contract so the loop never cares which kind of widget it's touching.
    /// </summary>
    public abstract class Widget
    {
        public abstract void Update(MouseState mouse, MouseState prev);
        public abstract void Draw(SpriteBatch spriteBatch, SpriteFont font);
    }
}
