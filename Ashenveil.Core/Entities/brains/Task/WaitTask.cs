using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Task
{
    public class WaitTask : ITask
    {
        public TaskState State { get; set; } = TaskState.Inactive;
        private readonly float _seconds;
        private float _elapsed;

        public WaitTask(float seconds) => _seconds = seconds;

        public void Execute(TaskContext context)
        {
            ((NPC)context.Owner).Direction = Vector2.Zero;   // stand still while waiting
            _elapsed += context.DeltaTime;
            if (_elapsed >= _seconds) State = TaskState.Completed;
        }
    }
}