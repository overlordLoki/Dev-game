using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ashenveil.Core.DevConsole
{
    /// <summary>What a console line is, which decides its colour.</summary>
    public enum LineKind
    {
        Input,    // what was typed, echoed back
        Output,   // what a command said
        Error,    // what went wrong
        Event,    // something the game reports by itself (a GameEvent being posted)
    }

    /// <summary>One line of console output.</summary>
    public record ConsoleLine(string Text, LineKind Kind);

    /// <summary>
    /// Thrown by a command for a mistake the user can fix ("No NPC called Bob"). The registry
    /// turns it into an Error line, so commands just throw and don't build error lines themselves.
    /// </summary>
    public class CommandException : Exception
    {
        public CommandException(string message) : base(message) { }
    }

    /// <summary>One console command: what it's called, how to use it, and what it does.</summary>
    public sealed class Command
    {
        public string Name { get; init; }
        public string Usage { get; init; }      // the arguments, e.g. "<col> <row>"; "" for none
        public string Help { get; init; }       // one line, shown by `help`
        public int MinArgs { get; init; }
        public int MaxArgs { get; init; }
        public Func<string[], IEnumerable<string>> Run { get; init; }   // args -> output lines
    }

    /// <summary>
    /// Every console command, by name. A command is registered once (name, usage, help, handler)
    /// instead of being an arm of one big switch, so `help` lists them all for free and adding one
    /// touches nothing else. Execute parses a typed line and runs it, and never throws: whatever
    /// goes wrong comes back as Error lines, so a bad command can't take the game down.
    /// Knows nothing about the screen or the world - see WorldCommands and ConsoleScreen.
    /// </summary>
    public class CommandRegistry
    {
        // Names are matched ignoring case: "Help" and "help" are the same command.
        private readonly Dictionary<string, Command> _byName = new(StringComparer.OrdinalIgnoreCase);

        public CommandRegistry()
        {
            Register("help", "", "Lists every command.", _ => HelpLines());
        }

        /// <summary>Every command, alphabetical.</summary>
        public IEnumerable<Command> All => _byName.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Adds a command. <paramref name="minArgs"/>/<paramref name="maxArgs"/> are checked before
        /// <paramref name="run"/> is called, so a handler can index its arguments without checking.
        /// </summary>
        public void Register(string name, string usage, string help, Func<string[], IEnumerable<string>> run,
                             int minArgs = 0, int maxArgs = 0)
        {
            if (_byName.ContainsKey(name))
                throw new ArgumentException($"A command called '{name}' is already registered.", nameof(name));
            _byName[name] = new Command
            {
                Name = name, Usage = usage, Help = help, Run = run,
                MinArgs = minArgs, MaxArgs = Math.Max(minArgs, maxArgs),
            };
        }

        /// <summary>
        /// Parses and runs one typed line. Blank input gives no lines. A leading "/" is ignored:
        /// the console only ever runs commands so it doesn't need one, but "/tp 3 4" out of
        /// habit from other games should still work.
        /// </summary>
        public IReadOnlyList<ConsoleLine> Execute(string input)
        {
            var words = Split(input?.TrimStart().TrimStart('/'));
            if (words.Count == 0) return Array.Empty<ConsoleLine>();

            if (!_byName.TryGetValue(words[0], out var command))
                return Error($"Unknown command '{words[0]}'. Type 'help' for the list.");

            string[] args = words.Skip(1).ToArray();
            if (args.Length < command.MinArgs || args.Length > command.MaxArgs)
                return Error($"Usage: {UsageOf(command)}");

            try
            {
                // ToList inside the try: a handler written with `yield return` only runs when
                // it's enumerated, so its exceptions have to be caught here too.
                return command.Run(args).Select(text => new ConsoleLine(text, LineKind.Output)).ToList();
            }
            catch (CommandException e)
            {
                return Error(e.Message);
            }
            catch (Exception e)
            {
                // A bug in a command, not a typo: say so, but keep the game running.
                return Error($"'{command.Name}' failed: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// Splits a typed line into words on whitespace. Double quotes keep spaces inside one word
        /// (journal "Sir Lancelot"); "" is an empty word; an unclosed quote runs to the end.
        /// </summary>
        public static List<string> Split(string input)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            bool inQuotes = false, inWord = false;

            foreach (char c in input ?? "")
            {
                if (c == '"') { inQuotes = !inQuotes; inWord = true; }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (inWord) words.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }
                else { word.Append(c); inWord = true; }
            }
            if (inWord) words.Add(word.ToString());
            return words;
        }

        private IEnumerable<string> HelpLines() =>
            All.Select(c => $"{UsageOf(c),-28} {c.Help}");

        private static string UsageOf(Command c) =>
            string.IsNullOrEmpty(c.Usage) ? c.Name : $"{c.Name} {c.Usage}";

        private static ConsoleLine[] Error(string text) => new[] { new ConsoleLine(text, LineKind.Error) };
    }
}
