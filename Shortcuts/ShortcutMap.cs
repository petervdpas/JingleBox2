using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Shortcuts;

/// <summary>One shortcut as it is written down, so a settings file can hold it.</summary>
/// <remarks>
/// The action by name rather than by number: a settings file outlives the order of an enum, and
/// a number in a file is a thing nobody can read or correct by hand.
/// </remarks>
public sealed class ShortcutBinding
{
    /// <summary>Which action, by the name of the enum member rather than its number.</summary>
    public string Action { get; set; } = "";

    /// <summary>As a person writes it: "Ctrl+S", "Ctrl+Shift+Z".</summary>
    public string Keys { get; set; } = "";
}

/// <inheritdoc/>
public sealed class ShortcutMap : IShortcutMap
{
    /// <summary>What each shortcut is called and what it ships on.</summary>
    private readonly IShortcutActions _actions = new ShortcutActions();

    /// <summary>What each action is on, and nothing at all for one somebody took off.</summary>
    /// <remarks>
    /// A dictionary of four, walked rather than indexed by keystroke: see <see cref="Match"/>.
    /// </remarks>
    private readonly Dictionary<ShortcutAction, KeyGesture> _held = new();

    /// <summary>A map on the defaults, which is what it is until something is stored into it.</summary>
    public ShortcutMap() => Reset();

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <summary>Says something moved, for whoever is drawing a key somewhere else.</summary>
    private void Moved() => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    public void Reset()
    {
        _held.Clear();

        foreach (var (action, _, keys) in _actions.Everything)
            if (Read(keys) is { } gesture) _held[action] = gesture;

        Moved();
    }

    /// <inheritdoc/>
    public KeyGesture? For(ShortcutAction action) =>
        _held.TryGetValue(action, out var gesture) ? gesture : null;

    /// <inheritdoc/>
    public string Said(ShortcutAction action) => For(action)?.ToString() ?? "";

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    /// <remarks>
    /// A system shortcut is refused here rather than merely left off the settings page, because
    /// the page is not the only way in: a settings file is a file, and one that has been edited
    /// by hand to move Save is asking for something this does not offer. Refused quietly, since
    /// the same call reads that file at startup and a line in a settings file is not worth a
    /// start that fails.
    /// </remarks>
    public void Set(ShortcutAction action, KeyGesture? gesture)
    {
        if (_actions.Fixed(action)) return;

        if (gesture is null)
        {
            if (_held.Remove(action)) Moved();

            return;
        }

        if (For(action) is { } now && now.Key == gesture.Key && now.KeyModifiers == gesture.KeyModifiers)
            return;

        foreach (var other in _held.Where(one =>
                     one.Key != action
                     && one.Value.Key == gesture.Key
                     && one.Value.KeyModifiers == gesture.KeyModifiers)
                 .Select(one => one.Key).ToList())
            _held.Remove(other);

        _held[action] = gesture;

        Moved();
    }

    /// <inheritdoc/>
    public void Take(IEnumerable<ShortcutBinding>? saved)
    {
        Reset();

        if (saved is null) return;

        foreach (var one in saved)
        {
            if (one is null) continue;
            if (!Enum.TryParse(one.Action, ignoreCase: true, out ShortcutAction action)) continue;

            if (_actions.Fixed(action)) continue;

            if (one.Keys.Length == 0) { _held.Remove(action); continue; }

            if (Read(one.Keys) is { } gesture) Set(action, gesture);
        }

        Moved();
    }

    /// <inheritdoc/>
    public List<ShortcutBinding> Given()
    {
        var written = new List<ShortcutBinding>();

        foreach (var (action, _, keys) in _actions.Everything)
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

    /// <summary>
    /// A keystroke as a person writes it, or nothing when it is not one this understands.
    /// </summary>
    /// <remarks>
    /// A shortcut nobody can parse is a shortcut that does nothing, which is better than a
    /// start that fails over one line in a settings file.
    /// </remarks>
    private static KeyGesture? Read(string keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) return null;

        try
        {
            return KeyGesture.Parse(keys);
        }
        catch (Exception)
        {
            Log.Write(LogArea.App, () => "shortcuts: '" + keys + "' is not a keystroke this understands");

            return null;
        }
    }
}
