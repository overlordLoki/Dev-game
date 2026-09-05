

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Utility
{
    public class Animation
    {
        private Texture2D _texture;   // the loaded sheet
        private int _frameCount;      // e.g. 4
        private int _frameSize;       // e.g. 32
        private double _frameDuration;// seconds per frame, e.g. 0.15
        private int _currentFrame;
        private double _timer;

        public Animation(Texture2D texture, int frameCount, int frameSize, double frameDuration)
        {
            _texture = texture;
            _frameCount = frameCount;
            _frameSize = frameSize;
            _frameDuration = frameDuration;
        }
        public void Update(GameTime gameTime)
        {
            _timer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= _frameDuration)
            {
                _timer -= _frameDuration;
                _currentFrame = (_currentFrame + 1) % _frameCount;
            }
        }

        public Rectangle CurrentFrame => new Rectangle(_currentFrame * _frameSize, 0, _frameSize, _frameSize);

        public void Draw(SpriteBatch spriteBatch, Rectangle destination)
        {
            spriteBatch.Draw(_texture, destination, CurrentFrame, Color.White);
        }
        
    }
    
}