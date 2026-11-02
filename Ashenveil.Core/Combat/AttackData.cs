namespace Ashenveil.Core.Combat
{
    /// <summary>
    /// One kind of attack, as numbers: how hard, how far, and how long each phase lasts.
    /// Time-based, not driven by animation frames: tests run with no textures, and frame
    /// lengths change whenever the art does. The clip is just picked to match.
    ///
    /// <code>
    ///  windup ──────► active ──────► recovery ──► done
    ///  (telegraph,    (hitbox is     (can't move
    ///   can't move)    live)          or swing yet)
    /// </code>
    /// </summary>
    /// <param name="Damage">Health taken from each target hit.</param>
    /// <param name="Reach">How far in front the hitbox goes, in cells.</param>
    /// <param name="Width">How wide the hitbox is across the facing, in cells.</param>
    /// <param name="Windup">Seconds before the hitbox goes live.</param>
    /// <param name="Active">Seconds the hitbox is live.</param>
    /// <param name="Recovery">Seconds after, before moving or swinging again.</param>
    /// <param name="Knockback">How far a target is pushed, in cells.</param>
    /// <param name="Animation">The clip to play: the player's "attack" (+ "_down" etc.), the knight's "attack1".</param>
    public record AttackData(int Damage, float Reach, float Width,
                             float Windup, float Active, float Recovery,
                             float Knockback, string Animation)
    {
        /// <summary>The whole swing, start to done, in seconds.</summary>
        public float Duration => Windup + Active + Recovery;
    }
}
