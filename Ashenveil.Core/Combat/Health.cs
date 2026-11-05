using System;

namespace Ashenveil.Core.Combat
{
    /// <summary>
    /// How much more something can take. A component, not a base class: anything that can be hurt
    /// has one (Entity.Health), so a training dummy or a barrel can get one later without being an
    /// Entity subclass. Null on things that can't be hurt.
    ///
    /// It only counts: what dying *means* (an NPC stopping, the death clip) is up to the owner,
    /// which listens to <see cref="Died"/> and <see cref="Revived"/>.
    /// </summary>
    public class Health
    {
        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        /// <summary>0 to 1, straight into a ProgressBar.</summary>
        public float Fraction => (float)Current / Max;

        /// <summary>
        /// Seconds left of "just got hit, can't be hit again". Stops one swing that overlaps for
        /// several frames from hitting several times, and gives the victim a moment.
        /// </summary>
        public float Invulnerable { get; private set; }

        /// <summary>Takes no damage at all: the console's `god`. Kill still works.</summary>
        public bool God { get; set; }

        /// <summary>Raised once, when Current reaches 0.</summary>
        public event Action Died;
        /// <summary>Raised when healing brings it back from 0 (the console's `heal`; knock-outs later).</summary>
        public event Action Revived;

        public Health(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max), "Max health must be above 0.");
            Max = max;
            Current = max;
        }

        /// <summary>
        /// Loses <paramref name="amount"/>, never below 0, then can't be hit again for
        /// <paramref name="invulnerableFor"/> seconds. False if it didn't land: already dead,
        /// still invulnerable, God, or nothing to take.
        /// </summary>
        public bool TakeDamage(int amount, float invulnerableFor = 0f)
        {
            if (amount <= 0 || IsDead || God || Invulnerable > 0) return false;

            Current = Math.Max(0, Current - amount);
            Invulnerable = Math.Max(0f, invulnerableFor);
            if (IsDead) Died?.Invoke();
            return true;
        }

        /// <summary>Gains <paramref name="amount"/>, never above Max. Healing the dead brings them back.</summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || Current == Max) return;

            bool wasDead = IsDead;
            Current = Math.Min(Max, Current + amount);
            if (wasDead) Revived?.Invoke();
        }

        /// <summary>Straight to 0, whatever God or invulnerability say. The console's `kill`.</summary>
        public void Kill()
        {
            if (IsDead) return;
            Current = 0;
            Died?.Invoke();
        }

        /// <summary>
        /// Puts back health a save had (a fresh Health, so it starts full): <paramref name="current"/>
        /// clamped to Max, and 0 or less is <see cref="Kill"/>, so the owner lies down as it did.
        /// </summary>
        public void Restore(int current)
        {
            if (current <= 0) { Kill(); return; }
            bool wasDead = IsDead;
            Current = Math.Min(Max, current);
            if (wasDead) Revived?.Invoke();
        }

        /// <summary>Counts <see cref="Invulnerable"/> down.</summary>
        public void Update(float dt) => Invulnerable = Math.Max(0f, Invulnerable - dt);
    }
}
