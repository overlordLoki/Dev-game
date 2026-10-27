namespace Ashenveil.Core.Entities.brains.Dialogue
{
    public class DecisionRequestDto
    {
        public string npc_name { get; set; }
        public string persona { get; set; }
        public string location { get; set; }
        public string[] transcript { get; set; }
        public string[] poi_names { get; set; }
        // Factions this NPC could let the people it talked to into. Empty: it can't.
        public string[] can_admit { get; set; } = System.Array.Empty<string>();
        public int max_tasks { get; set; } = 4;
        public int max_memories { get; set; } = 5;
    }
}