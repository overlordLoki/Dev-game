
using System.Numerics;

namespace Ashenveil.Core.Entities.brains.Task
{
    public class WaitTask : ITask
    {
        public TaskState State { get; set; } = TaskState.Inactive;
        public readonly float _seconds;
        public float _elapsed;
        
        public void Execute(TaskContext context)
        {
            ((NPC)context.Owner).Direction = Vector2.Zero;   // stand still while waiting
            _elapsed += context.DeltaTime;
            if (_elapsed >= _seconds) State = TaskState.Completed;
            }
        public WaitTask(float seconds)
        {
            State = TaskState.Inactive;
        }
    }
}