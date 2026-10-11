
namespace Ashenveil.Core.Entities.brains.Task
{
    public interface ITask
    {
        public enum TaskState
        {
            Inactive,
            Active,
            Completed,
        }
        TaskState State { get; set; }
        void Execute(NPC npc);

    }
}