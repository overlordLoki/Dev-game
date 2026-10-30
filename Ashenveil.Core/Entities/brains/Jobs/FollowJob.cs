using System.Collections.Generic;
using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    /// <summary>
    /// Keeps the NPC's feet near a person (player or NPC), with a <see cref="Chase"/> that
    /// plans again when they move. Never completes on its own: it runs until something
    /// replaces it (see <see cref="NpcBrain.Enqueue"/>). Fails if the target can't be found
    /// in the NPC's level, or can't be reached.
    /// </summary>
    public class FollowJob : IJob
    {
        // Stop this close to the target, in cells, feet to feet: near them, not on top of them.
        private const float FollowDistance = 1f;

        public JobState State { get; set; } = JobState.Inactive;
        private readonly int _targetId;
        private readonly Chase _chase = new(FollowDistance);

        public FollowJob(int targetId) => _targetId = targetId;

        /// <summary>
        /// Who is being followed: a player or NPC id. All saving keeps; the route is planned
        /// afresh on the first Execute after loading.
        /// </summary>
        public int TargetId => _targetId;

        /// <summary>
        /// The waypoints still to walk, in cells, starting with the one being headed for.
        /// Empty until the first Execute plans the route. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints => _chase.Remaining;

        public void Execute(JobContext context)
        {
            var npc = (NPC)context.Owner;

            // Looked up every tick: the target may have walked out of the level since the last one.
            Entity target = context.FindPerson?.Invoke(_targetId);
            if (target == null)
            {
                Fail(npc);
                return;
            }

            switch (_chase.Step(context.Nav, npc.Feet, target.Feet, context.DeltaTime, out Vector2 heading))
            {
                case RouteStep.Arrived:
                    // Close enough: wait here. Still following, so the job stays Active.
                    npc.Direction = Vector2.Zero;
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
