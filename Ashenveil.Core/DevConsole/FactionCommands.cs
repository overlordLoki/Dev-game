using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.GameWorld;

namespace Ashenveil.Core.DevConsole
{
    /// <summary>
    /// The console commands for factions and opinions (docs/factions-plan.md): the table, the
    /// player joining and leaving, what NPCs think of people and factions, the player's standing,
    /// rumours, and posting a hostile event by hand until combat posts real ones. Registered by
    /// WorldCommands, and like those given the World, so they can be tested without a screen.
    /// </summary>
    public static class FactionCommands
    {
        public static void Register(CommandRegistry commands, Func<World> world)
        {
            commands.Register("factions", "", "Lists the factions, what each thinks of the others, and who's in them.",
                _ => Factions(world()));
            commands.Register("join", "<faction>", "The player joins a faction (no one has to let them in).",
                args => Join(world(), args[0]), minArgs: 1, maxArgs: 1);
            commands.Register("leave", "<faction>", "The player leaves a faction.",
                args => Leave(world(), args[0]), minArgs: 1, maxArgs: 1);
            commands.Register("opinions", "<npc>", "Shows what an NPC thinks of people and factions, and its grudges.",
                args => Opinions(world(), args[0]), minArgs: 1, maxArgs: 1);
            commands.Register("opinion", "<npc> <who|faction> <n>", "Sets an NPC's opinion of a person (player, id or name) or a faction, -100 to 100.",
                args => SetOpinion(world(), args), minArgs: 3, maxArgs: 3);
            commands.Register("standing", "[<faction> <n>]", "Shows the player's standing with every faction, or sets one.",
                args => Standing(world(), args), minArgs: 0, maxArgs: 2);
            commands.Register("rumours", "<npc>", "Shows what an NPC has heard from others and not yet reflected on.",
                args => Rumours(world(), args[0]), minArgs: 1, maxArgs: 1);
            commands.Register("event", "attack <who> <whom>", "Posts an event by hand: <who> attacked <whom> (player, id or name).",
                args => PostEvent(world(), args), minArgs: 3, maxArgs: 3);
        }

        private static IEnumerable<string> Factions(World world)
        {
            var table = world.Factions;
            foreach (var faction in table.Names)
            {
                var views = string.Join(", ", table.Names.Select(other => $"{other} {table.View(faction, other)}"));
                var members = world.Roster.All.Where(n => n.Factions.Contains(faction)).Select(n => $"{n.Name} (#{n.Id})")
                    .Concat(world.player.Factions.Contains(faction) ? new[] { $"{world.player.Name} (you)" } : Array.Empty<string>())
                    .ToList();
                yield return $"{faction}{(table.IsJoinable(faction) ? " (joinable)" : "")}: {views}";
                yield return $"  members: {(members.Count == 0 ? "none" : string.Join(", ", members))}";
            }
        }

        private static IEnumerable<string> Join(World world, string faction)
        {
            RequireFaction(world, faction);
            var player = world.player;
            if (!player.Join(faction, world.Factions)) throw new CommandException($"You're already in the {faction}.");

            world.Events.Post(GameEvent.Of(EventType.Joined, world.location.Name, player.Feet, player.Id, null,
                                           $"{player.Name} joined the {faction}"));
            return StandingLines(world).Prepend($"Joined the {faction}.");
        }

        private static IEnumerable<string> Leave(World world, string faction)
        {
            RequireFaction(world, faction);
            var player = world.player;
            if (!player.Leave(faction, world.Factions)) throw new CommandException($"You aren't in the {faction}.");

            world.Events.Post(GameEvent.Of(EventType.Left, world.location.Name, player.Feet, player.Id, null,
                                           $"{player.Name} left the {faction}"));
            return StandingLines(world).Prepend($"Left the {faction}.");
        }

        private static IEnumerable<string> Opinions(World world, string who)
        {
            var npc = WorldCommands.FindNpc(world, who);
            var grudges = Relations.Grudges(world.Events, npc, world.Clock.TotalDays);

            yield return $"{npc.Name} (#{npc.Id}), {(npc.Factions.Count == 0 ? "in no faction" : string.Join(", ", npc.Factions))}:";
            yield return "  people:";
            if (npc.Opinions.People.Count == 0) yield return "    none of its own yet (goes by faction views)";
            foreach (var (id, opinion) in npc.Opinions.People.OrderBy(p => p.Key))
                yield return $"    {NameOf(world, id)}: {opinion.Score} ({Attitudes.Of(opinion.Score).ToString().ToLowerInvariant()})"
                           + (opinion.Why != null ? $" - {opinion.Why}" : "");
            yield return $"  the player, now: {Relations.Describe(npc, world.player, world.player.Name, world.Events, world.Clock.TotalDays)}";
            yield return "  factions: " + string.Join(", ", npc.Opinions.Factions.Select(f => $"{f.Key} {f.Value}"));
            if (grudges.Count > 0)
                yield return "  grudges (hostile until reflected on): " + string.Join(", ", grudges.Select(id => NameOf(world, id)));
        }

        private static IEnumerable<string> SetOpinion(World world, string[] args)
        {
            var npc = WorldCommands.FindNpc(world, args[0]);
            int score = Score(args[2]);

            if (world.Factions.Exists(args[1]))
            {
                npc.Opinions.SetFaction(args[1], score);
                yield return $"{npc.Name} now thinks {npc.Opinions.OfFaction(args[1])} of the {args[1]}.";
                yield break;
            }

            var about = FindPerson(world, args[1]);
            npc.Opinions.SetPerson(about.Id, score);
            yield return $"{npc.Name} now thinks {npc.OpinionOf(about)} of {Conversation.NameOf(about)}.";
        }

        private static IEnumerable<string> Standing(World world, string[] args)
        {
            if (args.Length == 1) throw new CommandException("Usage: standing, or standing <faction> <n>.");
            if (args.Length == 2)
            {
                RequireFaction(world, args[0]);
                world.player.Standing[args[0]] = Score(args[1]);
            }
            return StandingLines(world);
        }

        private static IEnumerable<string> StandingLines(World world)
        {
            var player = world.player;
            yield return $"{player.Name}, in {(player.Factions.Count == 0 ? "no faction" : string.Join(", ", player.Factions))}. Standing:";
            foreach (var faction in world.Factions.Names)
            {
                int score = player.StandingWith(faction);
                yield return $"  {faction}: {score} ({Attitudes.Of(score).ToString().ToLowerInvariant()})";
            }
        }

        private static IEnumerable<string> Rumours(World world, string who)
        {
            var npc = WorldCommands.FindNpc(world, who);
            var rumours = npc.Journal.Memories.Where(m => m.Kind == MemoryKind.Rumour).ToList();

            yield return $"{npc.Name} (#{npc.Id}) has heard {rumours.Count} rumour(s):";
            foreach (var m in rumours)
                yield return $"  - day {m.Day}: {NPC.Hearsay(m, id => NameOf(world, id))}  (event #{m.SourceId}, hop {m.Hops})";
        }

        private static IEnumerable<string> PostEvent(World world, string[] args)
        {
            if (!args[0].Equals("attack", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Only 'event attack <who> <whom>' for now.");

            var attacker = FindPerson(world, args[1]);
            var victim = FindPerson(world, args[2]);
            if (attacker == victim) throw new CommandException("Someone attacking themselves isn't news.");

            string level = victim is NPC npc ? world.Roster.LevelOf(npc) : world.location.Name;
            var posted = world.Events.Post(GameEvent.Of(EventType.Attacked, level, victim.Feet, attacker.Id, victim.Id,
                $"{Conversation.NameOf(attacker)} attacked {Conversation.NameOf(victim)}"));
            yield return WorldCommands.Describe(posted);
        }

        // "player" (or "me"), or an NPC by id or name.
        private static Entity FindPerson(World world, string who) =>
            who.Equals("player", StringComparison.OrdinalIgnoreCase) || who.Equals("me", StringComparison.OrdinalIgnoreCase)
                ? world.player
                : WorldCommands.FindNpc(world, who);

        private static string NameOf(World world, int id) =>
            world.FindPerson(id) is { } who ? $"{Conversation.NameOf(who)} (#{id})" : $"#{id}";

        private static void RequireFaction(World world, string faction)
        {
            if (!world.Factions.Exists(faction))
                throw new CommandException($"No faction called '{faction}'. Try 'factions'.");
        }

        private static int Score(string text) =>
            int.TryParse(text, out int n) && n >= Attitudes.Min && n <= Attitudes.Max
                ? n
                : throw new CommandException($"<n> must be a whole number from {Attitudes.Min} to {Attitudes.Max}.");
    }
}
