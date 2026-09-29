using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// A UI element a screen owns — a button today, a label or panel later. A screen keeps
    /// a List&lt;Widget&gt; and loops it: Update for input, Draw to paint. The base carries what
    /// every widget has — where it is, whether it's shown, and whether the mouse is over it —
    /// so a subclass only adds what makes it different (a Button its click, a Label its text).
    /// </summary>
    public abstract class Widget
    {
        protected Rectangle Bounds;
        protected bool Hovered { get; private set; }
        public bool Visible { get; set; } = true;

        protected Widget(Rectangle bounds) => Bounds = bounds;

        // Shared input plumbing: track hover (only when shown). A subclass calls base.Update
        // then adds its own reaction (a button fires its click).
        public virtual void Update(MouseState mouse, MouseState prev)
        {
            Hovered = Visible && Bounds.Contains(mouse.X, mouse.Y);
        }

        public abstract void Draw(SpriteBatch spriteBatch, SpriteFont font);

        /// <summary>A full left-click this frame: held last frame, released this one.</summary>
        protected static bool Clicked(MouseState mouse, MouseState prev) =>
            mouse.LeftButton == ButtonState.Released && prev.LeftButton == ButtonState.Pressed;
    }
}
