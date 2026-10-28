using System.Collections.Generic;
using Ashenveil.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    /// <summary>
    /// Walks the NPC's feet to a point. With a <see cref="NavGrid"/> it routes round
    /// obstacles with a <see cref="Route"/>, one waypoint at a time; without one
    /// (tests, or a level with no grid) it heads straight at the target. Fails if there's
    /// no way through, or if it keeps getting stuck.
    /// </summary>
    public class GoToJob : IJob
    {
        public JobState State { get; set; } = JobState.Inactive;
        private readonly Vector2 _target;            // in cells
        private readonly Route _route = new();       // the waypoints and stuck timers

        public GoToJob(Vector2 target) => _target = target;

        /// <summary>
        /// Where the feet are headed, in cells. All saving keeps: the route and stuck timers
        /// aren't saved, because the first Execute after loading plans again from wherever the
        /// NPC is, as it does for any job that's just starting.
        /// </summary>
        public Vector2 Target => _target;

        /// <summary>
        /// The waypoints still to walk, in cells, starting with the one being headed for.
        /// Empty until the first Execute plans the route. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints => _route.Remaining;

        public void Execute(JobContext context)
        {
            var npc = (NPC)context.Owner;

            // Plan on the first tick, not in the constructor: the job may wait in the queue
            // behind others, so where the NPC is now is only known once it starts.
            if (!_route.IsPlanned && !_route.Plan(context.Nav, npc.Feet, _target))
            {
                Fail(npc);
                return;
            }

            // Walk the feet to the target - the same point Location.PlaceAt stands an entity
            // on a POI by, so walking to a point ends exactly where spawning on it would.
            switch (_route.Step(npc.Feet, context.DeltaTime, out Vector2 heading))
            {
                case RouteStep.Arrived:
                    npc.Direction = Vector2.Zero;      // stop
                    State = JobState.Completed;        // tell the queue we're done
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