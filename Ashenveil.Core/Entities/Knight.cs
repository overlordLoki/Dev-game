using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// A wandering knight. Movement and collision come from NPC / Entity; this class is
    /// just the knight's art, size and speed.
    /// </summary>
    public class Knight : NPC
    {
        // The knight takes up ~35px of an 84px-tall frame. At 1.2 cells the knight
        // himself stands about as tall as the player on screen; the rest is transparent
        // padding. Tune this, then retune the "Knight" boxes in the editor to match.
        protected override float SizeInCells => 1.2f;

        public Knight(Vector2 position, int id) : base(position, id)
        {
            Speed = 100f;

            AddAnimation("idle",    Assets.KnightIdle,    0.12);
            AddAnimation("walk",    Assets.KnightWalk,    0.10);
            AddAnimation("run",     Assets.KnightRun,     0.08);
            AddAnimation("jump",    Assets.KnightJump,    0.10, loop: false);
            AddAnimation("attack1", Assets.KnightAttack1, 0.08, loop: false);
            AddAnimation("attack2", Assets.KnightAttack2, 0.08, loop: false);
            AddAnimation("attack3", Assets.KnightAttack3, 0.08, loop: false);
            AddAnimation("defend",  Assets.KnightDefend,  0.10, loop: false);
            AddAnimation("hurt",    Assets.KnightHurt,    0.10, loop: false);
            AddAnimation("death",   Assets.KnightDeath,   0.10, loop: false);
        }
    }
}
