using System;
using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Entities;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Combat
{
    /// <summary>
    /// One attack in progress: its <see cref="AttackData"/>, how long since it started, the facing
    /// it was thrown in, and who it has already hit. Each swing hits each target at most once, so
    /// a hitbox that overlaps someone for several frames still hits them once.
    /// </summary>
    public class Swing
    {
        public enum SwingPhase { Windup, Active, Recovery, Done }

        public AttackData Data { get; }
        public Entity Attacker { get; }
        /// <summary>Fixed when the swing starts: turning mid-swing doesn't move the hitbox.</summary>
        public Facing Facing { get; }
        /// <summary>Seconds since the swing started.</summary>
        public float Elapsed { get; private set; }

        // Where Elapsed was before the last Update, so a long frame that jumps right over the
        // active phase still counts as having been live (see IsLive).
        private float _previous;
        private readonly HashSet<Entity> _hit = new();

        public Swing(AttackData data, Entity attacker, Facing facing)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
            Facing = facing;
        }

        public SwingPhase Phase =>
            Elapsed < Data.Windup ? SwingPhase.Windup
            : Elapsed < Data.Windup + Data.Active ? SwingPhase.Active
            : Elapsed < Data.Duration ? SwingPhase.Recovery
            : SwingPhase.Done;

        public bool IsDone => Phase == SwingPhase.Done;

        /// <summary>
        /// Whether the hitbox was live at any point in the last Update's step of time. Not just
        /// "in the active phase now": a frame long enough to jump from windup to recovery still
        /// gets its one chance to hit.
        /// </summary>
        public bool IsLive
        {
            get
            {
                float start = Data.Windup, end = Data.Windup + Data.Active;
                return Elapsed > _previous && _previous < end && Elapsed > start;
            }
        }

        public void Update(float dt)
        {
            _previous = Elapsed;
            Elapsed += Math.Max(0f, dt);
        }

        /// <summary>
        /// The hitbox, in cells: <see cref="AttackData.Reach"/> deep and <see cref="AttackData.Width"/>
        /// wide, out from the centre of the attacker's collision box (its body, at its feet) in the
        /// swing's facing. Empty unless <see cref="IsLive"/>.
        /// </summary>
        public RectF Hitbox => IsLive ? HitboxFrom(Attacker.Bounds.Center) : RectF.Empty;

        /// <summary>Where the hitbox would be from a body centred on <paramref name="centre"/>, live or not.</summary>
        public RectF HitboxFrom(Vector2 centre)
        {
            float reach = Data.Reach, half = Data.Width / 2f;
            return Facing switch
            {
                Facing.Right => new RectF(centre.X, centre.Y - half, reach, Data.Width),
                Facing.Left  => new RectF(centre.X - reach, centre.Y - half, reach, Data.Width),
                Facing.Down  => new RectF(centre.X - half, centre.Y, Data.Width, reach),
                _            => new RectF(centre.X - half, centre.Y - reach, Data.Width, reach),
            };
        }

        public bool HasHit(Entity target) => _hit.Contains(target);

        /// <summary>Remembers <paramref name="target"/> as hit; false if it already was.</summary>
        public bool MarkHit(Entity target) => _hit.Add(target);
    }
}
