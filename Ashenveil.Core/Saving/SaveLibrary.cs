using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.GameWorld;
using Microsoft.Data.Sqlite;

namespace Ashenveil.Core.Saving
{
    /// <summary>
    /// The saves folder, as the menus see it: every save in it, and "save", "load" and "delete",
    /// each answering with a sentence to show the player instead of throwing. A save is any .db
    /// file in the folder; new ones are called save-1.db, save-2.db... but the name is never
    /// shown - a row's details come from inside the file (SaveStore.ReadSummary).
    ///
    /// Joins the two halves of saving - SaveMapper (game to save classes) and SaveStore (save
    /// classes to one file) - and adds the rules that are about saving rather than either half:
    /// no saving mid-conversation, and a save clears the world's unsaved play.
    /// </summary>
    public class SaveLibrary
    {
        public const string Extension = ".db";

        // save-12.db -> 12. Only names this class made count when picking the next one; any
        // other .db in the folder (the old slot1.db) is still listed, just never numbered.
        private static readonly Regex Numbered = new(@"^save-(\d+)\.db$", RegexOptions.IgnoreCase);

        public string Folder { get; }

        public SaveLibrary(string folder) => Folder = folder;

        // ------------------------------------------------------------------ what's there

        /// <summary>
        /// Every save in the folder, newest first, then the ones that can't be read (so the
        /// player can still see and delete them). Never throws: a missing or unreadable folder
        /// means no saves.
        /// </summary>
        public IReadOnlyList<SaveSummary> List() =>
            SaveFiles()
                .Select(path => new SaveStore(path).ReadSummary())
                .OrderBy(s => s.IsReadable ? 0 : 1)
                .ThenByDescending(s => s.SavedAt)
                .ThenBy(s => s.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Whether there's anything to offer Continue for. Doesn't open the files.</summary>
        public bool HasSaves => SaveFiles().Any();

        /// <summary>
        /// Where the next new save goes: one more than the highest save-N.db there is. A gap
        /// left by a deleted save isn't refilled, so in the folder the newest is always the
        /// biggest number.
        /// </summary>
        public string NextNewFilePath()
        {
            int highest = SaveFiles()
                .Select(path => Numbered.Match(Path.GetFileName(path)))
                .Where(m => m.Success)
                .Select(m => int.TryParse(m.Groups[1].Value, out int n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
            return Path.Combine(Folder, $"save-{highest + 1}{Extension}");
        }

        private IEnumerable<string> SaveFiles()
        {
            try
            {
                if (!Directory.Exists(Folder)) return Array.Empty<string>();
                // Checked by hand as well: on Windows "*.db" can also match longer extensions.
                return Directory.GetFiles(Folder, "*" + Extension)
                    .Where(path => string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        // ------------------------------------------------------------------ save

        /// <summary>
        /// Whether <paramref name="world"/> can be saved right now, and if not, why. The pause
        /// menu asks before opening the save screen, so the player isn't shown a list they can't use.
        /// </summary>
        public bool CanSave(World world, out string message)
        {
            // A conversation in progress isn't saved (nor is the decision it ends with), so
            // saving now would quietly lose it. Ask the player to finish it first.
            if (world.Conversations.Active.Count > 0)
            {
                message = "You can't save in the middle of a conversation.";
                return false;
            }
            message = null;
            return true;
        }

        /// <summary>Saves <paramref name="world"/> as a new save, leaving every other one alone.</summary>
        public bool TrySaveNew(World world, out string message) =>
            TrySave(world, NextNewFilePath(), replaceUnreadable: false, out message);

        /// <summary>
        /// Saves <paramref name="world"/> over the save <paramref name="over"/> describes (the
        /// screen has already asked "Overwrite?"). A save that couldn't be read is replaced
        /// with a fresh file, since there's nothing in it to keep.
        /// </summary>
        public bool TrySaveOver(World world, SaveSummary over, out string message) =>
            TrySave(world, over.FilePath, replaceUnreadable: !over.IsReadable, out message);

        private bool TrySave(World world, string path, bool replaceUnreadable, out string message)
        {
            if (!CanSave(world, out message)) return false;

            try
            {
                // SaveStore won't write into a file that isn't a save of this version (it could
                // be someone else's, or a newer game's) - but the player chose to overwrite it.
                if (replaceUnreadable && File.Exists(path)) File.Delete(path);
                new SaveStore(path).Save(SaveMapper.ToSave(world, DateTime.UtcNow));
            }
            catch (Exception e) when (e is SqliteException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                message = $"Couldn't save: {e.Message}";
                return false;
            }

            world.HasUnsavedPlay = false;
            message = "Game saved.";
            return true;
        }

        // ------------------------------------------------------------------ load

        /// <summary>
        /// The saved world, ready to play, or false with a sentence for the player: the file is
        /// gone, broken or too new, or a level it names can't be read any more.
        /// </summary>
        public bool TryLoad(SaveSummary save, out World world, out string problem,
                            Func<ConversationRequestDto, Task<ChatMessage[]>> sendChat = null,
                            Func<DecisionRequestDto, Task<NpcDecisionDto>> requestDecision = null)
        {
            world = null;
            if (!new SaveStore(save.FilePath).TryLoad(out var data, out problem)) return false;

            try
            {
                world = World.FromSave(data, sendChat, requestDecision);
                return true;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
            {
                problem = $"The save couldn't be loaded ({e.Message}).";
                return false;
            }
        }

        // ------------------------------------------------------------------ delete

        /// <summary>
        /// Deletes the save (the screen has already asked). Deleting the one being played is
        /// fine: the game is in memory, it just has no save to go back to.
        /// </summary>
        public bool TryDelete(SaveSummary save, out string problem)
        {
            // File.Delete says nothing for a file that isn't there; the player should hear it.
            if (!File.Exists(save.FilePath))
            {
                problem = "That save isn't there any more.";
                return false;
            }

            try
            {
                File.Delete(save.FilePath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problem = $"Couldn't delete the save: {e.Message}";
                return false;
            }
            problem = null;
            return true;
        }
    }
}
