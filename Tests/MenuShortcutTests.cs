using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using JingleBox2.Shortcuts;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The shortcuts page: a key of your own on a page along the top, and the system's four left
/// where they are.
/// </summary>
/// <remarks>
/// The page exists because writing the help found out that it did not. Everything under it had
/// been built for a long time and nobody had built the page, and the help said where it was,
/// which is the shape worth naming: help text is the one place in an application where a feature
/// can be described into existence, since nothing compiles it and nothing runs it.
/// </remarks>
public class MenuShortcutTests
{
    /// <summary>What each shortcut is called and whether it is the system's.</summary>
    private readonly IShortcutActions _actions = new ShortcutActions();

    /// <summary>A page over a map of its own, so nothing here touches the application's.</summary>
    /// <param name="map">The map it is over.</param>
    /// <param name="kept">Where what would be stored is put, for a test that wants to see it.</param>
    private static ShortcutsViewModel Page(IShortcutMap map, List<ShortcutBinding>? kept = null) =>
        new(keys => { kept?.Clear(); kept?.AddRange(keys); }, map: map);

    /// <summary>The four the application ships with are the system's.</summary>
    [Theory]
    [InlineData(ShortcutAction.Save)]
    [InlineData(ShortcutAction.Delete)]
    [InlineData(ShortcutAction.Undo)]
    [InlineData(ShortcutAction.Redo)]
    public void The_system_keys_are_fixed(ShortcutAction action)
    {
        Assert.True(_actions.Fixed(action));
    }

    /// <summary>And every page along the top is not.</summary>
    [Theory]
    [InlineData(ShortcutAction.Mixer)]
    [InlineData(ShortcutAction.Record)]
    [InlineData(ShortcutAction.Pads)]
    [InlineData(ShortcutAction.Fire)]
    [InlineData(ShortcutAction.Tracker)]
    [InlineData(ShortcutAction.Designer)]
    [InlineData(ShortcutAction.Settings)]
    [InlineData(ShortcutAction.MidiCc)]
    public void A_page_is_yours(ShortcutAction action)
    {
        Assert.False(_actions.Fixed(action));
    }

    /// <summary>Every page ships on no key at all.</summary>
    /// <remarks>
    /// A shortcut nobody asked for is a keystroke that does something surprising, which is the
    /// rule the pads already keep: one nobody has pointed at should do nothing.
    /// </remarks>
    [Fact]
    public void A_fresh_installation_has_no_page_keys()
    {
        IShortcutMap map = new ShortcutMap();

        foreach (var (action, _, _) in _actions.Everything.Where(one => !_actions.Fixed(one.Action)))
            Assert.Equal("", map.Said(action));
    }

    /// <summary>
    /// The map refuses to move a system key, whoever asks.
    /// </summary>
    /// <remarks>
    /// The guard is in the map rather than only off the settings page, because the page is not
    /// the only way in: a settings file is a file and one edited by hand is asking for something
    /// this does not offer.
    /// </remarks>
    [Fact]
    public void A_system_key_cannot_be_moved()
    {
        IShortcutMap map = new ShortcutMap();

        map.Set(ShortcutAction.Save, new KeyGesture(Key.F4));

        Assert.Equal("Ctrl+S", map.Said(ShortcutAction.Save));
    }

    /// <summary>Nor taken off.</summary>
    [Fact]
    public void A_system_key_cannot_be_taken_off()
    {
        IShortcutMap map = new ShortcutMap();

        map.Set(ShortcutAction.Undo, null);

        Assert.Equal("Ctrl+Z", map.Said(ShortcutAction.Undo));
    }

    /// <summary>And a settings file that says otherwise is read past.</summary>
    [Fact]
    public void A_settings_file_cannot_move_one_either()
    {
        IShortcutMap map = new ShortcutMap();

        map.Take(new[] { new ShortcutBinding { Action = "Save", Keys = "Ctrl+F9" } });

        Assert.Equal("Ctrl+S", map.Said(ShortcutAction.Save));
    }

    /// <summary>A key pressed while a row is listening lands on that row.</summary>
    [Fact]
    public void A_key_pressed_while_listening_lands_on_the_row()
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        var row = page.Menu.First(one => one.Action == ShortcutAction.Tracker);

        page.ListenCommand.Execute(row);

        Assert.True(page.Took(Key.T, KeyModifiers.Control | KeyModifiers.Alt));

        Assert.Equal("Ctrl+Alt+T", map.Said(ShortcutAction.Tracker));
        Assert.Equal("Ctrl+Alt+T", row.Keys);
        Assert.False(row.Listening);
    }

    /// <summary>
    /// A modifier on its own is a hand arriving rather than a shortcut.
    /// </summary>
    /// <remarks>
    /// Ctrl goes down before the letter does, every time, so a row that took the first key it
    /// was given would learn Ctrl and never see what anybody meant.
    /// </remarks>
    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.LWin)]
    public void A_modifier_on_its_own_is_not_a_shortcut(Key key)
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        var row = page.Menu.First();

        page.ListenCommand.Execute(row);
        page.Took(key, KeyModifiers.Control);

        Assert.True(row.Listening, "still waiting for the key that goes with it");
        Assert.Equal("", map.Said(row.Action));
    }

    /// <summary>Escape leaves it as it was.</summary>
    [Fact]
    public void Escape_changes_nothing()
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        var row = page.Menu.First(one => one.Action == ShortcutAction.Fire);

        page.ListenCommand.Execute(row);
        page.Took(Key.F, KeyModifiers.Control | KeyModifiers.Alt);

        page.ListenCommand.Execute(row);
        page.Took(Key.Escape, KeyModifiers.None);

        Assert.Equal("Ctrl+Alt+F", map.Said(ShortcutAction.Fire));
        Assert.False(row.Listening);
    }

    /// <summary>Backspace takes the key off.</summary>
    [Fact]
    public void Backspace_takes_it_off()
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        var row = page.Menu.First(one => one.Action == ShortcutAction.Pads);

        page.ListenCommand.Execute(row);
        page.Took(Key.P, KeyModifiers.Control | KeyModifiers.Alt);

        page.ListenCommand.Execute(row);
        page.Took(Key.Back, KeyModifiers.None);

        Assert.Equal("", map.Said(ShortcutAction.Pads));
        Assert.False(row.HasKey);
    }

    /// <summary>
    /// A page shortcut is Ctrl+Alt and a letter, and every other keystroke is refused.
    /// </summary>
    /// <remarks>
    /// The narrowness is what makes it safe: everything else this application answers is a
    /// letter with Ctrl, or with Ctrl and Shift, or on its own, so a page key cannot land on top
    /// of something that already works and nobody has to know what is taken before choosing one.
    /// </remarks>
    [Theory]
    [InlineData(Key.T, KeyModifiers.Control | KeyModifiers.Alt, ShortcutCatch.Take)]
    [InlineData(Key.A, KeyModifiers.Control | KeyModifiers.Alt, ShortcutCatch.Take)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Alt, ShortcutCatch.Take)]
    [InlineData(Key.T, KeyModifiers.Control, ShortcutCatch.Refused)]
    [InlineData(Key.T, KeyModifiers.Control | KeyModifiers.Shift, ShortcutCatch.Refused)]
    [InlineData(Key.T, KeyModifiers.Alt, ShortcutCatch.Refused)]
    [InlineData(Key.F5, KeyModifiers.Control | KeyModifiers.Alt, ShortcutCatch.Refused)]
    [InlineData(Key.D1, KeyModifiers.Control | KeyModifiers.Alt, ShortcutCatch.Refused)]
    [InlineData(Key.Space, KeyModifiers.None, ShortcutCatch.Refused)]
    [InlineData(Key.Escape, KeyModifiers.Control | KeyModifiers.Alt, ShortcutCatch.Refused)]
    public void Only_control_alt_and_a_letter_is_taken(Key key, KeyModifiers modifiers, ShortcutCatch means)
    {
        Assert.Equal(means, new ShortcutCatcher().Means(key, modifiers));
    }

    /// <summary>A keystroke it refuses leaves the row still waiting for the right one.</summary>
    /// <remarks>
    /// Rather than stopping, which would read as the press having been taken and something
    /// having gone wrong with it.
    /// </remarks>
    [Fact]
    public void A_refused_key_leaves_it_listening()
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        var row = page.Menu.First();

        page.ListenCommand.Execute(row);

        Assert.True(page.Took(Key.S, KeyModifiers.Control));

        Assert.True(row.Listening);
        Assert.Equal("", map.Said(row.Action));

        page.Took(Key.M, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.Equal("Ctrl+Alt+M", map.Said(row.Action));
    }

    /// <summary>
    /// One key does one job, and the row that loses it says so.
    /// </summary>
    /// <remarks>
    /// The map already took the key off whatever held it. What this is about is the page: every
    /// row is read off the map after any change rather than the one that was touched being
    /// written to, or the row that lost its key would go on showing it.
    /// </remarks>
    [Fact]
    public void The_row_that_loses_a_key_stops_showing_it()
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        var first = page.Menu.First(one => one.Action == ShortcutAction.Record);
        var second = page.Menu.First(one => one.Action == ShortcutAction.Mixer);

        page.ListenCommand.Execute(first);
        page.Took(Key.J, KeyModifiers.Control | KeyModifiers.Alt);

        page.ListenCommand.Execute(second);
        page.Took(Key.J, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.Equal("Ctrl+Alt+J", second.Keys);
        Assert.Equal("", first.Keys);
    }

    /// <summary>Only what somebody set is written down, so a default can still be improved.</summary>
    [Fact]
    public void Only_what_was_set_is_stored()
    {
        IShortcutMap map = new ShortcutMap();
        var kept = new List<ShortcutBinding>();
        var page = Page(map, kept);

        page.ListenCommand.Execute(page.Menu.First(one => one.Action == ShortcutAction.Settings));
        page.Took(Key.K, KeyModifiers.Control | KeyModifiers.Alt);

        var one = Assert.Single(kept);

        Assert.Equal("Settings", one.Action);
        Assert.Equal("Ctrl+Alt+K", one.Keys);
    }

    /// <summary>Clearing them all leaves every page on nothing.</summary>
    [Fact]
    public void Clear_all_empties_the_pages()
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        page.ListenCommand.Execute(page.Menu.First());
        page.Took(Key.Q, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.True(page.AnySet);

        page.ClearAllCommand.Execute(null);

        Assert.False(page.AnySet);
        Assert.All(page.Menu, row => Assert.Equal("", row.Keys));
    }

    /// <summary>
    /// The system list carries the keys written into doors as well as the map's own four.
    /// </summary>
    /// <remarks>
    /// It showed only the map's four for about an hour, and the answer to a key being missing is
    /// not to put it in the map, which would be two ways of delivering one keystroke, but to
    /// have one list of what the application answers.
    /// </remarks>
    [Theory]
    [InlineData("Space")]
    [InlineData("Ctrl+R")]
    [InlineData("Ctrl+Shift+M")]
    [InlineData("Ctrl+H")]
    [InlineData("Ctrl+S")]
    [InlineData("Ctrl+D")]
    [InlineData("Ctrl+Z")]
    [InlineData("Ctrl+Shift+Z")]
    public void The_system_list_carries_every_key_that_is_not_yours(string key)
    {
        Assert.Contains(Page(new ShortcutMap()).System, one => one.Keys == key);
    }

    /// <summary>And every one of them says what it does.</summary>
    [Fact]
    public void Every_system_key_says_what_it_does()
    {
        Assert.All(new SystemKeys().All, one =>
        {
            Assert.False(string.IsNullOrWhiteSpace(one.Keys));
            Assert.False(string.IsNullOrWhiteSpace(one.Does));
        });
    }

    /// <summary>Only the pages are rows somebody can set.</summary>
    [Fact]
    public void Only_the_pages_are_settable()
    {
        var page = Page(new ShortcutMap());

        Assert.Equal(8, page.Menu.Count);
        Assert.All(page.Menu, row => Assert.True(row.Settable));
    }

    /// <summary>
    /// While a row listens every other key door stands down, and afterwards they hear again.
    /// </summary>
    /// <remarks>
    /// This is the whole reason the gate exists. Every key this application answers is heard at
    /// the window before whatever has the keyboard sees it, so without it the space bar would
    /// reach the transport and a row waiting for it would never see one. A gate left set is
    /// worse: the application would be deaf to its own keys for the rest of the session.
    /// </remarks>
    [Fact]
    public void The_other_keys_stand_down_while_one_is_learned()
    {
        IShortcutMap map = new ShortcutMap();
        var page = Page(map);

        Assert.False(LearningKeys.On);

        page.ListenCommand.Execute(page.Menu.First());

        Assert.True(LearningKeys.On);

        page.Took(Key.Y, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.False(LearningKeys.On);
        Assert.Equal("Ctrl+Alt+Y", page.Menu.First().Keys);
    }

    /// <summary>And they hear again when the page is simply left.</summary>
    [Fact]
    public void And_when_the_page_is_left_without_pressing_anything()
    {
        var page = Page(new ShortcutMap());

        page.ListenCommand.Execute(page.Menu.First());
        page.Stop();

        Assert.False(LearningKeys.On);
    }

    /// <summary>A keystroke with nothing listening is left alone.</summary>
    /// <remarks>
    /// It answers false so the key carries on to whatever else might want it, rather than being
    /// swallowed by a page that was not in the mode.
    /// </remarks>
    [Fact]
    public void A_key_with_nothing_listening_is_not_taken()
    {
        Assert.False(Page(new ShortcutMap()).Took(Key.Space, KeyModifiers.None));
    }
}
