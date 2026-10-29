using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains;
using Ashenveil.Core.Entities.brains.Jobs;
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

        /// <summary>The id NextId will hand out next, without using it up. For saving.</summary>
        public int PeekNextId => _nextId;

        /// <summary>Levels whose starting NPCs have already spawned. For saving.</summary>
        public IReadOnlyCollection<string> PopulatedLevels => _populated;

        /// <summary>
        /// Picks up a saved game's counter and spawned levels, before its NPCs are added back.
        /// Without this a loaded game would hand out ids the saved NPCs already have, and
        /// respawn every level's starting NPCs on top of the saved ones.
        /// </summary>
        public void Restore(int nextId, IEnumerable<string> populatedLevels)
        {
            _nextId = nextId;
            _populated.Clear();
            _populated.UnionWith(populatedLevels);
        }

        /// <summary>The NPCs in this level (none yet if it's never been visited).</summary>
        public IReadOnlyList<NPC> In(string levelName) => ListFor(levelName);

        /// <summary>
        /// Where NPCs post what happens to them (EventLog.Post). Set by the World; every NPC
        /// added from then on is wired to it. Null: they post nothing.
        /// </summary>
        public Action<GameEvent> PostEvent { get; set; }

        /// <summary>Seeds the opinions of NPCs spawned here. Set by the World; the game's own table if not.</summary>
        public FactionTable Factions { get; set; }
        /// <summary>
        /// Finds a person by id for an NPC, but only if they're in that NPC's level. Set by the
        /// World; null: nobody can be found.
        /// </summary>
        public Func<NPC, int, Entity> FindPersonNear { get; set; }

        /// <summary>Puts an NPC in a level, tells the NPC which level it's in, and where to post events.</summary>
        public void Add(Location level, NPC npc)
        {
            ListFor(level.Name).Add(npc);
            npc.Location = level;
            npc.PostEvent = PostEvent;
            // Read FindPersonNear when it's called, not now, so an NPC added before the World
            // sets it still gets it.
            npc.FindPerson = id => FindPersonNear?.Invoke(npc, id);
        }

        /// <summary>Each level's NPCs, one list per level.</summary>
        public IEnumerable<IReadOnlyList<NPC>> Levels => _byLevel.Values;

        /// <summary>Every NPC in every level.</summary>
        public IEnumerable<NPC> All => _byLevel.Values.SelectMany(list => list);

        /// <summary>The name of the level this NPC is in, or null if it isn't in any.</summary>
        public string LevelOf(NPC npc) =>
            _byLevel.FirstOrDefault(level => level.Value.Contains(npc)).Key;

        /// <summary>
        /// Finds an NPC by id across every level, not just the current one, so a lookup
        /// works even while the player is somewhere else. Null if no NPC has that id.
        /// </summary>
        public NPC Find(int id) => All.FirstOrDefault(npc => npc.Id == id);

        /// <summary>
        /// The first time a level is entered, spawn the NPCs it starts with. Safe to call on
        /// every visit - later calls do nothing.
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
                    var knight = new Knight(Vector2.Zero, NextId(), "Sir Lancelot", new KnightBrain());
                    knight.Factions.Add("guard");
                    knight.Opinions.Seed(knight.Factions, Factions ?? FactionTable.Default);
                    level.PlaceAt(knight, "Knight_spawn");
                    Add(level, knight);

                    // A short walk to the entry and back. Only if the level has both points:
                    // a missing one would otherwise crash here with a null reference.
                    PointOfInterest entry = level.Poi("entry"), home = level.Poi("Knight_spawn");
                    if (entry != null && home != null)
                    {
                        knight.GiveJob(new GoToJob(entry.StandingSpot()));
                        knight.GiveJob(new WaitJob(3f));
                        knight.GiveJob(new GoToJob(home.StandingSpot()));
                    }
                    break;
            }
        }
    }
}
