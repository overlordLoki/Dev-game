using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Task
{
    public class GoToTask : ITask
    {
        public TaskState State { get; set; } = TaskState.Inactive;
        private readonly Vector2 _target;            // in cells
        private const float ArriveRadius = 0.03f;    // "close enough", in cells (~4px at 720p) — stops the jitter

        public GoToTask(Vector2 target) => _target = target;

        public void Execute(TaskContext context)
        {
            var npc = (NPC)context.Owner;
            // Walk the feet to the target - the same point Location.PlaceAt stands an entity on
            // a POI by, so walking to a point ends exactly where spawning on it would.
            Vector2 toTarget = _target - npc.Feet;

            if (toTarget.Length() <= ArriveRadius)
            {
                npc.Direction = Vector2.Zero;      // stop
                State = TaskState.Completed;        // tell the queue we're done
                return;
            }

            npc.Direction = Vector2.Normalize(toTarget);  // head straight at it; Entity.Move steps
        }
        //TODO: Implement A* pathfinding for NPCs to navigate around obstacles and find the best path to the target.
        private void AStar()
        {

        }
    }
}
