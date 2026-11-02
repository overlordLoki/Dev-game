using System.Collections.Generic;
using Ashenveil.Core.Combat;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// A wandering knight. Movement, collision and fighting come from NPC / Entity / NpcBrain;
    /// this class is just the knight's art, size, speed and attacks.
    /// </summary>
    public class Knight : NPC
    {
        // The knight takes up ~35px of an 84px-tall frame. At 1.2 cells the knight
        // himself stands about as tall as the player on screen; the rest is transparent
        // padding. Tune this, then retune the "Knight" boxes in the editor to match.
        protected override float SizeInCells => 1.2f;
        
        public Knight(Vector2 position, int id, string name, NpcBrain brain) : base(position, id, name, brain)
        {
            Portrait = Assets.KnightPortrait;
            Admits = true;   // a knight can swear someone into his own order
            Speed = 0.7f;   // cells per second (was 100px/s at 720p's 144px cells)
            SetHealth(80);  // a tuning number (docs/combat-plan.md)
            // What used to be his fixed greeting, now background he can draw on in his own words.
            Persona = "A noble knight sworn to guard these meadows. Speaks formally and calls strangers " +
                      "'traveller'. The roads have grown dangerous since the ash began to fall: wolves, " +
                      "bandits, and worse things that walk at night. He warns travellers to keep to the " +
                      "path, and that if they hear bells in the fog, they should run.";

            AddAnimation("idle",    Assets.KnightIdle,    0.12);
            AddAnimation("walk",    Assets.KnightWalk,    0.10);
            AddAnimation("run",     Assets.KnightRun,     0.08);
            AddAnimation("jump",    Assets.KnightJump,    0.10, loop: false);
            AddAnimation("attack1", Assets.KnightAttack1, 0.08, loop: false);
            AddAnimation("attack2", Assets.KnightAttack2, 0.08, loop: false);
            AddAnimation("attack3", Assets.KnightAttack3, 0.08, loop: false);
            // 6 frames in about NpcBrain.DefendSeconds, so the clip lasts as long as the guard.
            AddAnimation("defend",  Assets.KnightDefend,  0.08, loop: false);
            // 4 frames in about Entity.FlinchSeconds, so the whole flinch shows before he recovers.
            AddAnimation("hurt",    Assets.KnightHurt,    0.06, loop: false);
            AddAnimation("death",   Assets.KnightDeath,   0.10, loop: false);
        }

        // His three attack clips as a combo, thrown in turn: two quick cuts, then a heavier one.
        // The windup lands the hit about halfway through each clip. Tuning numbers.
        private static readonly AttackData[] Combo =
        {
            new(Damage: 8,  Reach: 0.8f, Width: 0.7f, Windup: 0.24f, Active: 0.12f, Recovery: 0.2f, Knockback: 0.3f, Animation: "attack1"),
            new(Damage: 8,  Reach: 0.8f, Width: 0.7f, Windup: 0.20f, Active: 0.12f, Recovery: 0.2f, Knockback: 0.3f, Animation: "attack2"),
            new(Damage: 12, Reach: 0.9f, Width: 0.8f, Windup: 0.30f, Active: 0.12f, Recovery: 0.3f, Knockback: 0.5f, Animation: "attack3"),
        };

        public override IReadOnlyList<AttackData> Attacks => Combo;
    }
}
