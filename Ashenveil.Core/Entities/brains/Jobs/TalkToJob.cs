using System.Collections.Generic;
using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    /// <summary>
    /// Walks the NPC up to a person (player or NPC), with a <see cref="Chase"/> in case they
    /// move, then starts a conversation with them about <see cref="Reason"/>. Completes once
    /// the conversation has started. Fails if the target can't be found in the NPC's level,
    /// can't be reached, or the conversation can't start (they're busy, say).
    /// </summary>
    public class TalkToJob : IJob
    {
        // Stop this close, in cells, feet to feet: well inside the range the player can talk from.
        private const float TalkDistance = 1f;

        public JobState State { get; set; } = JobState.Inactive;
        private readonly int _targetId;
        private readonly Chase _chase = new(TalkDistance);

        public TalkToJob(int targetId, string reason)
        {
            _targetId = targetId;
            Reason = reason;
        }

        /// <summary>Who to talk to: a player or NPC id. Saved; the route is planned afresh after loading.</summary>
        public int TargetId => _targetId;

        /// <summary>What the NPC wants to say to them, from the decision that sent it. Saved; may be null.</summary>
        public string Reason { get; }

        /// <summary>
        /// The waypoints still to walk, in cells, starting with the one being headed for.
        /// Empty until the first Execute plans the route. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints => _chase.Remaining;

        public void Execute(JobContext context)
        {
            var npc = (NPC)context.Owner;

            Entity target = context.FindPerson?.Invoke(_targetId);
            if (target == null)
            {
                Fail(npc);
                return;
            }

            switch (_chase.Step(context.Nav, npc.Feet, target.Feet, context.DeltaTime, out Vector2 heading))
            {
                case RouteStep.Arrived:
                    npc.Direction = Vector2.Zero;
                    State = context.StartConversation?.Invoke(target, Reason) == true
                        ? JobState.Completed
                        : JobState.Failed;
                    break;
                case RouteStep.GaveUp:
                    Fail(npc);
                    break;
                default:
                    npc.Direction = heading;           // Entity.Move steps
                    break;
            }
        }

        private void Fail(NPC npc)
        {
            npc.Direction = Vector2.Zero;
            State = JobState.Failed;
        }
    }
}
