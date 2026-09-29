using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Utility.Widgets
{
    /// <summary>
    /// A nine-sliced background (panel_*, round_*, banner_* ...). Ignores input — add it to the
    /// widget list BEFORE the things that sit on it so it draws underneath them.
    /// </summary>
    public class Panel : Widget
    {
        private string _sprite;
        private int _border, _scale;

        // border = source pixels of corner art. 16 suits the 64x64 panels; lower it for plainer ones.
        public Panel(Rectangle bounds, string sprite, int border = 16, int scale = 2)
            : base(bounds)
        {
            _sprite = sprite;
            _border = border;
            _scale = scale;
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible) return;
            UI.DrawNineSlice(spriteBatch, AshenveilGame.UiAtlas, _sprite, Bounds, _border, _scale, Color.White);
        }
    }
}
