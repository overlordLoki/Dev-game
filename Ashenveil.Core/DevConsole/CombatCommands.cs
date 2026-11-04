using System;
using System.Collections.Generic;
using Ashenveil.Core.Combat;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;

namespace Ashenveil.Core.DevConsole
{
    /// <summary>
    /// The console commands for health (docs/combat-plan.md, step 1): reading it, hurting,
    /// healing and killing by hand, and god mode. That's how health is tested until there's an
    /// attack. Registered by WorldCommands, and like those given the World.
    /// </summary>
    public static class CombatCommands
    {
        public static void Register(CommandRegistry commands, Func<World> world)
        {
            commands.Register("hp", "[npc]", "Shows the player's health, or an NPC's (id or name).",
                args => Hp(world(), args.Length == 0 ? "player" : args[0]), minArgs: 0, maxArgs: 1);
            commands.Register("damage", "<npc|player> <n>", "Takes n health from someone (god mode blocks it).",
                args => Damage(world(), args[0], args[1]), minArgs: 2, maxArgs: 2);
            commands.Register("heal", "<npc|player> [n]", "Gives n health back (default: all of it). Healing the dead brings them back.",
                args => Heal(world(), args[0], args.Length > 1 ? args[1] : null), minArgs: 1, maxArgs: 2);
            commands.Register("kill", "<npc|player>", "Takes all of someone's health, god mode or not.",
                args => Kill(world(), args[0]), minArgs: 1, maxArgs: 1);
            commands.Register("god", "", "Turns the player's invulnerability on or off.",
                _ => God(world()));
            commands.Register("aggro", "<npc> [who]", "Makes an NPC fight someone: the player (default), or another NPC by id or name.",
                args => Aggro(world(), args[0], args.Length > 1 ? args[1] : "player"), minArgs: 1, maxArgs: 2);
            commands.Register("calm", "<npc>", "Ends an NPC's fight; it goes back to what it was doing.",
                args => Calm(world(), args[0]), minArgs: 1, maxArgs: 1);
        }

        private static IEnumerable<string> Aggro(World world, string who, string whom)
        {
            var npc = WorldCommands.FindNpc(world, who);
            var target = FactionCommands.FindPerson(world, whom);
            if (target == npc) throw new CommandException($"{npc.Name} can't fight themselves.");
            if (npc.IsDead) throw new CommandException($"{npc.Name} is dead.");
            if (target.IsDead) throw new CommandException($"{Conversation.NameOf(target)} is already dead.");
            if (world.Roster.LevelOf(npc) != (target is NPC other ? world.Roster.LevelOf(other) : world.location.Name))
                throw new CommandException($"{npc.Name} and {Conversation.NameOf(target)} aren't in the same level.");

            npc.StartFighting(target);
            if (npc.FightingWith != target)
                throw new CommandException($"{npc.Name} can't fight right now ({npc.State}).");
            yield return $"{npc.Name} (#{npc.Id}) is fighting {Conversation.NameOf(target)}.";
        }

        private static IEnumerable<string> Calm(World world, string who)
        {
            var npc = WorldCommands.FindNpc(world, who);
            if (npc.FightingWith == null) throw new CommandException($"{npc.Name} isn't fighting anyone.");
            npc.StopFighting();
            yield return $"{npc.Name} (#{npc.Id}) has stopped fighting ({npc.State}).";
        }

        private static IEnumerable<string> Hp(World world, string who)
        {
            var (entity, health) = Find(world, who);
            yield return Describe(entity, health);
        }

        private static IEnumerable<string> Damage(World world, string who, string amount)
        {
            var (entity, health) = Find(world, who);
            int n = Amount(amount);
            if (!health.TakeDamage(n))
                throw new CommandException(health.IsDead ? $"{Conversation.NameOf(entity)} is already dead."
                    : health.God ? $"{Conversation.NameOf(entity)} is in god mode." : $"{Conversation.NameOf(entity)} can't be hurt right now.");
            yield return Describe(entity, health);
        }

        private static IEnumerable<string> Heal(World world, string who, string amount)
        {
            var (entity, health) = Find(world, who);
            health.Heal(amount == null ? health.Max : Amount(amount));
            yield return Describe(entity, health);
        }

        private static IEnumerable<string> Kill(World world, string who)
        {
            var (entity, health) = Find(world, who);
            if (health.IsDead) throw new CommandException($"{Conversation.NameOf(entity)} is already dead.");
            health.Kill();
            yield return Describe(entity, health);
        }

        private static IEnumerable<string> God(World world)
        {
            var health = world.player.Health;
            health.God = !health.God;
            yield return health.God ? "God mode on: nothing hurts you." : "God mode off.";
        }

        // "Sir Lancelot (#1): 60/80" / "Traveller (you): 0/100, dead".
        private static string Describe(Entity entity, Health health)
        {
            string name = entity is Player ? $"{Conversation.NameOf(entity)} (you)" : $"{Conversation.NameOf(entity)} (#{entity.Id})";
            string notes = (entity is NPC { IsKnockedOut: true } npc ? $", knocked out ({npc.KnockedOut:0}s left)"
                            : health.IsDead ? ", dead" : "") + (health.God ? ", god mode" : "");
            return $"{name}: {health.Current}/{health.Max}{notes}";
        }

        // Someone with a health: "player" (or "me"), or an NPC by id or name.
        private static (Entity, Health) Find(World world, string who)
        {
            var entity = FactionCommands.FindPerson(world, who);
            return entity.Health is { } health
                ? (entity, health)
                : throw new CommandException($"{Conversation.NameOf(entity)} has no health: they can't be hurt.");
        }

        private static int Amount(string text) =>
            int.TryParse(text, out int n) && n > 0 ? n : throw new CommandException("<n> must be a whole number, 1 or more.");
    }
}
