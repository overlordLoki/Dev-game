using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Entities
{
    public class Player : Entity
    {
        public enum PlayerState
        {
            Idle,
            Walking,
            Talking,
        }
        public PlayerState State { get; set; } = PlayerState.Idle;
        
        public Player(Vector2 position, int id) : base(position, id)
        {
            Speed = 200f;
            AddAnimation("idle", Assets.PLAYERIDLE, 0.15);
        }

        // Keyboard-driven instead of following Direction.
        protected override void Move(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var kb = Keyboard.GetState();

            if (kb.IsKeyDown(Keys.Right)) Position = new Vector2(Position.X + Speed * delta, Position.Y);
            if (kb.IsKeyDown(Keys.Left))  Position = new Vector2(Position.X - Speed * delta, Position.Y);
            if (kb.IsKeyDown(Keys.Up))    Position = new Vector2(Position.X, Position.Y - Speed * delta);
            if (kb.IsKeyDown(Keys.Down))  Position = new Vector2(Position.X, Position.Y + Speed * delta);
        }
    }
}
