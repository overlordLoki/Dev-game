using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Navigation
{
    /// <summary>
    /// Walks some feet up to a goal that moves - a person - in cells: a <see cref="Route"/>
    /// to where they are, planned again whenever they've moved off it. Like Route it knows
    /// nothing about NPCs or jobs, so anything that chases (following, walking up to talk,
    /// a combat chase) can use it.
    /// </summary>
    public class Chase
    {
        private readonly float _stopDistance;
        // Plan again once the goal is this far from where the route was planned to. Kept under
        // the stop distance, so reaching the old goal always leaves the feet close enough.
        private readonly float _repathDistance;

        // A new one for each plan, so the stuck count is per stretch of the chase, not for the
        // whole of it: otherwise three stuck moments minutes apart would add up to giving up.
        private Route _route = new();
        private Vector2 _goal;                       // where the route was planned to, in cells

        /// <param name="stopDistance">How close is close enough, feet to feet, in cells.</param>
        public Chase(float stopDistance)
        {
            _stopDistance = stopDistance;
            _repathDistance = stopDistance * 0.75f;
        }

        /// <summary>
        /// The waypoints still to walk, in cells, starting with the one being headed for.
        /// Empty until the first Step plans a route. For debug drawing.
        /// </summary>
        public IReadOnlyList<Vector2> Remaining => _route.Remaining;

        /// <summary>
        /// One tick of the chase from where the <paramref name="feet"/> are now towards
        /// <paramref name="goal"/>'s current spot. Arrived once within the stop distance (and
        /// it stays Arrived for as long as they are); GaveUp if there's no way through or the
        /// route got stuck too often. Walking sets <paramref name="heading"/>; the others leave it zero.
        /// </summary>
        public RouteStep Step(NavGrid nav, Vector2 feet, Vector2 goal, float dt, out Vector2 heading)
        {
            heading = Vector2.Zero;
            if (Vector2.Distance(feet, goal) <= _stopDistance) return RouteStep.Arrived;

            // Plan on the first step, and again whenever the goal has moved off the old one.
            if (!_route.IsPlanned || Vector2.Distance(goal, _goal) > _repathDistance)
            {
                _route = new Route();
                _goal = goal;
                if (!_route.Plan(nav, feet, _goal)) return RouteStep.GaveUp;
            }

            // Arrived here means on the old goal, which is within the stop distance of the new one.
            return _route.Step(feet, dt, out heading);
        }
    }
}
