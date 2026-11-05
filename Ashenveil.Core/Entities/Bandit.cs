using System.Collections.Generic;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities.brains;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// The first enemy: a road bandit. There's no bandit art yet, so it's the knight's sheets
    /// darkened (docs/combat-plan.md, open question 3) with its own health, speed and one heavy
    /// swing. Unlike named NPCs, a bandit dies for good.
    /// </summary>
    public class Bandit : Knight
    {
        /// <summary>What a bandit is called when the level doesn't name it.</summary>
        public const string DefaultName = "Bandit";

        public Bandit(Vector2 position, int id, string name, NpcBrain brain) : base(position, id, name, brain)
        {
            Admits = false;
            Speed = 0.75f;
            SetHealth(45);   // tuning numbers (docs/combat-plan.md)
            Persona = "A ragged bandit who preys on travellers on the meadow roads. Rough, greedy and quick " +
                      "to threaten; respects only strength, and hates the knights who hunt his kind.";
        }

        public override bool DiesForGood => true;

        // The knight's body, so his size; its own "Bandit" boxes in bounds.json (copied from his),
        // so the editor, which reads this file, can tune it on its own.
        protected override float SizeInCells => 1.2f;

        // Darker and dirtier than the knight, so the two can't be mistaken for each other.
        protected override Color Tint => new(140, 105, 95);

        // One slow, heavy cut: easier to see coming than the knight's combo.
        private static readonly AttackData[] Swings =
        {
            new(Damage: 9, Reach: 0.8f, Width: 0.7f, Windup: 0.35f, Active: 0.12f, Recovery: 0.35f, Knockback: 0.4f, Animation: "attack3"),
        };

        public override IReadOnlyList<AttackData> Attacks => Swings;
    }
}
