using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Entities;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.GameWorld.Locations;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.DevConsole
{
    /// <summary>
    /// The console commands that look at or change the game: where the player is, moving them,
    /// listing NPCs, reading an NPC's memory. Each is given the World, not the game or a screen,
    /// so they can be tested against a World built from a test level.
    /// </summary>
    public static class WorldCommands
    {
        public static void Register(CommandRegistry commands, World world) => Register(commands, () => world);

        /// <param name="world">
        /// The world being played, asked for each time a command runs: Continue or the `load`
        /// command swap in a different World, and the commands should follow it.
        /// </param>
        public static void Register(CommandRegistry commands, Func<World> world)
        {
            commands.Register("where", "[npc]", "Shows the level, cell and x,y (in cells) of the player's feet, or an NPC's.",
                args => Where(world(), args.Length == 0 ? null : args[0]), minArgs: 0, maxArgs: 1);
            commands.Register("tp", "<col> <row>", "Stands the player on a cell of this level (as if spawned there).",
                args => Teleport(world(), args), minArgs: 2, maxArgs: 2);
            commands.Register("tpxy", "<x> <y>", "Moves the player's feet to an exact x,y in cells (e.g. 3.25 4.5).",
                args => TeleportXY(world(), args), minArgs: 2, maxArgs: 2);
            commands.Register("npcs", "", "Lists every NPC in every level: id, name, level, state.",
                _ => Npcs(world()));
            commands.Register("journal", "<npc>", "Shows what an NPC remembers. <npc> is an id or a name.",
                args => Journal(world(), args[0]), minArgs: 1, maxArgs: 1);
            commands.Register("nav", "", "Shows or hides the blocked cells (red) and each NPC's path (yellow).",
                _ => ToggleNav());
        }

        private static IEnumerable<string> ToggleNav()
        {
            Utility.Debug.SHOWNAV = !Utility.Debug.SHOWNAV;
            yield return Utility.Debug.SHOWNAV
                ? "Nav overlay on: red cells are blocked, yellow lines are NPC paths."
                : "Nav overlay off.";
        }

        // Measured by the feet - the bottom centre of the collision box - like doors and
        // depth-sorting are, so the cell shown is the one the game thinks you're in. With no
        // argument it's the player; otherwise an NPC, in whatever level it's in.
        private static IEnumerable<string> Where(World world, string who)
        {
            Entity e;
            string label, level;
            if (who == null || who.Equals("player", StringComparison.OrdinalIgnoreCase))
                (e, label, level) = (world.player, "Player", world.location.Name);
            else
            {
                var npc = FindNpc(world, who);
                (e, label, level) = (npc, $"{npc.Name} (#{npc.Id})", world.Roster.LevelOf(npc));
            }

            var (feet, cell) = Feet(e);
            yield return $"{label}: {level}, cell ({cell.X}, {cell.Y}), feet at ({feet.X:0.00}, {feet.Y:0.00})";
        }

        private static IEnumerable<string> Teleport(World world, string[] args)
        {
            if (!int.TryParse(args[0], out int col) || !int.TryParse(args[1], out int row))
                throw new CommandException("<col> and <row> must be whole numbers.");

            var map = world.location.tileMap;
            if (col < 0 || row < 0 || col >= map.Cols || row >= map.Rows)
                throw new CommandException(
                    $"({col}, {row}) is off the map: {world.location.Name} is {map.Cols}x{map.Rows} cells (0 to {map.Cols - 1}, 0 to {map.Rows - 1}).");

            // Stood on the cell the same way Location.PlaceAt stands someone on a point.
            world.player.StandAt(new PointOfInterest("tp", col, row).StandingSpot());
            yield return $"Moved to ({col}, {row}).";
        }

        // An exact spot rather than a cell: handy for lining up against a box edge or a door.
        // Numbers are read with a '.' decimal point whatever the PC's region is set to.
        private static IEnumerable<string> TeleportXY(World world, string[] args)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.NumberStyles.Float;
            if (!float.TryParse(args[0], style, culture, out float x) || !float.TryParse(args[1], style, culture, out float y))
                throw new CommandException("<x> and <y> must be numbers, like 3 or 3.25.");

            // Edges included this time: feet on the very edge of the map is a real place to stand.
            var map = world.location.tileMap;
            if (x < 0 || y < 0 || x > map.Cols || y > map.Rows)
                throw new CommandException(
                    $"({x}, {y}) is off the map: {world.location.Name} runs from (0, 0) to ({map.Cols}, {map.Rows}).");

            world.player.StandAt(new Vector2(x, y));
            yield return $"Feet moved to ({x:0.00}, {y:0.00}).";
        }

        private static IEnumerable<string> Npcs(World world)
        {
            var all = world.Roster.All.ToList();
            if (all.Count == 0) { yield return "No NPCs yet."; yield break; }

            foreach (var npc in all.OrderBy(n => n.Id))
                yield return $"#{npc.Id} {npc.Name} - {world.Roster.LevelOf(npc)}, {npc.State}";
        }

        private static IEnumerable<string> Journal(World world, string who)
        {
            var npc = FindNpc(world, who);
            var journal = npc.Journal;

            yield return $"{npc.Name} (#{npc.Id}) remembers {journal.Memories.Count} fact(s):";
            foreach (var m in journal.Memories)
                yield return $"  - {m.Text}" + (m.SourceId is int id ? $"  (conversation #{id})" : $"  ({m.Kind})");

            yield return $"...and {journal.Transcripts.Count} conversation(s):";
            foreach (var t in journal.Transcripts)
                yield return $"  #{t.Id} day {t.Day}{(t.Location != null ? $" at {t.Location}" : "")}"
                           + $" with {string.Join(", ", t.Participants.Select(p => p.Name))}: {t.Summary ?? "(no summary yet)"}";
        }

        /// <summary>
        /// An NPC by id ("1") or name ("Sir Lancelot", any case), in any level. Throws a
        /// CommandException that says what to try when there's no match, or more than one.
        /// </summary>
        public static NPC FindNpc(World world, string who)
        {
            if (int.TryParse(who, out int id))
                return world.Roster.Find(id) ?? throw new CommandException($"No NPC has id {id}. Try 'npcs'.");

            var matches = world.Roster.All
                .Where(n => string.Equals(n.Name, who, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            return matches.Count switch
            {
                0 => throw new CommandException($"No NPC called '{who}'. Try 'npcs'."),
                1 => matches[0],
                _ => throw new CommandException(
                    $"{matches.Count} NPCs are called '{who}' (ids {string.Join(", ", matches.Select(n => n.Id))}). Use an id."),
            };
        }

        private static (Vector2 feet, Point cell) Feet(Entity e)
        {
            var feet = e.Feet;
            // Bottom is one past the inside, so feet resting exactly on a cell's bottom edge
            // are in that cell, not the one below: round up, then step back one.
            var cell = new Point((int)MathF.Floor(feet.X), (int)MathF.Ceiling(feet.Y) - 1);
            return (feet, cell);
        }
    }
}
