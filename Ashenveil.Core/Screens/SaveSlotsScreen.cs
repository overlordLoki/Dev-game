using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Saving;
using Ashenveil.Core.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Screens
{
    /// <summary>
    /// The list of saves, to pick one from. Two purposes, one screen:
    ///   Save - a "New save" row first, then every save (picking one means overwrite);
    ///   Load - every save, to carry on.
    /// Each row shows who, where, which day and when it was saved, and has a Delete button. More
    /// rows than fit scroll, with the mouse wheel or the ^ and v buttons. Back or Escape leaves.
    ///
    /// Like ConfirmScreen it only reports what was chosen: whoever opened it saves, loads,
    /// deletes and asks "are you sure?", then hands back the new list with <see cref="Show"/>.
    /// So it needs no World and no files, and its row text is plain functions.
    ///
    /// Built once and kept; <see cref="Open"/> lays it out for the window each time it's shown,
    /// as SettingsScreen does.
    /// </summary>
    public class SaveSlotsScreen : IScreen
    {
        public enum Purpose { Save, Load }

        private const int Margin = 40, Pad = 20, TitleH = 40, RowH = 70, RowGap = 8, Gap = 12;
        private const int DeleteW = 120, BtnW = 200, BtnH = 50, ArrowW = 60, MessageH = 30, TextInset = 16;
        // Damaged rows say why; a long reason is cut so it doesn't run under the Delete button.
        private const int MaxDetailLength = 70;
        // One notch of a mouse wheel, as MonoGame counts it.
        private const int WheelNotch = 120;

        private static readonly Color Good = Color.LightGreen, Bad = Color.Salmon;

        public List<Widget> Widgets { get; set; } = new List<Widget>();
        private readonly SpriteFont _font;
        private readonly Action _onNew;
        private readonly Action<SaveSummary> _onPick, _onDelete;
        private readonly Action _onBack;

        private Purpose _purpose;
        private IReadOnlyList<SaveSummary> _saves = Array.Empty<SaveSummary>();
        private int _first;                 // the entry shown in the top row
        private int _screenWidth, _screenHeight;
        private string _message = "";
        private Color _messageColor = Good;
        private MouseState _prevMouse;
        private KeyboardState _prevKb;

        /// <param name="onNew">Save purpose: the "New save" row was picked.</param>
        /// <param name="onPick">A save was picked: overwrite it (Save) or carry it on (Load).</param>
        /// <param name="onDelete">A save's Delete was clicked.</param>
        /// <param name="onBack">Back or Escape.</param>
        public SaveSlotsScreen(SpriteFont font, Action onNew, Action<SaveSummary> onPick, Action<SaveSummary> onDelete, Action onBack)
        {
            _font = font;
            _onNew = onNew;
            _onPick = onPick;
            _onDelete = onDelete;
            _onBack = onBack;
        }

        /// <summary>
        /// Call before pushing the screen: sets what it's for and the saves to list, lays it out
        /// for the window as it is now, and starts from the keys and mouse held now, so the click
        /// that opened it isn't also read as picking a row.
        /// </summary>
        public void Open(Purpose purpose, IReadOnlyList<SaveSummary> saves, int screenWidth, int screenHeight)
        {
            _purpose = purpose;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _first = 0;
            _message = "";
            _prevMouse = AshenveilGame.Input.Mouse;
            _prevKb = AshenveilGame.Input.Keyboard;
            Show(saves);
        }

        /// <summary>The saves to list now, after a save or delete. Keeps the scroll where it was, if it still can.</summary>
        public void Show(IReadOnlyList<SaveSummary> saves)
        {
            _saves = saves;
            ScrollBy(0);   // clamps, then lays out
        }

        /// <summary>A line at the bottom: what just happened, or why it didn't.</summary>
        public void ShowMessage(string text, bool good)
        {
            _message = text ?? "";
            _messageColor = good ? Good : Bad;
            Layout();
        }

        public string Message => _message;

        /// <summary>What it was last opened for, so the caller knows what a picked save means.</summary>
        public Purpose OpenFor => _purpose;

        // ------------------------------------------------------------------ entries

        // Every row there is, scrolled or not: in Save, "New save" (null) then the saves.
        private int EntryCount => _saves.Count + (_purpose == Purpose.Save ? 1 : 0);

        private SaveSummary EntryAt(int entry) =>
            _purpose == Purpose.Save ? (entry == 0 ? null : _saves[entry - 1]) : _saves[entry];

        /// <summary>How many rows the window has room for.</summary>
        public int RowsThatFit
        {
            get
            {
                int listH = ListArea.Height;
                return Math.Max(1, (listH + RowGap) / (RowH + RowGap));
            }
        }

        /// <summary>The headline of each row on screen now, top to bottom ("New save" for that row).</summary>
        public IReadOnlyList<string> VisibleRows =>
            Enumerable.Range(_first, Math.Min(RowsThatFit, EntryCount - _first))
                      .Select(i => EntryAt(i) is { } save ? Headline(save) : "+ New save")
                      .ToList();

        /// <summary>Picks the row for this entry (0 is the top of the whole list, not of what's on screen).</summary>
        public void Choose(int entry)
        {
            if (entry < 0 || entry >= EntryCount) return;
            if (EntryAt(entry) is { } save) _onPick(save);
            else _onNew();
        }

        /// <summary>That entry's Delete button. The "New save" row has none.</summary>
        public void Delete(int entry)
        {
            if (entry < 0 || entry >= EntryCount) return;
            if (EntryAt(entry) is { } save) _onDelete(save);
        }

        /// <summary>Scrolls the list by whole rows (negative is up), never past either end.</summary>
        public void ScrollBy(int rows)
        {
            _first = Math.Clamp(_first + rows, 0, Math.Max(0, EntryCount - RowsThatFit));
            Layout();
        }

        public void Back() => _onBack();

        // ------------------------------------------------------------------ row text

        /// <summary>"Loki - Medows, Summer 11, Year 1", or "Damaged save" for one that can't be read.</summary>
        public static string Headline(SaveSummary save) =>
            save.IsReadable
                ? $"{save.PlayerName} - {save.Level}, {GameDate.OnDay(save.TotalDays).Display}"
                : "Damaged save";

        /// <summary>"Saved 27 Oct 2026, 14:32" in the player's own time zone, or why a damaged save can't be read.</summary>
        public static string Detail(SaveSummary save) => Detail(save, TimeZoneInfo.Local);

        /// <summary>As <see cref="Detail(SaveSummary)"/>, in a given time zone (tests pick one, so the answer doesn't depend on the machine).</summary>
        public static string Detail(SaveSummary save, TimeZoneInfo zone)
        {
            if (!save.IsReadable)
                return save.Problem.Length <= MaxDetailLength ? save.Problem : save.Problem[..(MaxDetailLength - 3)] + "...";

            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(save.SavedAt, DateTimeKind.Utc), zone);
            return "Saved " + local.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ layout

        private Rectangle Box => new(Margin, Margin, _screenWidth - Margin * 2, _screenHeight - Margin * 2);

        // Between the title and the message line.
        private Rectangle ListArea
        {
            get
            {
                var box = Box;
                int top = box.Y + Pad + TitleH + Gap;
                int bottom = box.Bottom - Pad - BtnH - Gap - MessageH - Gap;
                return new Rectangle(box.X + Pad, top, box.Width - Pad * 2, Math.Max(0, bottom - top));
            }
        }

        // Rebuilt whenever what's shown changes (opened, scrolled, a new list, a message):
        // simpler than moving widgets about, and there are only a handful.
        private void Layout()
        {
            var box = Box;
            var list = ListArea;
            int bottomY = box.Bottom - Pad - BtnH;

            Widgets = new List<Widget>
            {
                new Panel(box, "panel_brown_dark"),
                new Label(new Rectangle(box.X, box.Y + Pad, box.Width, TitleH),
                          _purpose == Purpose.Save ? "Save Game" : "Load Game", Color.Gold),
            };

            if (EntryCount == 0)
                Widgets.Add(new Label(list, "No saves yet.", Color.LightGray));

            int rows = Math.Min(RowsThatFit, EntryCount - _first);
            for (int row = 0; row < rows; row++)
            {
                int entry = _first + row;
                var rect = new Rectangle(list.X, list.Y + row * (RowH + RowGap), list.Width - DeleteW - Gap, RowH);
                var save = EntryAt(entry);

                if (save == null)
                {
                    Widgets.Add(new Button(rect, "+ New save", "button_brown", () => Choose(entry)));
                    continue;
                }

                // The row is a button with no caption, the two lines drawn over it. Delete sits
                // beside it, not on it, so clicking Delete never also picks the row.
                Widgets.Add(new Button(rect, "", "button_brown", () => Choose(entry)));
                int textW = rect.Width - TextInset * 2;
                Widgets.Add(new Label(new Rectangle(rect.X + TextInset, rect.Y + 4, textW, RowH / 2 - 4),
                                      Headline(save), save.IsReadable ? Color.Black : Color.DarkRed, centred: false));
                Widgets.Add(new Label(new Rectangle(rect.X + TextInset, rect.Y + RowH / 2, textW, RowH / 2 - 4),
                                      Detail(save), Color.DimGray, centred: false));
                Widgets.Add(new Button(new Rectangle(rect.Right + Gap, rect.Y + (RowH - BtnH) / 2, DeleteW, BtnH),
                                       "Delete", "button_brown", () => Delete(entry)));
            }

            Widgets.Add(new Label(new Rectangle(box.X, bottomY - Gap - MessageH, box.Width, MessageH), _message, _messageColor));

            // Scroll buttons only when there's more than fits.
            if (EntryCount > RowsThatFit)
            {
                Widgets.Add(new Button(new Rectangle(box.X + Pad, bottomY, ArrowW, BtnH), "^", "button_brown", () => ScrollBy(-1)));
                Widgets.Add(new Button(new Rectangle(box.X + Pad + ArrowW + Gap, bottomY, ArrowW, BtnH), "v", "button_brown", () => ScrollBy(1)));
            }
            Widgets.Add(new Button(new Rectangle(box.Right - Pad - BtnW, bottomY, BtnW, BtnH), "Back", "button_brown", Back));
        }

        // ------------------------------------------------------------------ each frame

        public void Update(GameTime gameTime)
        {
            var mouse = AshenveilGame.Input.Mouse;
            // A copy: a click can rebuild the list (scrolling, or the caller's Show after a delete).
            foreach (var w in Widgets.ToArray()) w.Update(mouse, _prevMouse);

            // Wheel up is positive, and should move the list up. A touchpad sends less than a
            // whole notch at a time; that still counts as one row.
            int wheel = mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;
            if (wheel != 0) ScrollBy(-(wheel / WheelNotch == 0 ? Math.Sign(wheel) : wheel / WheelNotch));
            _prevMouse = mouse;

            var kb = AshenveilGame.Input.Keyboard;
            if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape)) Back();
            _prevKb = kb;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var w in Widgets) w.Draw(spriteBatch, _font);
        }
    }
}
