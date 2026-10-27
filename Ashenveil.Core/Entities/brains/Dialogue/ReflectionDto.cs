using System.Collections.Generic;

namespace Ashenveil.Core.Entities.brains.Dialogue
{
    // Mirrors main.py's ReflectionRequest / ReflectionResponse: one NPC looking back on one day.
    public class ReflectionRequestDto
    {
        public string npc_name { get; set; }
        public string persona { get; set; }
        public string date { get; set; }                               // "Spring 3, Year 1"
        public List<string> events { get; set; } = new();              // what it saw happen, oldest first
        public List<string> conversations { get; set; } = new();       // that day's conversation summaries
        public int max_facts { get; set; } = 3;
    }

    public class ReflectionDto
    {
        public string diary { get; set; }      // first person, 1-3 sentences
        public string[] facts { get; set; }    // lasting facts worth remembering
    }
}
