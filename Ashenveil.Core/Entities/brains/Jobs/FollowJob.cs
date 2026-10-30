using System.Collections.Generic;
using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    /// <summary>
    /// Keeps the NPC's feet near a person (player or NPC), walking a <see cref="Route"/>
    /// to wherever they are now and planning again when they move. Never completes on its
    /// own: it runs until something replaces it (see <see cref="NpcBrain.Enqueue"/>). Fails
    /// if the target can't be found in the NPC's level, or can't be reached.
    /// </summary>
    public class FollowJob : IJob
    {
        // Stop this close to the target, in cells, feet to feet: near them, not on top of them.
        private const float FollowDistance = 1f;
        // Plan again once the target is this far from where the route was planned to. Kept
        // under FollowDistance, so arriving at the old goal always leaves the NPC close enough.
        private const float RepathDistance = 0.75f;

        public JobState State { get; set; } = JobState.Inactive;
        private readonly int _targetId;
        // A new one for each plan, so the stuck count is per stretch of the chase, not for the
        // whole follow: otherwise three stuck moments minutes apart would add up to giving up.
        private Route _route = new();
        private Vector2 _goal;                       // where the route was planned to, in cells

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
        public IReadOnlyList<Vector2> RemainingWaypoints => _route.Remaining;

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

            // Close enough: wait here. Still following, so the job stays Active.
            if (Vector2.Distance(npc.Feet, target.Feet) <= FollowDistance)
            {
                npc.Direction = Vector2.Zero;
                return;
            }

            // Plan on the first tick, and again whenever the target has moved off the goal.
            if (!_route.IsPlanned || Vector2.Distance(target.Feet, _goal) > RepathDistance)
            {
                _route = new Route();
                _goal = target.Feet;
                if (!_route.Plan(context.Nav, npc.Feet, _goal))
                {
                    Fail(npc);
                    return;
                }
            }

            switch (_route.Step(npc.Feet, context.DeltaTime, out Vector2 heading))
            {
                case RouteStep.Arrived:
                    // On where the target was. Not done: wait for them to move off the goal.
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
