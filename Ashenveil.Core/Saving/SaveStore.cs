using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ashenveil.Core.Config;
using Microsoft.Data.Sqlite;

namespace Ashenveil.Core.Saving
{
    /// <summary>
    /// Writes a <see cref="SaveData"/> to a SQLite database and reads it back: one file per save,
    /// at the path it's given - a file in %LOCALAPPDATA%\Ashenveil\saves in the game, in a temp
    /// folder in the tests. The only code that knows about tables and SQL. Which files exist,
    /// and what they're called, is SaveLibrary's business; this only ever knows its own file.
    ///
    /// The schema's version lives in SQLite's own PRAGMA user_version. An older file is
    /// upgraded by running <see cref="Migrations"/> in order; a newer one is refused, never
    /// half-read. A save is a snapshot, written in one transaction (clear every table, insert
    /// everything), so a crash part-way leaves the previous save whole.
    ///
    /// Like SettingsStore: loading never throws (anything wrong is a message, and the game
    /// still starts); saving throws, and the caller decides how to say so.
    /// </summary>
    public class SaveStore
    {
        /// <summary>The schema this game writes. Bump it when adding a migration.</summary>
        public const int SchemaVersion = 7;

        /// <summary>Where the game keeps its saves: a "saves" folder beside settings.json.</summary>
        public static string DefaultFolder => Path.Combine(SettingsStore.DefaultFolder, "saves");

        public string FilePath { get; }

        public SaveStore(string filePath) => FilePath = filePath;

        /// <summary>Whether the file exists. It may still turn out to be unreadable.</summary>
        public bool HasSave => File.Exists(FilePath);

        // ------------------------------------------------------------------ schema

        // Version 1, as in docs/save-system-plan.md. Never edit this once saves exist in the wild:
        // change the schema with a migration instead, so older files can catch up. A new file is
        // made at version 1 and then migrated like any other, so there's one way to every version.
        // Public so the tests can write a version 1 file the way a version 1 game did.
        public const string Version1Schema = """
            CREATE TABLE game (
                id                 INTEGER PRIMARY KEY CHECK (id = 1),
                saved_at           TEXT    NOT NULL,
                total_days         INTEGER NOT NULL,
                seconds_into_day   REAL    NOT NULL,
                next_person_id     INTEGER NOT NULL,
                next_transcript_id INTEGER NOT NULL
            );
            CREATE TABLE players (
                player_id INTEGER PRIMARY KEY,
                name      TEXT NOT NULL,
                level     TEXT NOT NULL,
                x         REAL NOT NULL,
                y         REAL NOT NULL
            );
            CREATE TABLE populated_levels (level TEXT PRIMARY KEY);
            CREATE TABLE npcs (
                npc_id INTEGER PRIMARY KEY,
                type   TEXT NOT NULL,
                name   TEXT NOT NULL,
                level  TEXT NOT NULL,
                x      REAL NOT NULL,
                y      REAL NOT NULL
            );
            CREATE TABLE memories (
                npc_id        INTEGER NOT NULL REFERENCES npcs(npc_id),
                seq           INTEGER NOT NULL,
                text          TEXT    NOT NULL,
                kind          TEXT    NOT NULL,
                transcript_id INTEGER,
                PRIMARY KEY (npc_id, seq)
            );
            CREATE TABLE transcripts (
                id       INTEGER PRIMARY KEY,
                day      INTEGER NOT NULL,
                location TEXT,
                summary  TEXT
            );
            CREATE TABLE transcript_participants (
                transcript_id INTEGER NOT NULL REFERENCES transcripts(id),
                seq           INTEGER NOT NULL,
                person_id     INTEGER NOT NULL,
                name          TEXT    NOT NULL,
                PRIMARY KEY (transcript_id, seq)
            );
            CREATE TABLE transcript_lines (
                transcript_id INTEGER NOT NULL REFERENCES transcripts(id),
                seq           INTEGER NOT NULL,
                text          TEXT    NOT NULL,
                PRIMARY KEY (transcript_id, seq)
            );
            CREATE TABLE journal_transcripts (
                npc_id        INTEGER NOT NULL REFERENCES npcs(npc_id),
                transcript_id INTEGER NOT NULL REFERENCES transcripts(id),
                PRIMARY KEY (npc_id, transcript_id)
            );
            CREATE TABLE tasks (
                npc_id  INTEGER NOT NULL REFERENCES npcs(npc_id),
                seq     INTEGER NOT NULL,
                type    TEXT    NOT NULL,
                x       REAL,
                y       REAL,
                seconds REAL,
                PRIMARY KEY (npc_id, seq)
            );
            """;

        // One entry per version after the first: Migrations[0] takes a version 1 file to
        // version 2, and so on. Each is a few ALTER TABLE / CREATE TABLE statements. Adding one
        // means adding it here and bumping SchemaVersion; Version1Schema stays as version 1.
        private static readonly string[] Migrations =
        {
            // 1 -> 2: the event log and daily reflection (docs/npc-memory-next-plan.md, Part B).
            // A version 1 memory has no day: one from a conversation takes its transcript's.
            """
            CREATE TABLE events (
                event_id      INTEGER PRIMARY KEY,
                day           INTEGER NOT NULL,
                level         TEXT,
                x             REAL    NOT NULL,
                y             REAL    NOT NULL,
                type          TEXT    NOT NULL,
                actor_id      INTEGER,
                target_id     INTEGER,
                transcript_id INTEGER,
                text          TEXT    NOT NULL
            );
            ALTER TABLE game ADD COLUMN next_event_id INTEGER NOT NULL DEFAULT 1;
            ALTER TABLE memories ADD COLUMN day INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE memories ADD COLUMN event_id INTEGER;
            UPDATE memories SET day = (SELECT day FROM transcripts WHERE id = memories.transcript_id)
                WHERE transcript_id IN (SELECT id FROM transcripts);
            CREATE TABLE day_entries (
                npc_id  INTEGER NOT NULL REFERENCES npcs(npc_id),
                seq     INTEGER NOT NULL,
                day     INTEGER NOT NULL,
                summary TEXT    NOT NULL,
                PRIMARY KEY (npc_id, seq)
            );
            ALTER TABLE npcs ADD COLUMN last_reflected_day INTEGER NOT NULL DEFAULT 0;
            """,

            // 2 -> 3: factions and opinions (docs/factions-plan.md). Factions are a comma-separated
            // list. A version 2 game's only NPC type was the knight, a guard; its player a
            // traveller. Opinions and standings left empty are seeded from the table on load.
            """
            ALTER TABLE npcs ADD COLUMN factions TEXT NOT NULL DEFAULT '';
            ALTER TABLE players ADD COLUMN factions TEXT NOT NULL DEFAULT '';
            UPDATE npcs SET factions = 'guard' WHERE type = 'Knight';
            UPDATE players SET factions = 'traveller';
            CREATE TABLE opinions (
                owner_id INTEGER NOT NULL REFERENCES npcs(npc_id),
                about    TEXT    NOT NULL,
                score    INTEGER NOT NULL,
                why      TEXT,
                PRIMARY KEY (owner_id, about)
            );
            CREATE TABLE standings (
                player_id INTEGER NOT NULL REFERENCES players(player_id),
                faction   TEXT    NOT NULL,
                score     INTEGER NOT NULL,
                PRIMARY KEY (player_id, faction)
            );
            ALTER TABLE memories ADD COLUMN teller_id INTEGER;
            ALTER TABLE memories ADD COLUMN hops INTEGER NOT NULL DEFAULT 0;
            """,

            // 3 -> 4: events each NPC knows of, kept after reflection forgets the memory, so
            // gossip doesn't tell it them again. A version 3 file has none: its NPCs only know
            // the events still in their memories.
            """
            CREATE TABLE known_events (
                npc_id   INTEGER NOT NULL REFERENCES npcs(npc_id),
                event_id INTEGER NOT NULL,
                day      INTEGER NOT NULL,
                PRIMARY KEY (npc_id, event_id)
            );
            """,

            // 4 -> 5: who a follow task is following. A version 4 file has no follows.
            """
            ALTER TABLE tasks ADD COLUMN target_id INTEGER;
            """,

            // 5 -> 6: what a talk_to task wants to say. A version 5 file has no talk_to tasks.
            """
            ALTER TABLE tasks ADD COLUMN reason TEXT;
            """,

            // 6 -> 7: a patrol task's stops, in walking order. A version 6 file has no patrols.
            """
            CREATE TABLE task_stops (
                npc_id   INTEGER NOT NULL REFERENCES npcs(npc_id),
                task_seq INTEGER NOT NULL,           -- the tasks row's seq
                seq      INTEGER NOT NULL,
                x        REAL    NOT NULL,
                y        REAL    NOT NULL,
                PRIMARY KEY (npc_id, task_seq, seq)
            );
            """,
        };

        // Every table, children before the tables they point at, so clearing them in this
        // order never leaves a row pointing at one that's gone.
        private static readonly string[] Tables =
        {
            "known_events", "standings", "opinions", "day_entries", "task_stops", "tasks", "journal_transcripts", "memories", "events", "transcript_lines",
            "transcript_participants", "transcripts", "npcs", "populated_levels", "players", "game",
        };

        // ------------------------------------------------------------------ save

        /// <summary>
        /// Writes <paramref name="save"/> as the whole save, replacing whatever was there.
        /// Creates the folder and the file if needed, and upgrades an older file first.
        /// Throws SqliteException or IOException if it can't write, and InvalidDataException
        /// if the file is from a newer version of the game (overwriting it would lose that save).
        /// </summary>
        public void Save(SaveData save)
        {
            string folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            using var db = Open(SqliteOpenMode.ReadWriteCreate);
            using var tx = db.BeginTransaction();

            int version = ReadVersion(db);
            if (version > SchemaVersion) throw new InvalidDataException(TooNewMessage(version));
            if (version == 0)   // a new file
            {
                Run(db, Version1Schema);
                version = 1;
            }
            Migrate(db, version);

            foreach (var table in Tables) Run(db, $"DELETE FROM {table};");
            Write(db, save);
            tx.Commit();   // nothing is changed on disk until here: all of it, or none
        }

        private static void Write(SqliteConnection db, SaveData save)
        {
            // Columns are named wherever a migration has added one: ADD COLUMN puts it last, and
            // naming them doesn't depend on that.
            Insert(db, "INSERT INTO game (id, saved_at, total_days, seconds_into_day, next_person_id, next_transcript_id, next_event_id) "
                     + "VALUES (1, $savedAt, $totalDays, $secondsIntoDay, $nextPersonId, $nextTranscriptId, $nextEventId)",
                ("$savedAt", save.SavedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)),
                ("$totalDays", save.TotalDays),
                ("$secondsIntoDay", save.SecondsIntoDay),
                ("$nextPersonId", save.NextPersonId),
                ("$nextTranscriptId", save.NextTranscriptId),
                ("$nextEventId", save.NextEventId));

            foreach (var p in save.Players)
            {
                Insert(db, "INSERT INTO players (player_id, name, level, x, y, factions) VALUES ($id, $name, $level, $x, $y, $factions)",
                    ("$id", p.Id), ("$name", p.Name), ("$level", p.Level), ("$x", p.X), ("$y", p.Y),
                    ("$factions", string.Join(",", p.Factions)));
                foreach (var standing in p.Standings)
                    Insert(db, "INSERT INTO standings (player_id, faction, score) VALUES ($id, $faction, $score)",
                        ("$id", p.Id), ("$faction", standing.Faction), ("$score", standing.Score));
            }

            foreach (var level in save.PopulatedLevels)
                Insert(db, "INSERT INTO populated_levels VALUES ($level)", ("$level", level));

            // Transcripts before the journals that point at them.
            foreach (var t in save.Transcripts)
            {
                Insert(db, "INSERT INTO transcripts VALUES ($id, $day, $location, $summary)",
                    ("$id", t.Id), ("$day", t.Day), ("$location", t.Location), ("$summary", t.Summary));
                for (int i = 0; i < t.Participants.Count; i++)
                    Insert(db, "INSERT INTO transcript_participants VALUES ($t, $seq, $person, $name)",
                        ("$t", t.Id), ("$seq", i), ("$person", t.Participants[i].Id), ("$name", t.Participants[i].Name));
                for (int i = 0; i < t.Lines.Count; i++)
                    Insert(db, "INSERT INTO transcript_lines VALUES ($t, $seq, $text)",
                        ("$t", t.Id), ("$seq", i), ("$text", t.Lines[i]));
            }

            // Events before the memories that point at them.
            foreach (var e in save.Events)
                Insert(db, "INSERT INTO events (event_id, day, level, x, y, type, actor_id, target_id, transcript_id, text) "
                         + "VALUES ($id, $day, $level, $x, $y, $type, $actor, $target, $transcript, $text)",
                    ("$id", e.Id), ("$day", e.Day), ("$level", e.Level), ("$x", e.X), ("$y", e.Y), ("$type", e.Type),
                    ("$actor", e.ActorId), ("$target", e.TargetId), ("$transcript", e.TranscriptId), ("$text", e.Text));

            foreach (var npc in save.Npcs)
            {
                Insert(db, "INSERT INTO npcs (npc_id, type, name, level, x, y, last_reflected_day, factions) "
                         + "VALUES ($id, $type, $name, $level, $x, $y, $lastReflected, $factions)",
                    ("$id", npc.Id), ("$type", npc.Type), ("$name", npc.Name), ("$level", npc.Level), ("$x", npc.X), ("$y", npc.Y),
                    ("$lastReflected", npc.LastReflectedDay), ("$factions", string.Join(",", npc.Factions)));
                foreach (var o in npc.Opinions)
                    Insert(db, "INSERT INTO opinions (owner_id, about, score, why) VALUES ($npc, $about, $score, $why)",
                        ("$npc", npc.Id), ("$about", o.About), ("$score", o.Score), ("$why", o.Why));
                for (int i = 0; i < npc.Memories.Count; i++)
                {
                    var m = npc.Memories[i];
                    Insert(db, "INSERT INTO memories (npc_id, seq, text, kind, transcript_id, day, event_id, teller_id, hops) "
                             + "VALUES ($npc, $seq, $text, $kind, $transcript, $day, $event, $teller, $hops)",
                        ("$npc", npc.Id), ("$seq", i), ("$text", m.Text), ("$kind", m.Kind), ("$transcript", m.TranscriptId),
                        ("$day", m.Day), ("$event", m.EventId), ("$teller", m.TellerId), ("$hops", m.Hops));
                }
                for (int i = 0; i < npc.DayEntries.Count; i++)
                    Insert(db, "INSERT INTO day_entries (npc_id, seq, day, summary) VALUES ($npc, $seq, $day, $summary)",
                        ("$npc", npc.Id), ("$seq", i), ("$day", npc.DayEntries[i].Day), ("$summary", npc.DayEntries[i].Summary));
                foreach (var known in npc.KnownEvents)
                    Insert(db, "INSERT INTO known_events (npc_id, event_id, day) VALUES ($npc, $event, $day)",
                        ("$npc", npc.Id), ("$event", known.EventId), ("$day", known.Day));
                foreach (var transcriptId in npc.TranscriptIds)
                    Insert(db, "INSERT INTO journal_transcripts VALUES ($npc, $transcript)",
                        ("$npc", npc.Id), ("$transcript", transcriptId));
                for (int i = 0; i < npc.Tasks.Count; i++)
                {
                    var task = npc.Tasks[i];
                    Insert(db, "INSERT INTO tasks VALUES ($npc, $seq, $type, $x, $y, $seconds, $target, $reason)",
                        ("$npc", npc.Id), ("$seq", i), ("$type", task.Type), ("$x", task.X), ("$y", task.Y), ("$seconds", task.Seconds),
                        ("$target", task.TargetId), ("$reason", task.Reason));
                    for (int s = 0; s < task.Stops.Count; s++)
                        Insert(db, "INSERT INTO task_stops VALUES ($npc, $task, $seq, $x, $y)",
                            ("$npc", npc.Id), ("$task", i), ("$seq", s), ("$x", task.Stops[s].X), ("$y", task.Stops[s].Y));
                }
            }
        }

        // ------------------------------------------------------------------ load

        /// <summary>
        /// Reads the save. Never throws: false, with a sentence for the player in
        /// <paramref name="problem"/>, if there's no save, it isn't a save, it's damaged or
        /// it's from a newer version of the game. An older file is upgraded as it's opened.
        /// </summary>
        public bool TryLoad(out SaveData save, out string problem)
        {
            save = null;
            if (!HasSave)
            {
                problem = "There is no save yet.";
                return false;
            }

            try
            {
                using var db = Open(SqliteOpenMode.ReadWrite);   // ReadWrite, not Create: never makes an empty file

                int version = ReadVersion(db);
                problem = VersionProblem(version);
                if (problem != null) return false;
                if (version < SchemaVersion)
                {
                    using var tx = db.BeginTransaction();
                    Migrate(db, version);
                    tx.Commit();
                }

                save = Read(db);
                problem = save == null ? "The save file is empty." : null;
                return save != null;
            }
            catch (Exception e) when (e is SqliteException or IOException or UnauthorizedAccessException
                                         or FormatException or InvalidCastException)
            {
                // SqliteException covers "file is not a database" and a missing table or column.
                save = null;
                problem = CouldNotRead(e);
                return false;
            }
        }

        /// <summary>
        /// What the save slots screen shows about this file - who, where, which day, when saved -
        /// read without loading the save. Never throws: a file that can't be read gives a summary
        /// with <see cref="SaveSummary.Problem"/> set, so it still gets a row (and can be deleted).
        ///
        /// Opens the file read-only and never upgrades it: listing saves shouldn't write to them.
        /// An older file's columns may differ, so if a migration ever renames one of these, read
        /// it the old way here too.
        /// </summary>
        public SaveSummary ReadSummary()
        {
            if (!HasSave) return SaveSummary.Unreadable(FilePath, "The save file isn't there any more.");

            try
            {
                using var db = Open(SqliteOpenMode.ReadOnly);

                string problem = VersionProblem(ReadVersion(db));
                if (problem != null) return SaveSummary.Unreadable(FilePath, problem);

                var game = Rows(db, "SELECT saved_at, total_days FROM game WHERE id = 1",
                    r => (savedAt: ParseSavedAt(r.GetString(0)), totalDays: r.GetInt32(1)));
                if (game.Count == 0) return SaveSummary.Unreadable(FilePath, "The save file is empty.");

                // The save's owner, as World.FromSave picks them.
                var owner = Rows(db, "SELECT name, level FROM players ORDER BY player_id LIMIT 1",
                    r => (name: r.GetString(0), level: r.GetString(1)));
                if (owner.Count == 0) return SaveSummary.Unreadable(FilePath, "The save has no player in it.");

                return new SaveSummary
                {
                    FilePath = FilePath,
                    SavedAt = game[0].savedAt,
                    TotalDays = game[0].totalDays,
                    PlayerName = owner[0].name,
                    Level = owner[0].level,
                };
            }
            catch (Exception e) when (e is SqliteException or IOException or UnauthorizedAccessException
                                         or FormatException or InvalidCastException)
            {
                return SaveSummary.Unreadable(FilePath, CouldNotRead(e));
            }
        }

        private static string CouldNotRead(Exception e) => $"The save file couldn't be read ({e.Message}).";

        private static DateTime ParseSavedAt(string text) =>
            DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        // Null if the game row is missing (a file with the tables but nothing saved in them).
        private static SaveData Read(SqliteConnection db)
        {
            var save = Rows(db, "SELECT saved_at, total_days, seconds_into_day, next_person_id, next_transcript_id, next_event_id FROM game WHERE id = 1",
                r => new SaveData
                {
                    SavedAt = ParseSavedAt(r.GetString(0)),
                    TotalDays = r.GetInt32(1),
                    SecondsIntoDay = r.GetFloat(2),
                    NextPersonId = r.GetInt32(3),
                    NextTranscriptId = r.GetInt32(4),
                    NextEventId = r.GetInt32(5),
                }).SingleOrDefault();
            if (save == null) return null;

            var standings = Rows(db, "SELECT player_id, faction, score FROM standings ORDER BY player_id, faction",
                r => (player: r.GetInt32(0), standing: new StandingSave { Faction = r.GetString(1), Score = r.GetInt32(2) }))
                .ToLookup(s => s.player, s => s.standing);
            save.Players = Rows(db, "SELECT player_id, name, level, x, y, factions FROM players ORDER BY player_id",
                r => new PlayerSave
                {
                    Id = r.GetInt32(0), Name = r.GetString(1), Level = r.GetString(2), X = r.GetFloat(3), Y = r.GetFloat(4),
                    Factions = SplitFactions(r.GetString(5)),
                    Standings = standings[r.GetInt32(0)].ToList(),
                });

            save.PopulatedLevels = Rows(db, "SELECT level FROM populated_levels ORDER BY level", r => r.GetString(0));

            // Each transcript's participants and lines, then the transcripts themselves.
            var participants = Rows(db, "SELECT transcript_id, person_id, name FROM transcript_participants ORDER BY transcript_id, seq",
                r => (transcript: r.GetInt32(0), person: new ParticipantSave { Id = r.GetInt32(1), Name = r.GetString(2) }))
                .ToLookup(p => p.transcript, p => p.person);
            var lines = Rows(db, "SELECT transcript_id, text FROM transcript_lines ORDER BY transcript_id, seq",
                r => (transcript: r.GetInt32(0), text: r.GetString(1)))
                .ToLookup(l => l.transcript, l => l.text);
            save.Transcripts = Rows(db, "SELECT id, day, location, summary FROM transcripts ORDER BY id",
                r => new TranscriptSave
                {
                    Id = r.GetInt32(0),
                    Day = r.GetInt32(1),
                    Location = r.IsDBNull(2) ? null : r.GetString(2),
                    Summary = r.IsDBNull(3) ? null : r.GetString(3),
                    Participants = participants[r.GetInt32(0)].ToList(),
                    Lines = lines[r.GetInt32(0)].ToList(),
                });

            save.Events = Rows(db, "SELECT event_id, day, level, x, y, type, actor_id, target_id, transcript_id, text FROM events ORDER BY event_id",
                r => new EventSave
                {
                    Id = r.GetInt32(0),
                    Day = r.GetInt32(1),
                    Level = r.IsDBNull(2) ? null : r.GetString(2),
                    X = r.GetFloat(3),
                    Y = r.GetFloat(4),
                    Type = r.GetString(5),
                    ActorId = r.IsDBNull(6) ? null : r.GetInt32(6),
                    TargetId = r.IsDBNull(7) ? null : r.GetInt32(7),
                    TranscriptId = r.IsDBNull(8) ? null : r.GetInt32(8),
                    Text = r.GetString(9),
                });

            // Each NPC's memories, conversations, tasks and diary, then the NPCs themselves.
            var memories = Rows(db, "SELECT npc_id, text, kind, transcript_id, day, event_id, teller_id, hops FROM memories ORDER BY npc_id, seq",
                r => (npc: r.GetInt32(0), memory: new MemorySave
                {
                    Text = r.GetString(1),
                    Kind = r.GetString(2),
                    TranscriptId = r.IsDBNull(3) ? null : r.GetInt32(3),
                    Day = r.GetInt32(4),
                    EventId = r.IsDBNull(5) ? null : r.GetInt32(5),
                    TellerId = r.IsDBNull(6) ? null : r.GetInt32(6),
                    Hops = r.GetInt32(7),
                }))
                .ToLookup(m => m.npc, m => m.memory);
            // People first, then factions, as they were saved; by key within each.
            var opinions = Rows(db, "SELECT owner_id, about, score, why FROM opinions ORDER BY owner_id, about LIKE 'faction:%', about",
                r => (npc: r.GetInt32(0), opinion: new OpinionSave { About = r.GetString(1), Score = r.GetInt32(2), Why = r.IsDBNull(3) ? null : r.GetString(3) }))
                .ToLookup(o => o.npc, o => o.opinion);
            var days = Rows(db, "SELECT npc_id, day, summary FROM day_entries ORDER BY npc_id, seq",
                r => (npc: r.GetInt32(0), entry: new DayEntrySave { Day = r.GetInt32(1), Summary = r.GetString(2) }))
                .ToLookup(d => d.npc, d => d.entry);
            var known = Rows(db, "SELECT npc_id, event_id, day FROM known_events ORDER BY npc_id, event_id",
                r => (npc: r.GetInt32(0), known: new KnownEventSave { EventId = r.GetInt32(1), Day = r.GetInt32(2) }))
                .ToLookup(k => k.npc, k => k.known);
            // Transcript ids only count up, so in id order is the order the NPC heard them in.
            var journals = Rows(db, "SELECT npc_id, transcript_id FROM journal_transcripts ORDER BY npc_id, transcript_id",
                r => (npc: r.GetInt32(0), transcript: r.GetInt32(1)))
                .ToLookup(j => j.npc, j => j.transcript);
            var stops = Rows(db, "SELECT npc_id, task_seq, x, y FROM task_stops ORDER BY npc_id, task_seq, seq",
                r => (task: (r.GetInt32(0), r.GetInt32(1)), stop: new StopSave { X = r.GetFloat(2), Y = r.GetFloat(3) }))
                .ToLookup(s => s.task, s => s.stop);
            var tasks = Rows(db, "SELECT npc_id, type, x, y, seconds, target_id, reason, seq FROM tasks ORDER BY npc_id, seq",
                r => (npc: r.GetInt32(0), task: new TaskSave
                {
                    Type = r.GetString(1),
                    X = r.IsDBNull(2) ? null : r.GetFloat(2),
                    Y = r.IsDBNull(3) ? null : r.GetFloat(3),
                    Seconds = r.IsDBNull(4) ? null : r.GetFloat(4),
                    TargetId = r.IsDBNull(5) ? null : r.GetInt32(5),
                    Reason = r.IsDBNull(6) ? null : r.GetString(6),
                    Stops = stops[(r.GetInt32(0), r.GetInt32(7))].ToList(),
                }))
                .ToLookup(t => t.npc, t => t.task);
            save.Npcs = Rows(db, "SELECT npc_id, type, name, level, x, y, last_reflected_day, factions FROM npcs ORDER BY npc_id",
                r => new NpcSave
                {
                    Id = r.GetInt32(0),
                    Type = r.GetString(1),
                    Name = r.GetString(2),
                    Level = r.GetString(3),
                    X = r.GetFloat(4),
                    Y = r.GetFloat(5),
                    Memories = memories[r.GetInt32(0)].ToList(),
                    TranscriptIds = journals[r.GetInt32(0)].ToList(),
                    Tasks = tasks[r.GetInt32(0)].ToList(),
                    DayEntries = days[r.GetInt32(0)].ToList(),
                    LastReflectedDay = r.GetInt32(6),
                    KnownEvents = known[r.GetInt32(0)].ToList(),
                    Factions = SplitFactions(r.GetString(7)),
                    Opinions = opinions[r.GetInt32(0)].ToList(),
                });

            return save;
        }

        private static List<string> SplitFactions(string text) =>
            text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // ------------------------------------------------------------------ versions

        // Brings an older file up to SchemaVersion, one migration at a time. Always called inside
        // a transaction, so a crash part-way leaves the file at its old version, not in between.
        private static void Migrate(SqliteConnection db, int version)
        {
            if (version >= SchemaVersion) return;
            for (int v = version; v < SchemaVersion; v++) Run(db, Migrations[v - 1]);
            Run(db, $"PRAGMA user_version = {SchemaVersion};");
        }

        // Why a file at this version can't be read, or null if it can (perhaps after migrating).
        private static string VersionProblem(int version) =>
            version == 0 ? "The save file isn't an Ashenveil save."
            : version > SchemaVersion ? TooNewMessage(version)
            : null;

        private static string TooNewMessage(int version) =>
            $"The save is from a newer version of the game (save format {version}; this game reads up to {SchemaVersion}).";

        private static int ReadVersion(SqliteConnection db)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ plumbing

        private SqliteConnection Open(SqliteOpenMode mode)
        {
            var db = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = FilePath,
                Mode = mode,
                // No pooling: a pooled connection keeps the file open after Dispose, so it
                // couldn't be moved or deleted (by the tests, or by the player) while the game runs.
                Pooling = false,
            }.ToString());
            db.Open();
            // SQLite only checks REFERENCES when asked, once per connection.
            Run(db, "PRAGMA foreign_keys = ON;");
            return db;
        }

        private static void Run(SqliteConnection db, string sql)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // Parameters, never values pasted into the SQL: a name with a quote in it is just a name.
        private static void Insert(SqliteConnection db, string sql, params (string name, object value)[] values)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static List<T> Rows<T>(SqliteConnection db, string sql, Func<SqliteDataReader, T> read)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var rows = new List<T>();
            while (reader.Read()) rows.Add(read(reader));
            return rows;
        }
    }
}
