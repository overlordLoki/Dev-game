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
        /// <summary>
        /// Starts a conversation an NPC walked up to have, with someone, about a reason; false
        /// if it can't now. Set by the World; null: it never can.
        /// </summary>
        public Func<NPC, Entity, string, bool> StartConversationFor { get; set; }

        /// <summary>Puts an NPC in a level, tells the NPC which level it's in, and where to post events.</summary>
        public void Add(Location level, NPC npc)
        {
            ListFor(level.Name).Add(npc);
            npc.Location = level;
            npc.PostEvent = PostEvent;
            // Read FindPersonNear when it's called, not now, so an NPC added before the World
            // sets it still gets it.
            npc.FindPerson = id => FindPersonNear?.Invoke(npc, id);
            npc.StartConversation = (who, reason) => StartConversationFor?.Invoke(npc, who, reason) == true;
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

        // What NPCs each level starts with: its file's "npcs" (Location.Spawns).
        private void SpawnStartingNpcs(Location level)
        {
            foreach (var spawn in level.Spawns) Spawn(level, spawn);
        }

        /// <summary>
        /// Brings in one NPC from a level's spawn list: its type, name and factions (the type's
        /// defaults where it gives none), standing on its point, with its starting errands.
        /// Null, and nothing spawned, for a type the game doesn't know.
        /// </summary>
        public NPC Spawn(Location level, Levels.NpcSpawnData spawn)
        {
            var at = (level.Poi(spawn.at) ?? level.Poi("default"))?.StandingSpot() ?? Vector2.Zero;
            var npc = SpawnAt(level, spawn.type, spawn.name, at, spawn.factions);
            if (npc == null) return null;

            foreach (var task in spawn.tasks ?? new List<Levels.SpawnTaskData>())
                if (JobFrom(task, level) is { } job) npc.GiveJob(job);
            return npc;
        }

        /// <summary>
        /// A new NPC of <paramref name="type"/> (any case) with its feet on <paramref name="feet"/>:
        /// the level's spawns and the console's `spawn` both come here. A blank name or no
        /// factions take the type's defaults. Null for a type the game doesn't know.
        /// </summary>
        public NPC SpawnAt(Location level, string type, string name, Vector2 feet, IEnumerable<string> factions = null)
        {
            type = NpcFactory.Canonical(type);
            if (type == null) return null;

            var npc = NpcFactory.Make(type, NextId(), string.IsNullOrWhiteSpace(name) ? NpcFactory.DefaultName(type) : name.Trim());
            npc.Factions.AddRange(factions ?? NpcFactory.DefaultFactions(type));
            npc.Opinions.Seed(npc.Factions, Factions ?? FactionTable.Default);
            npc.StandAt(feet);
            Add(level, npc);
            return npc;
        }

        // A starting errand as a job; null for an unknown type or a point the level doesn't have.
        private static IJob JobFrom(Levels.SpawnTaskData task, Location level)
        {
            switch (task.type)
            {
                case "go_to":
                    return level.Poi(task.target) is { } poi ? new GoToJob(poi.StandingSpot()) : null;
                case "wait":
                    return task.seconds > 0 ? new WaitJob(task.seconds) : null;
                case "patrol":
                    var stops = (task.targets ?? new List<string>()).Select(level.Poi).Where(p => p != null)
                                                                    .Select(p => p.StandingSpot()).ToList();
                    return stops.Distinct().Count() >= 2 ? new PatrolJob(stops, Math.Max(0f, task.seconds)) : null;
                default:
                    return null;
            }
        }
    }
}
