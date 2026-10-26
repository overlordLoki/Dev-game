using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ashenveil.Core.Config;
using Ashenveil.Core.Entities.brains.Dialogue;

namespace Ashenveil.Core.DevConsole
{
    /// <summary>
    /// The console commands for the Python backend: `server` shows or changes its address, and
    /// `health` asks it whether it (and Ollama, and the model) are up. They share the settings
    /// screen's code - BackendUrl to tidy an address, DialogueApi.CheckHealthAsync to check one -
    /// and are given functions rather than the game, so a test can hand them fakes.
    /// </summary>
    public static class BackendCommands
    {
        /// <param name="getUrl">The address in use now.</param>
        /// <param name="setUrl">Takes a normalised address: applies and saves it. Throws IOException if it can't save.</param>
        /// <param name="checkHealth">Calls /health at an address (DialogueApi.CheckHealthAsync).</param>
        /// <param name="later">
        /// Writes a line once a check finishes - possibly from another thread, so it must be
        /// safe to call from one (ConsoleScreen.WriteLater is).
        /// </param>
        public static void Register(CommandRegistry commands, Func<string> getUrl, Action<string> setUrl,
                                    Func<string, Task<HealthResult>> checkHealth, Action<ConsoleLine> later)
        {
            commands.Register("server", "[url]", "Shows the backend's address, or sets (and saves) a new one.",
                args => args.Length == 0 ? new[] { $"Backend: {getUrl()}" } : SetServer(args[0], setUrl),
                minArgs: 0, maxArgs: 1);
            commands.Register("health", "[url]", "Asks the backend (or the given address) if it and Ollama are up.",
                args => Health(args.Length == 0 ? getUrl() : args[0], checkHealth, later),
                minArgs: 0, maxArgs: 1);
        }

        private static IEnumerable<string> SetServer(string input, Action<string> setUrl)
        {
            if (!BackendUrl.TryNormalize(input, out var url, out var error))
                throw new CommandException(error);

            try
            {
                setUrl(url);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return new[] { $"Backend set to {url} for now, but it couldn't be saved: {e.Message}" };
            }
            return new[] { $"Backend set to {url} (saved). Try 'health'." };
        }

        // The answer takes a moment (up to DialogueApi.HealthTimeout), and the console mustn't
        // freeze while it waits: say it's asking now, and write the answer when it comes.
        private static IEnumerable<string> Health(string input, Func<string, Task<HealthResult>> checkHealth,
                                                  Action<ConsoleLine> later)
        {
            if (!BackendUrl.TryNormalize(input, out var url, out var error))
                throw new CommandException(error);

            checkHealth(url).ContinueWith(t =>
            {
                var result = t.IsCompletedSuccessfully
                    ? t.Result
                    : new HealthResult(HealthStatus.Error, null, t.Exception?.GetBaseException().Message ?? "The check was cancelled.");
                later(new ConsoleLine(result.Message, result.Ok ? LineKind.Output : LineKind.Error));
            }, TaskContinuationOptions.ExecuteSynchronously);

            return new[] { $"Checking {url}..." };
        }
    }
}
