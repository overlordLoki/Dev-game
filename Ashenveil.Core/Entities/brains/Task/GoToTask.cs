using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Task
{
    public class GoToTask : ITask
    {
        public TaskState State { get; set; } = TaskState.Inactive;
        private readonly Vector2 _target;
        private const float ArriveRadius = 4f;   // "close enough" — stops the jitter

        public GoToTask(Vector2 target) => _target = target;

        public void Execute(TaskContext context)
        {
            var npc = (NPC)context.Owner;
            // Position is the frame's top-left, so aim the sprite's centre at the target -
            // the same reference point World.Spawn places a POI entity by.
            Vector2 centre = npc.Position + new Vector2(npc.Width / 2f, npc.Height / 2f);
            Vector2 toTarget = _target - centre;

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