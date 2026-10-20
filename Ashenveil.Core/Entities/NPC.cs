using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Shared code for everything the player doesn't control: a name, a brain that
    /// decides the heading, and push-out collision. The subclass only brings its art,
    /// size and speed (see Knight).
    /// </summary>
    public abstract class NPC : Entity
    {
        protected NpcBrain Brain { get; }
        public string Name { get; }
        public string Portrait { get; set; }
        // The persona seeds who the NPC is and how they talk. There's no fixed opening line:
        // whoever starts a conversation speaks first, and the LLM writes every NPC line
        // (see ConversationSystem.Start).
        public string Persona { get; set;} = "A generic NPC.";
        protected NPC(Vector2 position, int id, string name, NpcBrain brain) : base(position, id)
        {
            Brain = brain;
            this.Name = name;
            Brain.Attach(this);
        }

        public override void Update(GameTime gameTime)
        {
            // TODO: NPC AI logic. Subclasses register at least "idle" and "walk".
            Play(Direction == Vector2.Zero ? "idle" : "walk");
            Brain.Update(gameTime);
            base.Update(gameTime);
        }

        // The brain is private to the NPC, so the World starts and ends talking through these.
        public void StartTalking() => Brain.StartTalking();
        public void StopTalking() => Brain.StopTalking();

        /// <summary>
        /// Push out of another entity, then let the brain decide how to react (a
        /// wanderer bounces away).
        /// </summary>
        public override void CheckEntityCollision(IEntity other)
        {
            Rectangle me = Bounds;
            Rectangle them = other.Bounds;
            if (!me.Intersects(them)) return;

            var (pushX, pushY) = Overlap(me, them);
            bool horizontal = Math.Abs(pushX) < Math.Abs(pushY);
            if (horizontal)
                Position = new Vector2(Position.X + pushX, Position.Y);
            else
                Position = new Vector2(Position.X, Position.Y + pushY);

            OnBlocked(horizontal);
        }

        /// <summary>Stopped by an entity or the map's edge: the brain decides how to react.</summary>
        public override void OnBlocked(bool horizontal) => Brain.OnBlocked(horizontal);
        public void GiveTask(brains.Task.ITask task)
        {
            Brain.Enqueue(task);
        }

        // Files a finished conversation in this NPC's cold-tier memory. The same transcript
        // is shared by every NPC that took part.
        public void RememberConversation(brains.Memorys.Transcript transcript)
        {
            Brain.Journal.Add(transcript);
        }

        // The brain is protected, so the World hands an end-of-conversation decision in here.
        public void ApplyDecision(brains.Dialogue.NpcDecisionDto decision, GameWorld.Locations.Location location,
                                  brains.Memorys.Transcript transcript = null)
        {
            Brain.ApplyDecision(decision, location, transcript);
        }

        // The last few remembered facts as prompt lines, so a new conversation can open
        // with the NPC remembering past ones.
        public IEnumerable<string> RecentMemories(int count) =>
            Brain.Journal.Recent(count).Select(m => m.Text);

        // Recall from the cold tier: one line per earlier conversation with `person`, oldest
        // first, the last `count` of them ("2 days ago, at Medows: ..."). Transcripts without
        // a summary (the decision never came back) are skipped - there's nothing to say.
        public IEnumerable<string> PastConversationsWith(string person, int count, int today) =>
            Brain.Journal.Transcripts
                .Where(t => t.Summary != null && t.Participants.Contains(person))
                .TakeLast(count)
                .Select(t => $"{DaysAgo(today - t.Day)}{(t.Location != null ? $", at {t.Location}" : "")}: {t.Summary}");

        private static string DaysAgo(int days) => days switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            _ => $"{days} days ago",
        };
    }
}
