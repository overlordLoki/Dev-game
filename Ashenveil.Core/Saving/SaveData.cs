using System;
using System.Collections.Generic;

namespace Ashenveil.Core.Saving
{
    /// <summary>
    /// One save, as plain data: everything needed to rebuild the world, and nothing that can be
    /// rebuilt from the level files (tiles, objects, POIs, nav grids). The boundary between the
    /// two halves of saving: SaveMapper turns the game into this and back, SaveStore writes it
    /// to SQLite and reads it again, and neither knows about the other.
    ///
    /// Like the level DTOs (MapData), these hold only values - never entities, textures or a
    /// Location - so a save can be built, compared and tested without a game running.
    /// Positions are feet (bottom-centre of the body), in cells.
    /// </summary>
    public class SaveData
    {
        public DateTime SavedAt { get; set; }            // UTC
        public int TotalDays { get; set; }               // GameClock.TotalDays
        public float SecondsIntoDay { get; set; }        // GameClock.SecondsIntoDay
        public int NextPersonId { get; set; }            // NpcRoster.PeekNextId: the next player or NPC id
        public int NextTranscriptId { get; set; }        // ConversationSystem.PeekNextTranscriptId

        // A list, not one player, so multiplayer won't need a new format. Players[0] is the owner today.
        public List<PlayerSave> Players { get; set; } = new();
        public List<string> PopulatedLevels { get; set; } = new();
        public List<NpcSave> Npcs { get; set; } = new();
        // Each conversation once, however many NPCs remember it. NpcSave.TranscriptIds points here.
        public List<TranscriptSave> Transcripts { get; set; } = new();
    }

    public class PlayerSave
    {
        public int Id { get; set; }                      // IEntity.Id; 0 is the save's owner
        public string Name { get; set; }
        public string Level { get; set; }
        public float X { get; set; }                     // feet, in cells
        public float Y { get; set; }
    }

    public class NpcSave
    {
        public int Id { get; set; }                      // IEntity.Id
        public string Type { get; set; }                 // "Knight": which class to build on load
        public string Name { get; set; }
        public string Level { get; set; }
        public float X { get; set; }                     // feet, in cells
        public float Y { get; set; }
        public List<MemorySave> Memories { get; set; } = new();   // oldest first
        public List<int> TranscriptIds { get; set; } = new();     // oldest first
        public List<TaskSave> Tasks { get; set; } = new();        // the queue, front first
    }

    /// <summary>
    /// One task in an NPC's queue. Each type fills only what it needs to carry on:
    ///   go_to: X, Y (the target, in cells)      wait: Seconds (time left)
    /// The type names are the decision schema's, so a new task type adds one branch there
    /// and one in SaveMapper.
    /// </summary>
    public class TaskSave
    {
        public string Type { get; set; }                 // "go_to", "wait"
        public float? X { get; set; }
        public float? Y { get; set; }
        public float? Seconds { get; set; }
    }

    public class MemorySave
    {
        public string Text { get; set; }
        public string Kind { get; set; }                 // MemoryKind's name: "Conversation", "Event"
        public int? TranscriptId { get; set; }           // Memory.SourceId
    }

    public class TranscriptSave
    {
        public int Id { get; set; }
        public int Day { get; set; }
        public string Location { get; set; }             // null if it happened nowhere in particular
        public string Summary { get; set; }              // null if the decision never came back
        public List<ParticipantSave> Participants { get; set; } = new();
        public List<string> Lines { get; set; } = new();
    }

    public class ParticipantSave
    {
        public int Id { get; set; }                      // who it was: a player or NPC id
        public string Name { get; set; }                 // what they were called at the time
    }
}
