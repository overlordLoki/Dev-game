using System.Collections.Generic;
using Ashenveil.Core.Combat;
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
            Attacking,  // a swing in progress: rooted to the spot until it's done
            Dead,   // health reached 0: can't move or talk; healing brings them back
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

        /// <summary>Full health. A tuning number (docs/combat-plan.md).</summary>
        public const int MaxHealth = 100;

        public Player(Vector2 position, int id, string name = DefaultName) : base(position, id)
        {
            Name = name;
            Speed = 1.4f;   // cells per second (was 200px/s at 720p's 144px cells)
            SetHealth(MaxHealth);

            // One clip per action and facing, named "<action>_<down|up|side>" (see Clip).
            // Attack, hurt and death are one-shots: they stop on their last frame.
            AddAnimation("idle_down",   Assets.PlayerIdleDown,   0.15);
            AddAnimation("idle_up",     Assets.PlayerIdleUp,     0.15);
            AddAnimation("idle_side",   Assets.PlayerIdleSide,   0.15);
            AddAnimation("walk_down",   Assets.PlayerWalkDown,   0.10);
            AddAnimation("walk_up",     Assets.PlayerWalkUp,     0.10);
            AddAnimation("walk_side",   Assets.PlayerWalkSide,   0.10);
            AddAnimation("attack_down", Assets.PlayerAttackDown, 0.08, loop: false);
            AddAnimation("attack_up",   Assets.PlayerAttackUp,   0.08, loop: false);
            AddAnimation("attack_side", Assets.PlayerAttackSide, 0.08, loop: false);
            AddAnimation("hurt_down",   Assets.PlayerHurtDown,   0.10, loop: false);
            AddAnimation("hurt_up",     Assets.PlayerHurtUp,     0.10, loop: false);
            AddAnimation("hurt_side",   Assets.PlayerHurtSide,   0.10, loop: false);
            AddAnimation("death_down",  Assets.PlayerDeathDown,  0.12, loop: false);
            AddAnimation("death_up",    Assets.PlayerDeathUp,    0.12, loop: false);
            AddAnimation("death_side",  Assets.PlayerDeathSide,  0.12, loop: false);
            Play(Clip("idle", Facing));
        }

        /// <summary>The clip for an action in a facing: ("walk", Up) is "walk_up", Left and Right are "_side".</summary>
        public static string Clip(string action, Facing facing) => facing switch
        {
            Facing.Up   => action + "_up",
            Facing.Down => action + "_down",
            _           => action + "_side",
        };

        // The player's side sheets are drawn facing left (unlike the knight's), so they flip
        // for right; the up and down sheets are never flipped.
        protected override bool FlipSprite => Facing == Facing.Right;

        /// <summary>The player's one attack, on Space. Tuning numbers (docs/combat-plan.md, step 2).</summary>
        public static readonly AttackData Sword = new(Damage: 10, Reach: 0.6f, Width: 0.7f,
            Windup: 0.08f, Active: 0.12f, Recovery: 0.2f, Knockback: 0.4f, Animation: "attack");

        // Space is edge-triggered: one press is one swing, holding it down doesn't keep swinging.
        private bool _attackWasDown;
        // Whether Move walked this frame. Not "the position changed": a knockback moves you too.
        private bool _walked;

        public override void Update(GameTime gameTime)
        {
            bool attackDown = AshenveilGame.Input.Keyboard.IsKeyDown(Keys.Space);
            if (attackDown && !_attackWasDown && State != PlayerState.Talking) StartSwing(Sword);
            _attackWasDown = attackDown;

            _walked = false;
            base.Update(gameTime);

            // Talking is set from outside and left alone, and Dead only changes when health does
            // (OnDied / OnRevived). Otherwise it's whatever happened this frame.
            if (State == PlayerState.Dead) return;
            if (State != PlayerState.Talking)
                State = Swing != null ? PlayerState.Attacking : _walked ? PlayerState.Walking : PlayerState.Idle;

            // A flinch shows over everything; the attack clip is the swing's facing, which is fixed.
            if (IsFlinching) Play(Clip("hurt", Facing));
            else if (Swing != null) Play(Clip(Swing.Data.Animation, Swing.Facing));
            else Play(Clip(State == PlayerState.Walking ? "walk" : "idle", Facing));
        }

        // Dying isn't the end: the World stands them back up on the spawn point (World.RespawnSeconds).
        public override bool DiesForGood => false;

        protected override void OnDied()
        {
            base.OnDied();
            State = PlayerState.Dead;
            Play(Clip("death", Facing));   // one-shot: stops on the last frame
        }

        protected override void OnRevived()
        {
            State = PlayerState.Idle;
            Play(Clip("idle", Facing));
        }

        // Keyboard-driven instead of following Direction. The held arrows make a direction
        // that's normalised, so a diagonal is as fast as a straight line rather than ~1.4x.
        protected override void Move(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var kb = AshenveilGame.Input.Keyboard;

            var dir = Vector2.Zero;
            if (kb.IsKeyDown(Keys.Right)) dir.X += 1;
            if (kb.IsKeyDown(Keys.Left))  dir.X -= 1;
            if (kb.IsKeyDown(Keys.Up))    dir.Y -= 1;
            if (kb.IsKeyDown(Keys.Down))  dir.Y += 1;
            if (dir == Vector2.Zero || delta <= 0) return;

            dir.Normalize();
            Position += dir * Speed * delta;
            _walked = true;
        }
    }
}
