using System;
using System.Collections.Generic;
using Ashenveil.Core.Entities;

namespace Ashenveil.Core.Combat
{
    /// <summary>
    /// Resolves hits. The World calls it after movement and collision, so every hitbox uses where
    /// everyone actually ended up. It's handed the combatants rather than the whole World, like
    /// ConversationSystem, so a test can build a few entities and a swing and see who got hit.
    ///
    /// No teams: a live swing hits whatever has health and is in its hitbox (friendly fire is on).
    /// Who a brain *chooses* to attack is the brain's business (step 3 and on).
    /// </summary>
    public class CombatSystem
    {
        /// <summary>How long a target can't be hit again after a hit lands, in seconds.</summary>
        public const float InvulnerableFor = 0.4f;

        /// <summary>A hit landed: attacker, victim, damage. For aggro, memory and the HUD later.</summary>
        public event Action<Entity, Entity, int> Hit;
        /// <summary>A hit killed its target: victim, killer.</summary>
        public event Action<Entity, Entity> Died;
        /// <summary>A guard stopped a hit: attacker, defender. It still counts as an attack for aggro.</summary>
        public event Action<Entity, Entity> Blocked;

        /// <summary>How far a block pushes the defender, as a share of the swing's knockback.</summary>
        public const float BlockedKnockback = 0.25f;

        /// <summary>
        /// Every live swing among <paramref name="combatants"/> hits every one of them it overlaps,
        /// except its attacker, the dead, things with no health, and anyone it already hit.
        /// Swings and health are ticked by each entity's own Update; this only resolves.
        /// </summary>
        public void Update(IReadOnlyList<Entity> combatants)
        {
            foreach (var attacker in combatants)
            {
                var swing = attacker.Swing;
                if (swing == null || !swing.IsLive || attacker.IsDead) continue;

                var hitbox = swing.Hitbox;
                foreach (var victim in combatants)
                {
                    if (victim == attacker || victim.Health == null || victim.IsDead) continue;
                    if (swing.HasHit(victim) || !hitbox.Intersects(victim.Bounds)) continue;

                    // One chance per swing per target, landed or not (invulnerable, god mode).
                    swing.MarkHit(victim);
                    Land(swing, victim);
                }
            }
        }

        private void Land(Swing swing, Entity victim)
        {
            var data = swing.Data;

            // Caught on a guard from the front: no damage, just a small shove.
            if (victim.Blocks(swing))
            {
                victim.Knock(swing.Facing.ToVector(), data.Knockback * BlockedKnockback);
                Blocked?.Invoke(swing.Attacker, victim);
                return;
            }

            if (!victim.Health.TakeDamage(data.Damage, InvulnerableFor)) return;

            // Pushed along the swing, away from the attacker; collision still stops them at walls.
            victim.Knock(swing.Facing.ToVector(), data.Knockback);
            victim.StartFlinch();

            Hit?.Invoke(swing.Attacker, victim, data.Damage);
            if (victim.IsDead) Died?.Invoke(victim, swing.Attacker);
        }
    }
}
