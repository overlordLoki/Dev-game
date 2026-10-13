
namespace Ashenveil.Core.Entities.brains.Task
{
    public class WaitTask : ITask
    {
        public TaskState State { get; set; } = TaskState.Inactive;

        public void Execute(TaskContext context)
        {
            // Implement wait logic here
        }
        public WaitTask()
        {
            State = TaskState.Inactive;
        }
    }
}