using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.GameWorld.Locations;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.DevConsole
{
    /// <summary>
    /// The console commands that look at or change the game: where the player is, moving them,
    /// listing NPCs, reading an NPC's memory, the event log and the clock. Each is given the World, not the game or a screen,
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
            commands.Register("events", "[n]", "Lists the last n events (default 10), with their ids.",
                args => Events(world(), args.Length == 0 ? null : args[0]), minArgs: 0, maxArgs: 1);
            commands.Register("time", "[+<n>d|+<n>h]", "Shows the date and time; +2d skips to the start of the day after next, +3h moves on 3 hours.",
                args => Time(world(), args.Length == 0 ? null : args[0]), minArgs: 0, maxArgs: 1);
            commands.Register("reflect", "<npc>", "Has an NPC reflect on today now, without waiting for midnight.",
                args => Reflect(world(), args[0]), minArgs: 1, maxArgs: 1);
        }

        private const int DefaultEventCount = 10;

        private static IEnumerable<string> Events(World world, string count)
        {
            int n = DefaultEventCount;
            if (count != null && (!int.TryParse(count, out n) || n < 1))
                throw new CommandException("[n] must be a whole number, 1 or more.");

            if (world.Events.All.Count == 0) { yield return "Nothing has happened yet."; yield break; }
            foreach (var e in world.Events.Recent(n))
                yield return Describe(e);
        }

        /// <summary>One event as a console line: "#3 day 2, Medows: Sir Lancelot reached the entry".</summary>
        public static string Describe(GameEvent e) =>
            $"#{e.Id} day {e.Day}{(e.Level != null ? $", {e.Level}" : "")}: {e.Text}";

        private static IEnumerable<string> Time(World world, string skip)
        {
            var clock = world.Clock;
            if (skip != null)
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                string amount = skip.Length > 2 ? skip[1..^1] : "";
                char unit = char.ToLowerInvariant(skip[^1]);
                if (!skip.StartsWith('+') || (unit != 'd' && unit != 'h'))
                    throw new CommandException("Skip forward with +<n>d (days) or +<n>h (hours), like +1d or +6h.");

                if (unit == 'd')
                {
                    if (!int.TryParse(amount, System.Globalization.NumberStyles.None, culture, out int days) || days < 1)
                        throw new CommandException("Days must be a whole number, 1 or more: +1d.");
                    clock.SkipDays(days);
                }
                else
                {
                    if (!float.TryParse(amount, System.Globalization.NumberStyles.Float, culture, out float hours) || hours <= 0)
                        throw new CommandException("Hours must be a number above 0: +6h or +0.5h.");
                    clock.SkipHours(hours);
                }
            }

            // Rounded, so 6.5 hours that comes out as 6.4999995 still reads 06:30.
            int minutes = Math.Min(24 * 60 - 1, (int)MathF.Round(clock.HourOfDay * 60));
            yield return $"{clock.DateDisplay} (day {clock.TotalDays}), {minutes / 60:00}:{minutes % 60:00}";
        }

        private static IEnumerable<string> Reflect(World world, string who)
        {
            var npc = FindNpc(world, who);
            int today = world.Clock.TotalDays;
            if (!npc.Journal.HasAnythingOn(today))
                throw new CommandException($"Nothing has happened to {npc.Name} today, so there's nothing to reflect on.");

            yield return world.Reflections.Queue(npc, today)
                ? $"{npc.Name} will reflect on today once the game is running again. See 'journal {npc.Id}' after."
                : $"{npc.Name} is already reflecting on today.";
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

            var facts = journal.Memories.Where(m => m.Kind != MemoryKind.Event).ToList();
            var events = journal.Memories.Where(m => m.Kind == MemoryKind.Event).ToList();

            yield return $"{npc.Name} (#{npc.Id}) remembers {facts.Count} fact(s):";
            foreach (var m in facts)
                yield return $"  - {m.Text}  (day {m.Day}, " + (m.SourceId is int id ? $"conversation #{id})" : $"{m.Kind})");

            yield return $"...{events.Count} event(s) not yet reflected on:";
            foreach (var m in events)
                yield return $"  - day {m.Day}: {m.Text}" + (m.SourceId is int id ? $"  (event #{id})" : "");

            yield return $"...{journal.Transcripts.Count} conversation(s):";
            foreach (var t in journal.Transcripts)
                yield return $"  #{t.Id} day {t.Day}{(t.Location != null ? $" at {t.Location}" : "")}"
                           + $" with {string.Join(", ", t.Participants.Select(p => p.Name))}: {t.Summary ?? "(no summary yet)"}";

            yield return $"...and {journal.Days.Count} diary entr{(journal.Days.Count == 1 ? "y" : "ies")}"
                       + $" (last reflected on day {journal.LastReflectedDay}):";
            foreach (var d in journal.Days)
                yield return $"  day {d.Day}: {d.Summary}";
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
