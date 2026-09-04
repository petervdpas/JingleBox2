using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Shortcuts.Enums;

namespace JingleBox2.ViewModels;

/// <summary>
/// One shortcut on the settings page: what it is, what it is on, and whether it may be moved.
/// </summary>
/// <remarks>
/// A row rather than a tuple because two of the three things on it move while somebody is
/// looking at the page: the key changes when they set one, and it also changes when they set
/// the same key on another row, since one key does one job and whatever held it loses it.
/// </remarks>
public sealed partial class ShortcutRowViewModel : ObservableObject
{
    /// <summary>Which shortcut this row is.</summary>
    public ShortcutAction Action { get; }

    /// <summary>What it is called, in the words the tab strip uses for a page.</summary>
    public string Name { get; }

    /// <summary>Whether it is the system's, so it is shown and not moved.</summary>
    public bool Locked { get; }

    /// <summary>Whether a key may be put on it, which is the opposite and is what a page binds.</summary>
    public bool Settable => !Locked;

    /// <summary>What it is on, as a person writes it, or nothing when it is on no key.</summary>
    [ObservableProperty]
    private string _keys = "";

    /// <summary>Whether this row is waiting for the keystroke that will become its shortcut.</summary>
    [ObservableProperty]
    private bool _listening;

    /// <summary>Builds one.</summary>
    /// <param name="action">Which shortcut.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="locked">Whether it is the system's.</param>
    public ShortcutRowViewModel(ShortcutAction action, string name, bool locked)
    {
        Action = action;
        Name = name;
        Locked = locked;
    }

    /// <summary>
    /// What the button on the row says.
    /// </summary>
    /// <remarks>
    /// A row on no key says so in words rather than being blank, since a blank button is one
    /// nobody can tell from a button that failed to draw.
    ///
    /// While it is listening it says what it will take rather than "press a key", because a page
    /// shortcut is Ctrl+Alt and a letter and nothing else, and a prompt that asked for any key
    /// would be inviting the one press this refuses.
    /// </remarks>
    public string Said => Listening ? "Ctrl+Alt and a letter" : Keys.Length > 0 ? Keys : "not set";

    /// <summary>Whether there is a key on it, which is whether there is anything to clear.</summary>
    public bool HasKey => Keys.Length > 0;

    /// <summary>The two things the button's wording is worked out from.</summary>
    /// <param name="value">What it changed to.</param>
    partial void OnKeysChanged(string value)
    {
        OnPropertyChanged(nameof(Said));
        OnPropertyChanged(nameof(HasKey));
    }

    /// <summary>And the other of them.</summary>
    /// <param name="value">What it changed to.</param>
    partial void OnListeningChanged(bool value) => OnPropertyChanged(nameof(Said));
}
