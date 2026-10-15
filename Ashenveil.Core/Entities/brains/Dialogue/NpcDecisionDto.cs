
namespace Ashenveil.Core.Entities.brains.Dialogue
{
    public class NpcDecisionDto
    {
        public string summary { get; set; }
        public string[] memories { get; set; }          // facts as text, not Memory objects
        public string behaviour { get; set; }           // "resume" | "idle" | "do_tasks"
        public bool clear_current_tasks { get; set; }
        public TaskSpecDto[] tasks { get; set; }         // specs, not ITask instances
        public NpcDecisionDto(string summary, string[] memories, string behaviour, bool clear_current_tasks, TaskSpecDto[] tasks)
        {
            this.summary = summary;
            this.memories = memories;
            this.behaviour = behaviour;
            this.clear_current_tasks = clear_current_tasks;
            this.tasks = tasks;
        }
    }

    public class TaskSpecDto
    {
        public string type { get; set; }    // "go_to" | "wait"
        public string target { get; set; }  // POI name (for go_to)
        public float seconds { get; set; }  // (for wait)
        public TaskSpecDto(string type, string target, float seconds)
        {
            this.type = type;
            this.target = target;
            this.seconds = seconds;
        }
    }
}