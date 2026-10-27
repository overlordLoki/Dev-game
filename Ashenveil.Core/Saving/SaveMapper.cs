using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Task;
using Ashenveil.Core.GameWorld;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Saving
{
    /// <summary>
    /// Game objects to save classes and back. Plain methods with no database and no GPU, so
    /// they can be tested with a World built from a level, the same way Conversation.ToTranscript is.
    ///
    /// Loading is done in pieces: transcripts first (once, shared), then each NPC is rebuilt
    /// (NpcFactory) with its journal filled from them and its task queue. Putting NPCs in their
    /// levels and the player and clock back is World's load path.
    /// </summary>
    public static class SaveMapper
    {
        // ------------------------------------------------------------------ game -> save

        /// <summary>Everything about this world worth keeping, as of now.</summary>
        public static SaveData ToSave(World world, DateTime savedAtUtc)
        {
            var npcs = world.Roster.All.ToList();
            return new SaveData
            {
                SavedAt = savedAtUtc,
                TotalDays = world.Clock.TotalDays,
                SecondsIntoDay = world.Clock.SecondsIntoDay,
                NextPersonId = world.Roster.PeekNextId,
                NextTranscriptId = world.Conversations.PeekNextTranscriptId,
                // The player is always in the level being played.
                Players = { ToSave(world.player, world.location.Name) },
                PopulatedLevels = world.Roster.PopulatedLevels.ToList(),
                Npcs = npcs.Select(npc => ToSave(npc, world.Roster.LevelOf(npc))).ToList(),
                // Two NPCs in one conversation hold the same Transcript: keep it once, by id.
                Transcripts = npcs.SelectMany(npc => npc.Journal.Transcripts)
                                  .DistinctBy(t => t.Id)
                                  .Select(ToSave)
                                  .ToList(),
            };
        }

        public static PlayerSave ToSave(Player player, string level) => new()
        {
            Id = player.Id,
            Name = player.Name,
            Level = level,
            X = player.Feet.X,
            Y = player.Feet.Y,
        };

        public static NpcSave ToSave(NPC npc, string level) => new()
        {
            Id = npc.Id,
            Type = npc.GetType().Name,   // the class name is what the NPC factory looks up
            Name = npc.Name,
            Level = level,
            X = npc.Feet.X,
            Y = npc.Feet.Y,
            Memories = npc.Journal.Memories.Select(ToSave).ToList(),
            TranscriptIds = npc.Journal.Transcripts.Select(t => t.Id).ToList(),
            // A task type saving doesn't know is left out, so the rest of the queue still loads.
            Tasks = npc.Tasks.Select(ToSave).Where(t => t != null).ToList(),
        };

        // Only what the task needs to carry on. Completed and failed tasks never get here:
        // they leave the queue the tick they finish.
        public static TaskSave ToSave(ITask task) => task switch
        {
            GoToTask go => new TaskSave { Type = "go_to", X = go.Target.X, Y = go.Target.Y },
            WaitTask wait => new TaskSave { Type = "wait", Seconds = wait.SecondsLeft },
            _ => null,
        };

        public static MemorySave ToSave(Memory memory) => new()
        {
            Text = memory.Text,
            Kind = memory.Kind.ToString(),
            TranscriptId = memory.SourceId,
        };

        public static TranscriptSave ToSave(Transcript transcript) => new()
        {
            Id = transcript.Id,
            Day = transcript.Day,
            Location = transcript.Location,
            Summary = transcript.Summary,
            Participants = transcript.Participants.Select(p => new ParticipantSave { Id = p.Id, Name = p.Name }).ToList(),
            Lines = transcript.Lines.ToList(),
        };

        // ------------------------------------------------------------------ save -> game

        /// <summary>
        /// Every saved conversation, rebuilt once and looked up by id. Hand the same dictionary
        /// to every <see cref="FillJournal"/> so NPCs who shared a conversation share one
        /// Transcript object again, as they did before saving.
        /// </summary>
        public static Dictionary<int, Transcript> TranscriptsFrom(SaveData save) =>
            save.Transcripts.ToDictionary(t => t.Id, FromSave);

        public static Transcript FromSave(TranscriptSave saved) => new(
            saved.Id,
            saved.Day,
            saved.Location,
            saved.Participants.Select(p => new Participant(p.Id, p.Name)).ToList(),
            saved.Lines.ToList())
        {
            Summary = saved.Summary,
        };

        /// <summary>
        /// Rebuilds a saved NPC: the right type and brain, standing where it was, with its
        /// journal and task queue. Null for a type the game doesn't know - that NPC is left out
        /// rather than failing the whole load. It isn't in any level yet; the caller adds it.
        /// </summary>
        public static NPC FromSave(NpcSave saved, IReadOnlyDictionary<int, Transcript> transcripts)
        {
            var npc = NpcFactory.Make(saved.Type, saved.Id, saved.Name);
            if (npc == null) return null;

            npc.StandAt(new Vector2(saved.X, saved.Y));
            FillJournal(npc.Journal, saved, transcripts);
            // GiveTask puts the NPC in the Tasked state, as it was when it was saved with work queued.
            foreach (var task in saved.Tasks.Select(FromSave).Where(t => t != null))
                npc.GiveTask(task);
            return npc;
        }

        /// <summary>The task a save describes, or null if its type is unknown or it's missing what that type needs.</summary>
        public static ITask FromSave(TaskSave saved) => saved.Type switch
        {
            "go_to" when saved.X is float x && saved.Y is float y => new GoToTask(new Vector2(x, y)),
            "wait" when saved.Seconds is float seconds => new WaitTask(seconds),
            _ => null,
        };

        /// <summary>
        /// Puts a saved NPC's memories and conversations into <paramref name="journal"/> (a
        /// freshly built NPC's, so it starts empty), in the order they were learned.
        /// </summary>
        public static void FillJournal(Journal journal, NpcSave saved, IReadOnlyDictionary<int, Transcript> transcripts)
        {
            foreach (var memory in saved.Memories) journal.Add(FromSave(memory));
            foreach (var id in saved.TranscriptIds)
            {
                // A journal pointing at a transcript that isn't in the save is a damaged file:
                // keep the rest of what the NPC remembers rather than fail the whole load.
                if (transcripts.TryGetValue(id, out var transcript)) journal.Add(transcript);
            }
        }

        public static Memory FromSave(MemorySave saved) =>
            // An unknown kind can't come from this version's saves (a newer file is refused
            // before it's read), so falling back to Event only covers a hand-edited file.
            new(saved.Text, Enum.TryParse<MemoryKind>(saved.Kind, out var kind) ? kind : MemoryKind.Event)
            {
                SourceId = saved.TranscriptId,
            };
    }
}
