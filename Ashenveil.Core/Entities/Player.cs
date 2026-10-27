using System.Collections.Generic;
using Ashenveil.Core.GameWorld;
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

        // How much joining a faction raises standing with it, and leaving lowers it.
        public const int JoinBonus = 30, LeavePenalty = 30;
        // Joining (or leaving) moves standing with every other faction by this share of what
        // that faction thinks of the one joined: join the guard, and bandits like you less.
        public const int ShiftShare = 4;

        /// <summary>The factions this player has joined. A new player is a "traveller".</summary>
        public List<string> Factions { get; } = new();

        /// <summary>
        /// How each faction as a whole sees this player, -100 to 100: their reputation with it,
        /// member or not. It's where an NPC's opinion of this player starts.
        /// </summary>
        public Dictionary<string, int> Standing { get; } = new();

        /// <summary>This player's standing with a faction; 0 for one it has none with.</summary>
        public int StandingWith(string faction) => Standing.TryGetValue(faction, out int score) ? score : 0;

        public void ChangeStanding(string faction, int by) =>
            Standing[faction] = Attitudes.Clamp(StandingWith(faction) + by);

        /// <summary>
        /// A new player: a traveller, with each faction's standing at what that faction thinks of
        /// travellers (guards neutral, bandits wary).
        /// </summary>
        public void SeedStanding(FactionTable table)
        {
            Factions.Clear();
            Factions.Add(FactionTable.StartingFaction);
            Standing.Clear();
            foreach (var faction in table.Names)
                Standing[faction] = Attitudes.Clamp(table.View(faction, FactionTable.StartingFaction));
        }

        /// <summary>
        /// Joins a faction: standing with it goes up, and standing with every other faction
        /// shifts by a share of what that one thinks of it. A change, not a reset: what was
        /// earned stays. False (and nothing changed) if already a member.
        /// </summary>
        public bool Join(string faction, FactionTable table)
        {
            if (Factions.Contains(faction)) return false;
            Factions.Add(faction);
            ChangeStanding(faction, JoinBonus);
            foreach (var other in table.Names)
                if (other != faction) ChangeStanding(other, table.View(other, faction) / ShiftShare);
            return true;
        }

        /// <summary>Leaving: the other way round, with a penalty with the faction left. False if not a member.</summary>
        public bool Leave(string faction, FactionTable table)
        {
            if (!Factions.Remove(faction)) return false;
            ChangeStanding(faction, -LeavePenalty);
            foreach (var other in table.Names)
                if (other != faction) ChangeStanding(other, -table.View(other, faction) / ShiftShare);
            return true;
        }

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
