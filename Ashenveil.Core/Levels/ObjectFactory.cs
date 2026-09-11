using System;
using System.Collections.Generic;
using Ashenveil.Core.Objects;

namespace Ashenveil.Core.Levels
{
    /// <summary>
    /// Turns an <see cref="ObjectData"/> row from a level file into a real object
    /// instance. Replaces a growing type/variety switch with one lookup table: the
    /// key is (type, variety) exactly as the editor writes it - varieties are 1-based
    /// and contiguous, matching GameCatalogue.BuildObjects. Adding a new placeable is
    /// one entry here, not a new case.
    /// </summary>
    public static class ObjectFactory
    {
        static readonly Dictionary<(string type, int variety), Func<int, int, IObject>> Builders = new()
        {
            [("tree", 1)]   = (c, r) => new TreeSmall(c, r),
            [("tree", 2)]   = (c, r) => new TreeMedium(c, r),
            [("tree", 3)]   = (c, r) => new TreeLarge(c, r),
            [("bush", 1)]   = (c, r) => new BushSmall(c, r),
            [("bush", 2)]   = (c, r) => new BushMedium(c, r),
            [("bush", 3)]   = (c, r) => new BushLarge(c, r),
            [("well", 1)]   = (c, r) => new Well(c, r, Assets.Well),
            [("castle", 1)] = (c, r) => new Castle_Square(c, r, Assets.Castle_Square),
        };

        /// <summary>
        /// Builds the object for this row, or null if its (type, variety) isn't known -
        /// matching the old switch, which silently skipped anything it didn't recognise.
        /// </summary>
        public static IObject Create(ObjectData o) =>
            Builders.TryGetValue((o.type, o.variety), out var build) ? build(o.col, o.row) : null;
    }
}
