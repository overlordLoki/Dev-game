using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Ashenveil.Core.Entities;

namespace Ashenveil.Core.Screens
{
    public class Gamebox
    {
        private Texture2D _pixel;
        private int _gap;
        private int _thickness;
        public Rectangle Bounds { get; private set; }
        public Gamebox(Texture2D pixel, int screenWidth, int screenHeight, int gap = 20, int thickness = 4)
        {
            _pixel = pixel;
            _gap = gap;
            _thickness = thickness;

            // The playable area sits just inside the border
            Bounds = new Rectangle(
                gap + thickness,
                gap + thickness,
                screenWidth - (gap + thickness) * 2,
                screenHeight - (gap + thickness) * 2
            );
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Color color = Color.Red;
            int w = Bounds.X + Bounds.Width + _thickness;
            int h = Bounds.Y + Bounds.Height + _thickness;
            //new Rectangle(x,  y,  width,  height )
            // Top
            spriteBatch.Draw(_pixel, new Rectangle(_gap, _gap, w - _gap * 2 + _thickness, _thickness), Color.White);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(_gap, h, w - _gap * 2 + _thickness, _thickness), Color.White);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(_gap, _gap, _thickness, h - _gap), Color.White);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(w, _gap, _thickness, h - _gap), Color.White);
        }
        public void CheckCollision(IEntity entity)
        {
            // left wall
            if (entity.Position.X < Bounds.Left) {
                entity.Position = new Vector2(Bounds.Left, entity.Position.Y);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(-entity.direction.X, entity.direction.Y);
            }
            // right wall
            if (entity.Position.X + entity.Width > Bounds.Right) {
                entity.Position = new Vector2(Bounds.Right - entity.Width, entity.Position.Y);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(-entity.direction.X, entity.direction.Y);
            }
            // top wall
            if (entity.Position.Y < Bounds.Top) {
                entity.Position = new Vector2(entity.Position.X, Bounds.Top);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(entity.direction.X, -entity.direction.Y);
            }
            // bottom wall
            if (entity.Position.Y + entity.Height > Bounds.Bottom) {
                entity.Position = new Vector2(entity.Position.X, Bounds.Bottom - entity.Height);
                if (entity.direction != Vector2.Zero)
                    entity.direction = new Vector2(entity.direction.X, -entity.direction.Y);
            }
        }
    }
}