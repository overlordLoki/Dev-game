using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Task
{
    public class GoToTask : ITask
    {
        public ITask.TaskState State { get; set; } = ITask.TaskState.Inactive;
        private Vector3 targetPosition;
        private NPC npc;

        public GoToTask(Vector3 targetPosition, NPC npc)
        {
            this.targetPosition = targetPosition;
            this.npc = npc;
        }

        public void Execute( TaskContext context)
        {
            npc = context.Owner as NPC;
            MoveToTarget();
        }
        private void MoveToTarget()
        {
            // Implement movement logic here
        }
        
    }
}