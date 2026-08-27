using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using JingleBox2.Diagnostics;

namespace JingleBox2.Shortcuts;

/// <summary>One shortcut as it is written down, so a settings file can hold it.</summary>
/// <remarks>
/// The action by name rather than by number: a settings file outlives the order of an enum, and
/// a number in a file is a thing nobody can read or correct by hand.
/// </remarks>
public sealed class ShortcutBinding
{
    public string Action { get; set; } = "";

    /// <summary>As a person writes it: "Ctrl+S", "Ctrl+Shift+Z".</summary>
    public string Keys { get; set; } = "";
}

/// <summary>
/// Which key does what. One object, so there is one place to change it from.
/// </summary>
/// <remarks>
/// Apart from the dispatcher on purpose. What a key means is a setting, and settings are edited,
/// stored and shown; what happens when it is pressed is plumbing. Keeping them apart is what
/// makes a page that edits shortcuts a list bound to this and nothing else, rather than a page
/// that has to know how keys are delivered.
///
/// Only ever holds what somebody changed. A shortcut left alone is not written down, so the
/// defaults can be improved later and will reach anybody who never had an opinion about them.
/// </remarks>
public sealed class ShortcutMap
{
    private readonly Dictionary<ShortcutAction, KeyGesture> _held = new();

    public ShortcutMap() => Reset();

    /// <summary>Puts every one back to what it ships as.</summary>
    public void Reset()
    {
        _held.Clear();

        foreach (var (action, _, keys) in ShortcutActions.Everything)
            if (Read(keys) is { } gesture) _held[action] = gesture;
    }

    /// <summary>What that action is on.</summary>
    public KeyGesture? For(ShortcutAction action) =>
        _held.TryGetValue(action, out var gesture) ? gesture : null;

    /// <summary>What that action is on, as a person would write it.</summary>
    public string Said(ShortcutAction action) => For(action)?.ToString() ?? "";

    /// <summary>
    /// Which action a keystroke asks for, or nothing.
    /// </summary>
    /// <remarks>
    /// Walked rather than looked up, because there are four of them and a dictionary keyed on a
    /// key and its modifiers is more machinery than the thing is worth.
    /// </remarks>
    public ShortcutAction? Match(Key key, KeyModifiers modifiers)
    {
        foreach (var (action, gesture) in _held)
            if (gesture.Key == key && gesture.KeyModifiers == modifiers) return action;

        return null;
    }

    /// <summary>
    /// Puts an action on a key, and takes that key off whatever else had it.
    /// </summary>
    /// <remarks>
    /// One key does one thing. Two actions on one keystroke is a state a settings page should
    /// never be able to leave somebody in, since only one of them could ever happen and which
    /// would be an accident of the order they are stored in.
    /// </remarks>
    public void Set(ShortcutAction action, KeyGesture? gesture)
    {
        if (gesture is null)
        {
            _held.Remove(action);
            return;
        }

        foreach (var other in _held.Where(one =>
                     one.Key != action
                     && one.Value.Key == gesture.Key
                     && one.Value.KeyModifiers == gesture.KeyModifiers)
                 .Select(one => one.Key).ToList())
            _held.Remove(other);

        _held[action] = gesture;
    }

    /// <summary>Reads what was stored, leaving anything it does not recognise alone.</summary>
    public void Take(IEnumerable<ShortcutBinding>? saved)
    {
        Reset();

        if (saved is null) return;

        foreach (var one in saved)
        {
            if (one is null) continue;
            if (!Enum.TryParse(one.Action, ignoreCase: true, out ShortcutAction action)) continue;

            // Nothing at all is a shortcut somebody deliberately took off.
            if (one.Keys.Length == 0) { _held.Remove(action); continue; }

            if (Read(one.Keys) is { } gesture) Set(action, gesture);
        }
    }

    /// <summary>Only what differs from the defaults, so improving one still reaches people.</summary>
    public List<ShortcutBinding> Given()
    {
        var written = new List<ShortcutBinding>();

        foreach (var (action, _, keys) in ShortcutActions.Everything)
        {
            var now = For(action);
            var was = Read(keys);

            bool same = now is null
                ? was is null
                : was is not null && now.Key == was.Key && now.KeyModifiers == was.KeyModifiers;

            if (same) continue;

            written.Add(new ShortcutBinding { Action = action.ToString(), Keys = now?.ToString() ?? "" });
        }

        return written;
    }

    private static KeyGesture? Read(string keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) return null;

        try
        {
            return KeyGesture.Parse(keys);
        }
        catch (Exception)
        {
            // A shortcut nobody can parse is a shortcut that does nothing, which is better than
            // a start that fails over a line in a settings file.
            Log.Write(LogArea.App, () => "shortcuts: '" + keys + "' is not a keystroke this understands");

            return null;
        }
    }
}
