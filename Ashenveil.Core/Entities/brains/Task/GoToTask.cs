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
            Vector2 toTarget = _target - npc.Position;

            if (toTarget.Length() <= ArriveRadius)
            {
                npc.Direction = Vector2.Zero;      // stop
                State = TaskState.Completed;        // tell the queue we're done
                return;
            }

            npc.Direction = Vector2.Normalize(toTarget);  // head straight at it; Entity.Move steps
        }
    }
}