using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.GameWorld;

namespace Ashenveil.Core.Entities.brains
{
    /// <summary>
    /// A personal opinion of one person: the score, and the last reason it changed
    /// ("he attacked me at the well"), for the conversation prompt. Null reason: set by hand.
    /// </summary>
    public record PersonalOpinion(int Score, string Why);

    /// <summary>
    /// What one NPC thinks of people and of whole factions, -100 to 100. Kept in the brain next
    /// to the Journal, and saved with it.
    ///
    /// Opinions of factions are seeded from the faction table when the NPC is created, then are
    /// the NPC's own: two guards can come to disagree about bandits.
    ///
    /// Opinions of people only exist once something has changed one. Until then the NPC goes by
    /// its faction view of that person (NPC.FactionViewOf), and keeps tracking it - a stranger
    /// still reacts when your standing changes. The first change starts the entry at whatever
    /// the faction view was right then; after that it's independent, and never fades back.
    /// </summary>
    public class Opinions
    {
        private readonly Dictionary<int, PersonalOpinion> _people = new();
        private readonly Dictionary<string, int> _factions = new();

        /// <summary>Personal opinions, by person id.</summary>
        public IReadOnlyDictionary<int, PersonalOpinion> People => _people;

        /// <summary>Opinions of factions, by faction name.</summary>
        public IReadOnlyDictionary<string, int> Factions => _factions;

        /// <summary>
        /// Sets this NPC's view of every faction to what its own factions think of it (the lowest,
        /// if it's in several). A new NPC's starting point; never called again for that NPC.
        /// </summary>
        public void Seed(IEnumerable<string> myFactions, FactionTable table)
        {
            var mine = myFactions.ToList();
            foreach (var faction in table.Names)
                _factions[faction] = FactionTable.Lowest(mine, m => table.View(m, faction));
        }

        /// <summary>What this NPC thinks of a faction; 0 for one it has no view of.</summary>
        public int OfFaction(string faction) => _factions.TryGetValue(faction, out int score) ? score : 0;

        public void SetFaction(string faction, int score) => _factions[faction] = Attitudes.Clamp(score);

        public void ChangeFaction(string faction, int by) => SetFaction(faction, OfFaction(faction) + by);

        /// <summary>This NPC's own opinion of a person, if it has one yet.</summary>
        public bool TryPersonal(int personId, out PersonalOpinion opinion) => _people.TryGetValue(personId, out opinion);

        public void SetPerson(int personId, int score, string why = null) =>
            _people[personId] = new PersonalOpinion(Attitudes.Clamp(score), why);

        /// <summary>
        /// Moves the opinion of a person by <paramref name="by"/>. With no entry yet, it starts at
        /// <paramref name="startFrom"/> (the faction view right now). Returns the new score.
        /// </summary>
        public int Change(int personId, int startFrom, int by, string why = null)
        {
            int before = _people.TryGetValue(personId, out var existing) ? existing.Score : startFrom;
            SetPerson(personId, before + by, why ?? existing?.Why);
            return _people[personId].Score;
        }
    }
}
