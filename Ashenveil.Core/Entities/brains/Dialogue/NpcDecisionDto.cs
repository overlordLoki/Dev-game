
namespace Ashenveil.Core.Entities.brains.Dialogue
{
    public class NpcDecisionDto
    {
        public string summary { get; set; }
        public string[] memories { get; set; }          // facts as text, not Memory objects
        public string behaviour { get; set; }           // "resume" | "idle" | "do_tasks"
        public bool clear_current_tasks { get; set; }
        public TaskSpecDto[] tasks { get; set; }         // specs, not IJob instances
        public string join_faction { get; set; }         // let the others in this faction; null for no
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
        public string type { get; set; }    // "go_to" | "wait" | "follow" | "talk_to" | "patrol"
        public string target { get; set; }  // POI name (for go_to), or a person's name (for follow, talk_to)
        public float seconds { get; set; }  // (for wait), or the pause at each stop (for patrol)
        public string reason { get; set; }  // what to say to them (for talk_to)
        public string[] targets { get; set; }   // POI names, in order (for patrol)
        public TaskSpecDto(string type, string target, float seconds, string reason = null, string[] targets = null)
        {
            this.type = type;
            this.target = target;
            this.seconds = seconds;
            this.reason = reason;
            this.targets = targets;
        }
    }
}