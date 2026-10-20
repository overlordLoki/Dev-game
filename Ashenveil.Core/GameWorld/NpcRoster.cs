using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
using Ashenveil.Core.Entities.brains.Task;
using Ashenveil.Core.GameWorld.Locations;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// Which NPCs exist, and which level each one is in. Every level keeps its own list,
    /// alive across level changes, so walking back into a level gives you the same NPCs
    /// (and their memories) rather than fresh ones.
    ///
    /// It doesn't know which level is being played - that's the World's job. Anything that
    /// needs a level is given one.
    /// </summary>
    public class NpcRoster
    {
        // Every level's NPCs, by level name.
        private readonly Dictionary<string, List<NPC>> _byLevel = new();
        // Levels whose starting NPCs have already been spawned, so a return visit doesn't respawn them.
        private readonly HashSet<string> _populated = new();
        // Hands out NPC ids. Only ever counts up, so ids stay unique across levels and
        // despawns for the whole session. 0 is the player; NPCs start at 1.
        private int _nextId = 1;

        /// <summary>A fresh, never-used NPC id.</summary>
        public int NextId() => _nextId++;

        /// <summary>The NPCs in this level (none yet if it's never been visited).</summary>
        public IReadOnlyList<NPC> In(string levelName) => ListFor(levelName);

        /// <summary>Puts an NPC in a level.</summary>
        public void Add(string levelName, NPC npc) => ListFor(levelName).Add(npc);

        /// <summary>Every NPC in every level.</summary>
        public IEnumerable<NPC> All => _byLevel.Values.SelectMany(list => list);

        /// <summary>
        /// Finds an NPC by id across every level, not just the current one, so a lookup
        /// works even while the player is somewhere else. Null if no NPC has that id.
        /// </summary>
        public NPC Find(int id) => All.FirstOrDefault(npc => npc.Id == id);

        /// <summary>
        /// The first time a level is entered, spawn the NPCs it starts with. Safe to call on
        /// every visit - later calls do nothing. Needs Layout to have a cell size, since
        /// spawning turns points of interest into pixel positions.
        /// </summary>
        public void Populate(Location level)
        {
            if (!_populated.Add(level.Name)) return;   // Add returns false if it was already there
            SpawnStartingNpcs(level);
        }

        private List<NPC> ListFor(string levelName)
        {
            if (!_byLevel.TryGetValue(levelName, out var list))
            {
                list = new List<NPC>();
                _byLevel[levelName] = list;
            }
            return list;
        }

        // What NPCs each level starts with. Hardcoded per level for now; later this reads
        // from the level's JSON the same way POIs and exits already do.
        private void SpawnStartingNpcs(Location level)
        {
            switch (level.Name)
            {
                case "Medows":
                    var knight = new Knight(new Vector2(200, 200), NextId(), "Sir Lancelot", new KnightBrain());
                    level.PlaceAt(knight, "Knight_spawn");
                    Add(level.Name, knight);

                    // A short walk to the entry and back. Only if the level has both points:
                    // a missing one would otherwise crash here with a null reference.
                    PointOfInterest entry = level.Poi("entry"), home = level.Poi("Knight_spawn");
                    if (entry != null && home != null)
                    {
                        knight.GiveTask(new GoToTask(entry.ToPixel()));
                        knight.GiveTask(new WaitTask(3f));
                        knight.GiveTask(new GoToTask(home.ToPixel()));
                    }
                    break;
            }
        }
    }
}
