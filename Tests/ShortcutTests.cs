using System.Collections.Generic;
using Avalonia.Input;
using JingleBox2.Shortcuts;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which key asks for what. The half of the shortcut module that a settings page will edit.
/// </summary>
public class ShortcutTests
{
    [Theory]
    [InlineData(Key.S, KeyModifiers.Control, ShortcutAction.Save)]
    [InlineData(Key.D, KeyModifiers.Control, ShortcutAction.Delete)]
    [InlineData(Key.Z, KeyModifiers.Control, ShortcutAction.Undo)]
    public void What_ships(Key key, KeyModifiers modifiers, ShortcutAction wanted) =>
        Assert.Equal(wanted, new ShortcutMap().Match(key, modifiers));

    [Fact]
    public void Redo_is_undo_with_shift()
    {
        Assert.Equal(ShortcutAction.Redo,
                     new ShortcutMap().Match(Key.Z, KeyModifiers.Control | KeyModifiers.Shift));
    }

    [Theory]
    [InlineData(Key.S, KeyModifiers.None)]
    [InlineData(Key.S, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.Q, KeyModifiers.Control)]
    public void And_nothing_else_asks_for_anything(Key key, KeyModifiers modifiers) =>
        Assert.Null(new ShortcutMap().Match(key, modifiers));

    [Fact]
    public void A_shortcut_nobody_changed_is_not_written_down()
    {
        // So a default that turns out to be a poor choice can be improved later and will reach
        // anybody who never had an opinion about it.
        Assert.Empty(new ShortcutMap().Given());
    }

    [Fact]
    public void Moving_an_action_takes_the_old_key_with_it()
    {
        var map = new ShortcutMap();

        map.Set(ShortcutAction.Save, KeyGesture.Parse("Ctrl+W"));

        Assert.Equal(ShortcutAction.Save, map.Match(Key.W, KeyModifiers.Control));
        Assert.Null(map.Match(Key.S, KeyModifiers.Control));

        var written = Assert.Single(map.Given());
        Assert.Equal("Save", written.Action);
        Assert.Equal("Ctrl+W", written.Keys);
    }

    [Fact]
    public void One_key_does_one_job()
    {
        var map = new ShortcutMap();

        map.Set(ShortcutAction.Save, KeyGesture.Parse("Ctrl+W"));
        map.Set(ShortcutAction.Delete, KeyGesture.Parse("Ctrl+W"));

        // Two actions on one keystroke is a state a settings page must never leave anybody in:
        // only one could ever happen and which would be an accident of storage order.
        Assert.Equal(ShortcutAction.Delete, map.Match(Key.W, KeyModifiers.Control));
        Assert.Equal("", map.Said(ShortcutAction.Save));
    }

    [Fact]
    public void What_was_stored_comes_back()
    {
        var map = new ShortcutMap();
        map.Set(ShortcutAction.Save, KeyGesture.Parse("Ctrl+W"));

        var second = new ShortcutMap();
        second.Take(map.Given());

        Assert.Equal(ShortcutAction.Save, second.Match(Key.W, KeyModifiers.Control));

        // And everything nobody touched is still where it shipped.
        Assert.Equal(ShortcutAction.Undo, second.Match(Key.Z, KeyModifiers.Control));
    }

    [Fact]
    public void A_shortcut_taken_off_stays_off()
    {
        var map = new ShortcutMap();
        map.Set(ShortcutAction.Save, null);

        var second = new ShortcutMap();
        second.Take(map.Given());

        Assert.Null(second.Match(Key.S, KeyModifiers.Control));
    }

    [Fact]
    public void Rubbish_in_the_settings_costs_that_line_and_nothing_else()
    {
        var map = new ShortcutMap();

        map.Take(new List<ShortcutBinding>
        {
            new() { Action = "Save", Keys = "Ctrl+Shift+Q" },
            new() { Action = "Nonsense", Keys = "Ctrl+P" },
            new() { Action = "Delete", Keys = "not a keystroke" }
        });

        Assert.Equal(ShortcutAction.Save, map.Match(Key.Q, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Null(map.Match(Key.P, KeyModifiers.Control));

        // An unreadable key leaves that action where it shipped rather than switching it off.
        Assert.Equal(ShortcutAction.Delete, map.Match(Key.D, KeyModifiers.Control));
    }

    [Fact]
    public void Reset_puts_everything_back()
    {
        var map = new ShortcutMap();
        map.Set(ShortcutAction.Save, KeyGesture.Parse("Ctrl+W"));

        map.Reset();

        Assert.Equal(ShortcutAction.Save, map.Match(Key.S, KeyModifiers.Control));
        Assert.Empty(map.Given());
    }

    [Fact]
    public void Every_action_has_a_name_and_a_default()
    {
        // A settings page builds itself from this, the way the log's page does from its areas.
        foreach (var (action, name, keys) in ShortcutActions.Everything)
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.NotNull(KeyGesture.Parse(keys));
            Assert.Equal(name, ShortcutActions.Named(action));
        }
    }
}
