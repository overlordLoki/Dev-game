using System;
using System.Collections.Generic;
using Ashenveil.Core.Entities.brains;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Builds an NPC from the name of its type ("Knight"), with the brain that type comes
    /// with. Loading a save uses it now; spawning NPCs from a level's JSON can later.
    /// One line per NPC type: a new type that isn't listed here can't be loaded.
    /// </summary>
    public static class NpcFactory
    {
        // The key is the class name, which is what SaveMapper writes as an NPC's type. If a
        // class is ever renamed, keep its old name here too, or older saves lose that NPC.
        private static readonly Dictionary<string, Func<int, string, NPC>> Makers = new()
        {
            [nameof(Knight)] = (id, name) => new Knight(Vector2.Zero, id, name, new KnightBrain()),
        };

        /// <summary>
        /// A new NPC of this type at the origin (the caller stands it where it belongs), or
        /// null for a type the game doesn't know.
        /// </summary>
        public static NPC Make(string type, int id, string name) =>
            type != null && Makers.TryGetValue(type, out var make) ? make(id, name) : null;
    }
}
