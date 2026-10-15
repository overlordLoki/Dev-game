namespace Ashenveil.Core.Entities.brains.Dialogue
{
    public class DecisionRequestDto
    {
        public string npc_name { get; set; }
        public string persona { get; set; }
        public string location { get; set; }
        public string[] transcript { get; set; }
        public string[] poi_names { get; set; }
        public int max_tasks { get; set; } = 4;
        public int max_memories { get; set; } = 5;
    }
}