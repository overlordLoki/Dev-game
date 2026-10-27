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

        // What a new player is called until there's a way to choose a name.
        public const string DefaultName = "Traveller";

        // What the player is called: in the transcript, and in what the LLM is told. Can
        // change, so memories never point at it - they point at Id, which doesn't.
        public string Name { get; set; }

        public Player(Vector2 position, int id, string name = DefaultName) : base(position, id)
        {
            Name = name;
            Speed = 1.4f;   // cells per second (was 200px/s at 720p's 144px cells)
            AddAnimation("idle", Assets.PLAYERIDLE, 0.15);
        }

        // Keyboard-driven instead of following Direction.
        protected override void Move(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var kb = AshenveilGame.Input.Keyboard;

            if (kb.IsKeyDown(Keys.Right)) Position = new Vector2(Position.X + Speed * delta, Position.Y);
            if (kb.IsKeyDown(Keys.Left))  Position = new Vector2(Position.X - Speed * delta, Position.Y);
            if (kb.IsKeyDown(Keys.Up))    Position = new Vector2(Position.X, Position.Y - Speed * delta);
            if (kb.IsKeyDown(Keys.Down))  Position = new Vector2(Position.X, Position.Y + Speed * delta);
        }
    }
}
