using System;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Saving;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// Saving and loading from the menus: the save slots screen, and the questions asked before
    /// overwriting, deleting, or loading over unsaved play. Opened from the main menu's Continue,
    /// the pause menu's Save and Load, and the console's `load`.
    /// </summary>
    public class SaveFlow
    {
        private readonly ScreenManager _screens;
        private readonly SpriteFont _font;
        private readonly GraphicsDevice _graphics;
        private readonly Func<World> _world;
        private readonly Action<World> _play;
        private readonly Action _backToMenu;
        private readonly Action<string, bool> _showOnPause;
        private readonly SaveSlotsScreen _slots;

        // Every save, in %LOCALAPPDATA%\Ashenveil\saves. Only the pause menu's Save (and the console's `save`) writes to it.
        public SaveLibrary Saves { get; }

        /// <param name="world">The game being played; null on the main menu.</param>
        /// <param name="play">Starts playing a loaded world, on top of the main menu.</param>
        /// <param name="backToMenu">Closes every screen down to the main menu.</param>
        /// <param name="showOnPause">Shows a message (good or not) on the pause menu.</param>
        public SaveFlow(ScreenManager screens, SpriteFont font, GraphicsDevice graphics, SaveLibrary saves,
                        Func<World> world, Action<World> play, Action backToMenu, Action<string, bool> showOnPause)
        {
            _screens = screens;
            _font = font;
            _graphics = graphics;
            Saves = saves;
            _world = world;
            _play = play;
            _backToMenu = backToMenu;
            _showOnPause = showOnPause;
            _slots = new SaveSlotsScreen(font, SaveToNewSlot, SlotPicked, SlotDeleted, () => _screens.Pop());
        }

        // The main menu's Continue, and the pause menu's Load: pick a save to carry on.
        public void OpenLoad() => OpenSlots(SaveSlotsScreen.Purpose.Load);

        // The pause menu's Save. Checked before the list opens, so the player is never shown
        // saves they can't save over.
        public void SaveFromPause()
        {
            if (Saves.CanSave(_world(), out var why)) OpenSlots(SaveSlotsScreen.Purpose.Save);
            else _showOnPause(why, false);
        }

        // The console's `load <n>`: no question asked, it's a dev tool. It closes the console
        // with everything else, so its answer is there next time the console opens.
        public (bool ok, string message) LoadFromConsole(SaveSummary save) =>
            LoadSave(save, out var problem) ? (true, "Loaded the save.") : (false, problem);

        private void OpenSlots(SaveSlotsScreen.Purpose purpose)
        {
            _slots.Open(purpose, Saves.List(), _graphics.Viewport.Width, _graphics.Viewport.Height);
            _screens.Push(_slots);
        }

        private void Confirm(string question, string yesText, Action onYes) =>
            ConfirmScreen.Ask(_screens, _font, question, yesText, onYes, _graphics.Viewport.Width, _graphics.Viewport.Height);

        // "+ New save": save, then back to the pause menu saying so.
        private void SaveToNewSlot()
        {
            if (Saves.TrySaveNew(_world(), out var message)) Saved(message);
            else _slots.ShowMessage(message, good: false);
        }

        // A save was picked: overwrite it, or carry it on - each asking first.
        private void SlotPicked(SaveSummary save)
        {
            var world = _world();
            if (_slots.OpenFor == SaveSlotsScreen.Purpose.Save)
            {
                Confirm("Overwrite this save?", "Overwrite", () =>
                {
                    if (Saves.TrySaveOver(world, save, out var message)) Saved(message);
                    else
                    {
                        _slots.Show(Saves.List());   // it may have been damaged, or gone
                        _slots.ShowMessage(message, good: false);
                    }
                });
            }
            // Loading throws away everything since the last save, so with unsaved play it asks
            // first, as quitting does. From the main menu there's no game to lose.
            else if (world != null && world.HasUnsavedPlay)
                Confirm("You've played since you last saved. Load this save and lose that?", "Load", () => LoadSlot(save));
            else
                LoadSlot(save);
        }

        private void SlotDeleted(SaveSummary save)
        {
            Confirm("Delete this save? This can't be undone.", "Delete", () =>
            {
                bool deleted = Saves.TryDelete(save, out var problem);
                _slots.Show(Saves.List());
                _slots.ShowMessage(deleted ? "Save deleted." : problem, deleted);
            });
        }

        // Saved from the slots screen: close it and say so on the pause menu underneath.
        private void Saved(string message)
        {
            _screens.Pop();
            _showOnPause(message, true);
        }

        // Carries on the chosen save, or stays on the list saying why it can't.
        private void LoadSlot(SaveSummary save)
        {
            if (!LoadSave(save, out var problem)) _slots.ShowMessage(problem, good: false);
        }

        /// <summary>
        /// Throws away the game being played (if any) and carries on the saved one: closes every
        /// screen down to the main menu (the game, and the pause menu, list or console over it),
        /// then plays the loaded world. False, with the reason, if the save can't be loaded -
        /// then nothing is closed and everything carries on as it was.
        /// </summary>
        private bool LoadSave(SaveSummary save, out string problem)
        {
            if (!Saves.TryLoad(save, out var loaded, out problem)) return false;
            _backToMenu();
            _play(loaded);
            return true;
        }
    }
}
