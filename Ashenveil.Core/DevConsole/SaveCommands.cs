using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ashenveil.Core.Saving;
using Ashenveil.Core.Screens;

namespace Ashenveil.Core.DevConsole
{
    /// <summary>
    /// `saves`, `save [n]` and `load &lt;n&gt;`: the save slots screen without leaving the console,
    /// handy for checking what a load restores (`save`, `load 1`, then `npcs` or `journal`).
    /// Saves are numbered as `saves` lists them - newest first, the same order as the screen -
    /// starting at 1. Nothing here asks "are you sure?": it's a dev tool.
    ///
    /// Given functions rather than the game, so a test can hand them fakes. Each action answers
    /// (ok, message); a failure is shown as an error line.
    /// </summary>
    public static class SaveCommands
    {
        /// <param name="list">Every save, newest first (SaveLibrary.List).</param>
        /// <param name="saveNew">Saves the world being played as a new save.</param>
        /// <param name="saveOver">Saves the world being played over this save.</param>
        /// <param name="load">Swaps in this save's world for the one being played.</param>
        public static void Register(CommandRegistry commands,
                                    Func<IReadOnlyList<SaveSummary>> list,
                                    Func<(bool ok, string message)> saveNew,
                                    Func<SaveSummary, (bool ok, string message)> saveOver,
                                    Func<SaveSummary, (bool ok, string message)> load)
        {
            commands.Register("saves", "", "Lists every save, numbered for `save <n>` and `load <n>`, newest first.",
                _ => Saves(list()));
            commands.Register("save", "[n]", "Saves the game as a new save, or over save n (no question asked).",
                args => Answer(args.Length == 0 ? saveNew() : saveOver(Pick(list(), args[0]))),
                minArgs: 0, maxArgs: 1);
            commands.Register("load", "<n>", "Throws away the game being played and carries on save n.",
                args => Answer(load(Pick(list(), args[0]))),
                minArgs: 1, maxArgs: 1);
        }

        private static IEnumerable<string> Saves(IReadOnlyList<SaveSummary> saves)
        {
            if (saves.Count == 0)
            {
                yield return "No saves yet. 'save' makes one.";
                yield break;
            }
            for (int i = 0; i < saves.Count; i++)
                yield return $"{i + 1}. {SaveSlotsScreen.Headline(saves[i])} ({SaveSlotsScreen.Detail(saves[i])})";
        }

        // The save numbered `n` in `saves`, or a CommandException saying what's wrong.
        private static SaveSummary Pick(IReadOnlyList<SaveSummary> saves, string n)
        {
            if (!int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                throw new CommandException("<n> must be a save's number. Type 'saves' to see them.");
            if (saves.Count == 0)
                throw new CommandException("There are no saves yet.");
            if (number < 1 || number > saves.Count)
                throw new CommandException($"There's no save {number}: they go from 1 to {saves.Count}. Type 'saves' to see them.");
            return saves[number - 1];
        }

        private static IEnumerable<string> Answer((bool ok, string message) result) =>
            result.ok ? new[] { result.message } : throw new CommandException(result.message);
    }
}
