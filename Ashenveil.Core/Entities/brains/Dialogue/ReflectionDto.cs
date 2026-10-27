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
        public List<string> rumours { get; set; } = new();             // what others told them, as hearsay
        public List<KnownDto> people { get; set; } = new();            // who the day was about, with keys
        public List<KnownDto> factions { get; set; } = new();          // and their factions
        public int max_facts { get; set; } = 3;
    }

    public class ReflectionDto
    {
        public string diary { get; set; }      // first person, 1-3 sentences
        public string[] facts { get; set; }    // lasting facts worth remembering
        public OpinionChangeDto[] opinion_changes { get; set; }
    }

    /// <summary>Someone or some faction a reflection may change its mind about, and how it feels now.</summary>
    public class KnownDto
    {
        public string key { get; set; }        // "person:0", "faction:bandit": what an opinion change names
        public string name { get; set; }       // "Loki", "bandit"
        public int opinion { get; set; }       // -100 to 100
    }

    public class OpinionChangeDto
    {
        public string about { get; set; }      // a KnownDto key
        public int by { get; set; }
        public string why { get; set; }
    }
}
