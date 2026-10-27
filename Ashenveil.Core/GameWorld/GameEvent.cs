using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>What kind of thing happened. Grows as the game does (Attacked, Killed, Joined...).</summary>
    public enum EventType
    {
        EnteredLevel,
        LeftLevel,
        TaskDone,
        TaskFailed,
        Talked,
        Attacked,       // actor attacked target. Combat posts these; until then, the console's `event attack`
        Joined,         // actor joined a faction (target: who let them in, if anyone)
        Left,           // actor left a faction
    }

    /// <summary>
    /// Something meaningful that happened in the world - never every step. Who did what to whom
    /// is data (ids and a type), so later systems can read it in code; <see cref="Text"/> is the
    /// same thing in words, written once and neutrally ("Sir Lancelot couldn't reach the well"),
    /// for prompts and the console.
    ///
    /// Build one with <see cref="Of"/>: <see cref="EventLog.Post"/> fills in the id and the day.
    /// </summary>
    /// <param name="Where">Where it happened, in cells (feet, for something a person did).</param>
    /// <param name="TranscriptId">For a Talked event, the conversation it was.</param>
    public record GameEvent(int Id, int Day, string Level, Vector2 Where,
                            EventType Type, int? ActorId, int? TargetId, string Text,
                            int? TranscriptId = null)
    {
        /// <summary>A new event, before it's posted: no id or day yet.</summary>
        public static GameEvent Of(EventType type, string level, Vector2 where, int? actorId, int? targetId,
                                   string text, int? transcriptId = null) =>
            new(0, 0, level, where, type, actorId, targetId, text, transcriptId);
    }
}
