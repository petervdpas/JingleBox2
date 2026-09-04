using System.Collections.Generic;
using Avalonia.Input;
using JingleBox2.Shortcuts;
using Xunit;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// Which key asks for what. The half of the shortcut module that a settings page will edit.
/// </summary>
/// <remarks>
/// Shortcuts are three pieces kept apart: the closed list of what a key can ask for, this map,
/// and the delivery, which knows about neither. Only the map is stored and only the map is
/// edited, so this is the only piece a settings page can get wrong.
///
/// The tests run as a settings page would meet it: what ships, then moving an action onto
/// another key, then what is written down and read back, then putting it all back.
/// </remarks>
public class ShortcutTests
{
    /// <summary>What each shortcut is called and what it ships on.</summary>
    private readonly IShortcutActions _actions = new ShortcutActions();

    /// <summary>The three shortcuts a fresh installation answers.</summary>
    [Theory]
    [InlineData(Key.S, KeyModifiers.Control, ShortcutAction.Save)]
    [InlineData(Key.D, KeyModifiers.Control, ShortcutAction.Delete)]
    [InlineData(Key.Z, KeyModifiers.Control, ShortcutAction.Undo)]
    public void What_ships(Key key, KeyModifiers modifiers, ShortcutAction wanted) =>
        Assert.Equal(wanted, new ShortcutMap().Match(key, modifiers));

    /// <summary>Redo is undo with shift, which is what everything else on the desktop does.</summary>
    [Fact]
    public void Redo_is_undo_with_shift()
    {
        Assert.Equal(ShortcutAction.Redo,
                     new ShortcutMap().Match(Key.Z, KeyModifiers.Control | KeyModifiers.Shift));
    }

    /// <summary>A key nobody claimed is left alone, modifiers included.</summary>
    /// <remarks>
    /// The dispatcher passes an unclaimed keystroke on untouched, so a near miss here has to
    /// mean nothing rather than mean the shortcut it nearly is.
    /// </remarks>
    [Theory]
    [InlineData(Key.S, KeyModifiers.None)]
    [InlineData(Key.S, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.Q, KeyModifiers.Control)]
    public void And_nothing_else_asks_for_anything(Key key, KeyModifiers modifiers) =>
        Assert.Null(new ShortcutMap().Match(key, modifiers));

    /// <summary>Only what somebody changed is stored.</summary>
    /// <remarks>
    /// So a default that turns out to be a poor choice can be improved later and will reach
    /// anybody who never had an opinion about it.
    /// </remarks>
    [Fact]
    public void A_shortcut_nobody_changed_is_not_written_down()
    {
        Assert.Empty(new ShortcutMap().Given());
    }

    /// <summary>An action put on another key is not left answering to the old one as well.</summary>
    /// <remarks>
    /// Asked of a page rather than of Save, since the system's four are not moveable at all:
    /// see <c>Tests/MenuShortcutTests.cs</c>. Everything about the map is the same either way,
    /// which is the point of asking it here.
    /// </remarks>
    [Fact]
    public void Moving_an_action_takes_the_old_key_with_it()
    {
        var map = new ShortcutMap();

        map.Set(ShortcutAction.Tracker, KeyGesture.Parse("Ctrl+W"));
        map.Set(ShortcutAction.Tracker, KeyGesture.Parse("Ctrl+E"));

        Assert.Equal(ShortcutAction.Tracker, map.Match(Key.E, KeyModifiers.Control));
        Assert.Null(map.Match(Key.W, KeyModifiers.Control));

        var written = Assert.Single(map.Given());
        Assert.Equal("Tracker", written.Action);
        Assert.Equal("Ctrl+E", written.Keys);
    }

    /// <summary>Putting an action on a key takes it off whatever had that key.</summary>
    /// <remarks>
    /// Two actions on one keystroke is a state a settings page must never leave anybody in:
    /// only one could ever happen and which would be an accident of storage order.
    /// </remarks>
    [Fact]
    public void One_key_does_one_job()
    {
        var map = new ShortcutMap();

        map.Set(ShortcutAction.Tracker, KeyGesture.Parse("Ctrl+W"));
        map.Set(ShortcutAction.Record, KeyGesture.Parse("Ctrl+W"));

        Assert.Equal(ShortcutAction.Record, map.Match(Key.W, KeyModifiers.Control));
        Assert.Equal("", map.Said(ShortcutAction.Tracker));
    }

    /// <summary>A changed key survives the round trip, and the rest is still as it shipped.</summary>
    [Fact]
    public void What_was_stored_comes_back()
    {
        var map = new ShortcutMap();
        map.Set(ShortcutAction.Tracker, KeyGesture.Parse("Ctrl+W"));

        var second = new ShortcutMap();
        second.Take(map.Given());

        Assert.Equal(ShortcutAction.Tracker, second.Match(Key.W, KeyModifiers.Control));
        Assert.Equal(ShortcutAction.Undo, second.Match(Key.Z, KeyModifiers.Control));
    }

    /// <summary>An action switched off comes back off, not back at its default.</summary>
    /// <remarks>
    /// Taking a shortcut away is a decision, and storing nothing for it would be storing the
    /// same thing as never having touched it. Shown on a page rather than on Save, which cannot
    /// be taken off at all, by setting one and then clearing it: what is stored has to say the
    /// difference between a page nobody touched and one somebody emptied.
    /// </remarks>
    [Fact]
    public void A_shortcut_taken_off_stays_off()
    {
        var map = new ShortcutMap();
        map.Set(ShortcutAction.Fire, KeyGesture.Parse("Ctrl+W"));

        var second = new ShortcutMap();
        second.Take(map.Given());

        Assert.Equal(ShortcutAction.Fire, second.Match(Key.W, KeyModifiers.Control));

        second.Set(ShortcutAction.Fire, null);

        var third = new ShortcutMap();
        third.Take(second.Given());

        Assert.Null(third.Match(Key.W, KeyModifiers.Control));
        Assert.Equal("", third.Said(ShortcutAction.Fire));
    }

    /// <summary>A settings file with nonsense in it loses that line and keeps the rest.</summary>
    /// <remarks>
    /// An action nobody has heard of is dropped, and an unreadable key leaves that action where
    /// it shipped rather than switching it off.
    /// </remarks>
    [Fact]
    public void Rubbish_in_the_settings_costs_that_line_and_nothing_else()
    {
        var map = new ShortcutMap();

        map.Take(new List<ShortcutBinding>
        {
            new() { Action = "Pads", Keys = "Ctrl+Shift+Q" },
            new() { Action = "Nonsense", Keys = "Ctrl+P" },
            new() { Action = "Mixer", Keys = "not a keystroke" }
        });

        Assert.Equal(ShortcutAction.Pads, map.Match(Key.Q, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Null(map.Match(Key.P, KeyModifiers.Control));
        Assert.Equal("", map.Said(ShortcutAction.Mixer));
        Assert.Equal(ShortcutAction.Delete, map.Match(Key.D, KeyModifiers.Control));
    }

    /// <summary>Reset puts every key back and leaves nothing stored.</summary>
    [Fact]
    public void Reset_puts_everything_back()
    {
        var map = new ShortcutMap();
        map.Set(ShortcutAction.Tracker, KeyGesture.Parse("Ctrl+W"));

        map.Reset();

        Assert.Equal(ShortcutAction.Save, map.Match(Key.S, KeyModifiers.Control));
        Assert.Null(map.Match(Key.W, KeyModifiers.Control));
        Assert.Empty(map.Given());
    }

    /// <summary>Every action has a name to show and a keystroke that parses.</summary>
    /// <remarks>
    /// A settings page builds itself from this, the way the log's page does from its areas, so
    /// an action added without either would show as a blank row nobody could bind.
    /// </remarks>
    [Fact]
    public void Every_action_has_a_name_and_a_default()
    {
        foreach (var (action, name, keys) in _actions.Everything)
        {
            Assert.False(string.IsNullOrWhiteSpace(name));

            if (keys.Length == 0)
            {
                Assert.False(_actions.Fixed(action), name + " ships on no key, so it must be settable");
                continue;
            }

            Assert.NotNull(KeyGesture.Parse(keys));
            Assert.Equal(name, _actions.Named(action));
        }
    }
}
