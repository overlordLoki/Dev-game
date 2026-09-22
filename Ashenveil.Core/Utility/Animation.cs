using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Utility
{
    public class Animation
    {
        private Texture2D _texture;   // the loaded sheet
        private int _frameCount;      // e.g. 4
        private double _frameDuration;// seconds per frame, e.g. 0.15
        private int _currentFrame;
        private double _timer;

        // Frames sit side by side in one row; they don't have to be square (knight: 96x84).
        public int FrameWidth { get; }
        public int FrameHeight { get; }

        // One-shot animations (attack, hurt, death) set this false: they stop on the
        // last frame and report IsFinished instead of starting over.
        public bool Loop { get; init; } = true;
        public bool IsFinished { get; private set; }

        public Animation(Texture2D texture, int frameCount, int frameWidth, int frameHeight, double frameDuration)
        {
            _texture = texture;
            _frameCount = frameCount;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            _frameDuration = frameDuration;
        }

        public void Update(GameTime gameTime)
        {
            if (IsFinished) return;

            _timer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= _frameDuration)
            {
                _timer -= _frameDuration;
                if (_currentFrame < _frameCount - 1) _currentFrame++;
                else if (Loop) _currentFrame = 0;
                else IsFinished = true;
            }
        }

        // Back to frame 0, e.g. when an attack is started again.
        public void Reset()
        {
            _currentFrame = 0;
            _timer = 0;
            IsFinished = false;
        }

        public Rectangle CurrentFrame => new Rectangle(_currentFrame * FrameWidth, 0, FrameWidth, FrameHeight);

        public void Draw(SpriteBatch spriteBatch, Rectangle destination, SpriteEffects effects = SpriteEffects.None)
        {
            spriteBatch.Draw(_texture, destination, CurrentFrame, Color.White, 0f, Vector2.Zero, effects, 0f);
        }
    }
}
