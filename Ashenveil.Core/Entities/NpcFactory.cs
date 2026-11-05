using System;
using System.Collections.Generic;
using Ashenveil.Core.Entities.brains;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Builds an NPC from the name of its type ("Knight"), with the brain that type comes
    /// with. Loading a save, spawning from a level's JSON and the console's `spawn` all use it.
    /// One line per NPC type: a new type that isn't listed here can't be loaded or spawned.
    /// </summary>
    public static class NpcFactory
    {
        // The key is the class name, which is what SaveMapper writes as an NPC's type. If a
        // class is ever renamed, keep its old name here too, or older saves lose that NPC.
        private static readonly Dictionary<string, Func<int, string, NPC>> Makers = new()
        {
            [nameof(Knight)] = (id, name) => new Knight(Vector2.Zero, id, name, new KnightBrain()),
            [nameof(Bandit)] = (id, name) => new Bandit(Vector2.Zero, id, name, new EnemyBrain()),
        };

        // What each type is called, and belongs to, when whoever spawns it doesn't say.
        private static readonly Dictionary<string, (string name, string[] factions)> Defaults = new()
        {
            [nameof(Knight)] = ("Knight", new[] { "guard" }),
            [nameof(Bandit)] = (Bandit.DefaultName, new[] { "bandit" }),
        };

        /// <summary>Every type that can be made, for the console and the editor.</summary>
        public static IEnumerable<string> Types => Makers.Keys;

        public static bool Knows(string type) => type != null && Makers.ContainsKey(type);

        /// <summary>The type's name as written, ignoring case ("bandit" is "Bandit"); null if there's no such type.</summary>
        public static string Canonical(string type)
        {
            foreach (var known in Makers.Keys)
                if (string.Equals(known, type, StringComparison.OrdinalIgnoreCase)) return known;
            return null;
        }

        /// <summary>What an NPC of this type is called when nobody names it.</summary>
        public static string DefaultName(string type) => Defaults.TryGetValue(type ?? "", out var d) ? d.name : type;

        /// <summary>The factions an NPC of this type belongs to when nobody says otherwise.</summary>
        public static IReadOnlyList<string> DefaultFactions(string type) =>
            Defaults.TryGetValue(type ?? "", out var d) ? d.factions : Array.Empty<string>();

        /// <summary>
        /// A new NPC of this type at the origin (the caller stands it where it belongs), or
        /// null for a type the game doesn't know.
        /// </summary>
        public static NPC Make(string type, int id, string name) =>
            type != null && Makers.TryGetValue(type, out var make) ? make(id, name) : null;
    }
}
