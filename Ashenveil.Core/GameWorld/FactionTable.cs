using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// The factions, and what each one thinks of the others by default, from
    /// Content/factions.json. Read-only data, like a level file: it only *seeds* things - a new
    /// NPC's faction opinions, a new player's standings, the shift when someone joins or leaves.
    /// After that every NPC and player has their own copy, and that's what changes and is saved.
    ///
    /// Scores run from -100 to 100 (see <see cref="Attitudes"/>). A faction the table doesn't
    /// mention is seen at 0.
    /// </summary>
    public class FactionTable
    {
        /// <summary>One faction as factions.json describes it.</summary>
        public class Entry
        {
            // Whether a player can be let in (by an NPC of it who admits people). Nobody joins "traveller".
            public bool joinable { get; set; }
            // What this faction thinks of each faction, itself included.
            public Dictionary<string, int> views { get; set; } = new();
        }

        private readonly Dictionary<string, Entry> _factions;

        public FactionTable(Dictionary<string, Entry> factions) => _factions = factions;

        /// <summary>The faction a new player starts in, before they've joined anything.</summary>
        public const string StartingFaction = "traveller";

        private static FactionTable _default;

        /// <summary>The game's table, read from Content/factions.json the first time it's asked for.</summary>
        public static FactionTable Default => _default ??= Load(Path.Combine(AppContext.BaseDirectory, "Content", "factions.json"));

        /// <summary>Reads a table from a factions.json. Throws if it isn't there or isn't valid.</summary>
        public static FactionTable Load(string path) =>
            new(JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path)));

        /// <summary>Every faction, in the file's order.</summary>
        public IEnumerable<string> Names => _factions.Keys;

        public bool Exists(string faction) => faction != null && _factions.ContainsKey(faction);

        public bool IsJoinable(string faction) => Exists(faction) && _factions[faction].joinable;

        /// <summary>What <paramref name="faction"/> thinks of <paramref name="about"/> by default; 0 if the table doesn't say.</summary>
        public int View(string faction, string about) =>
            Exists(faction) && _factions[faction].views.TryGetValue(about, out int score) ? score : 0;

        /// <summary>
        /// The faction view of someone in all of <paramref name="factions"/>: the lowest of them
        /// wins (a guard sees a bandit, even if he's also a villager). 0 for no factions.
        /// </summary>
        public static int Lowest(IEnumerable<string> factions, Func<string, int> view) =>
            factions.Select(view).DefaultIfEmpty(0).Min();
    }

    /// <summary>What a score means for behaviour. Thresholds, so the scores can be tuned without new states.</summary>
    public enum Attitude
    {
        Hostile,    // <= -60: attacks on sight (once combat exists)
        Wary,       // -60 to -20: keeps its distance; short in chat
        Neutral,    // -20 to +20
        Friendly,   // >= +20: helps in a fight, warmer in chat, trusted with gossip
    }

    public static class Attitudes
    {
        public const int Min = -100, Max = 100;
        public const int HostileAt = -60, WaryAt = -20, FriendlyAt = 20;

        public static int Clamp(int score) => Math.Clamp(score, Min, Max);

        public static Attitude Of(int score) =>
            score <= HostileAt ? Attitude.Hostile
            : score < WaryAt ? Attitude.Wary
            : score < FriendlyAt ? Attitude.Neutral
            : Attitude.Friendly;
    }
}
