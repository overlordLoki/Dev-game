using Ashenveil.Core;
using Ashenveil.Core.Saving;
using Ashenveil.Core.Screens;
using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// The save slots screen's logic without a window, like SettingsScreenTests: which rows it
/// shows, what picking or deleting one reports, scrolling, and the row text. It never saves
/// or loads anything itself - the fakes below just record what it asked for. Layout and
/// drawing are checked by hand.
/// </summary>
public class SaveSlotsScreenTests : IDisposable
{
    private readonly FakeInput _input = new();
    private readonly SaveSlotsScreen _screen;
    private int _news, _backs;
    private readonly List<SaveSummary> _picked = new(), _deleted = new();

    public SaveSlotsScreenTests()
    {
        AshenveilGame.Input = _input;
        _screen = new SaveSlotsScreen(null, () => _news++, _picked.Add, _deleted.Add, () => _backs++);
    }

    public void Dispose() => AshenveilGame.Input = new LiveInput();

    private static SaveSummary Save(string player, int day = 1, string level = "Medows") => new()
    {
        FilePath = $"{player}.db",
        PlayerName = player,
        Level = level,
        TotalDays = day,
        SavedAt = new DateTime(2026, 10, 27, 9, 30, 0, DateTimeKind.Utc),
    };

    private static List<SaveSummary> Saves(int count) =>
        Enumerable.Range(1, count).Select(i => Save($"P{i}")).ToList();

    private void Frame() => _screen.Update(new GameTime());

    // ------------------------------------------------------------------ rows

    [Fact]
    public void Saving_offers_a_new_save_first_then_every_save()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Save, new[] { Save("Loki"), Save("Thor") }, 1280, 720);

        Assert.Equal(new[] { "+ New save", "Loki - Medows, Spring 1, Year 1", "Thor - Medows, Spring 1, Year 1" },
                     _screen.VisibleRows);
    }

    [Fact]
    public void Loading_lists_only_the_saves()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Load, new[] { Save("Loki") }, 1280, 720);

        Assert.Equal(new[] { "Loki - Medows, Spring 1, Year 1" }, _screen.VisibleRows);
    }

    [Fact]
    public void Loading_with_no_saves_shows_no_rows()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Load, Array.Empty<SaveSummary>(), 1280, 720);

        Assert.Empty(_screen.VisibleRows);
    }

    // ------------------------------------------------------------------ choosing

    [Fact]
    public void Picking_new_save_reports_new_and_picking_a_save_reports_that_save()
    {
        var thor = Save("Thor");
        _screen.Open(SaveSlotsScreen.Purpose.Save, new[] { Save("Loki"), thor }, 1280, 720);

        _screen.Choose(0);
        _screen.Choose(2);

        Assert.Equal(1, _news);
        Assert.Same(thor, Assert.Single(_picked));
    }

    [Fact]
    public void Delete_reports_that_save_and_picks_nothing()
    {
        var loki = Save("Loki");
        _screen.Open(SaveSlotsScreen.Purpose.Load, new[] { loki }, 1280, 720);

        _screen.Delete(0);

        Assert.Same(loki, Assert.Single(_deleted));
        Assert.Empty(_picked);
    }

    [Fact]
    public void The_new_save_row_cannot_be_deleted()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Save, new[] { Save("Loki") }, 1280, 720);

        _screen.Delete(0);

        Assert.Empty(_deleted);
    }

    [Fact]
    public void Escape_goes_back()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Load, new[] { Save("Loki") }, 1280, 720);

        _input.Hold(Keys.Escape);
        Frame();

        Assert.Equal(1, _backs);
    }

    [Fact]
    public void An_Escape_already_held_when_it_opens_is_not_a_back()
    {
        _input.Hold(Keys.Escape);   // the Escape that's still down from the menu underneath
        _screen.Open(SaveSlotsScreen.Purpose.Load, new[] { Save("Loki") }, 1280, 720);

        Frame();

        Assert.Equal(0, _backs);
    }

    // ------------------------------------------------------------------ scrolling

    [Fact]
    public void More_saves_than_fit_scroll_one_row_at_a_time_and_stop_at_each_end()
    {
        var saves = Saves(20);
        _screen.Open(SaveSlotsScreen.Purpose.Load, saves, 1280, 720);
        int fits = _screen.RowsThatFit;
        Assert.InRange(fits, 2, 19);   // a 720p window shows some, not all
        Assert.Equal(fits, _screen.VisibleRows.Count);

        _screen.ScrollBy(-1);
        Assert.StartsWith("P1 ", _screen.VisibleRows[0]);   // already at the top

        _screen.ScrollBy(1);
        Assert.StartsWith("P2 ", _screen.VisibleRows[0]);

        _screen.ScrollBy(100);
        Assert.StartsWith("P20 ", _screen.VisibleRows[^1]);   // the last row, and no further
        Assert.Equal(fits, _screen.VisibleRows.Count);
    }

    [Fact]
    public void Choosing_after_scrolling_picks_by_place_in_the_whole_list()
    {
        var saves = Saves(20);
        _screen.Open(SaveSlotsScreen.Purpose.Load, saves, 1280, 720);
        _screen.ScrollBy(5);

        _screen.Choose(7);

        Assert.Same(saves[7], Assert.Single(_picked));
    }

    [Fact]
    public void The_mouse_wheel_scrolls_down_a_row_a_notch()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Load, Saves(20), 1280, 720);

        _input.MoveMouse(0, 0, wheel: -120);   // one notch down
        Frame();

        Assert.StartsWith("P2 ", _screen.VisibleRows[0]);
    }

    [Fact]
    public void A_shorter_list_after_a_delete_keeps_the_scroll_inside_it()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Load, Saves(20), 1280, 720);
        _screen.ScrollBy(100);

        _screen.Show(Saves(3));

        Assert.Equal(new[] { "P1 ", "P2 ", "P3 " }, _screen.VisibleRows.Select(r => r[..3]));
    }

    [Fact]
    public void Opening_again_starts_at_the_top_with_no_message()
    {
        _screen.Open(SaveSlotsScreen.Purpose.Load, Saves(20), 1280, 720);
        _screen.ScrollBy(3);
        _screen.ShowMessage("Game saved.", good: true);

        _screen.Open(SaveSlotsScreen.Purpose.Load, Saves(20), 1280, 720);

        Assert.StartsWith("P1 ", _screen.VisibleRows[0]);
        Assert.Equal("", _screen.Message);
    }

    // ------------------------------------------------------------------ row text

    [Fact]
    public void A_row_says_who_where_and_which_day()
    {
        Assert.Equal("Loki - CastleInterior, Summer 11, Year 1", SaveSlotsScreen.Headline(Save("Loki", day: 41, level: "CastleInterior")));
    }

    [Fact]
    public void A_row_says_when_it_was_saved_in_the_players_time_zone()
    {
        var save = Save("Loki");   // 09:30 UTC
        var newZealand = TimeZoneInfo.CreateCustomTimeZone("Test +13", TimeSpan.FromHours(13), "Test +13", "Test +13");

        Assert.Equal("Saved 27 Oct 2026, 09:30", SaveSlotsScreen.Detail(save, TimeZoneInfo.Utc));
        Assert.Equal("Saved 27 Oct 2026, 22:30", SaveSlotsScreen.Detail(save, newZealand));
    }

    [Fact]
    public void A_damaged_row_says_so_and_why_cut_short_if_long()
    {
        var damaged = SaveSummary.Unreadable("x.db", "The save file couldn't be read (" + new string('z', 200) + ").");

        Assert.Equal("Damaged save", SaveSlotsScreen.Headline(damaged));
        var detail = SaveSlotsScreen.Detail(damaged, TimeZoneInfo.Utc);
        Assert.StartsWith("The save file couldn't be read", detail);
        Assert.EndsWith("...", detail);
        Assert.True(detail.Length <= 70);
    }
}
