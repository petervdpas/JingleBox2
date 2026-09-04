using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Shortcuts;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.Shortcuts.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// The shortcuts page in SETTINGS: the system's keys to read, and a key of your own on each
/// page along the top.
/// </summary>
/// <remarks>
/// It exists because writing the help found out that it did not. Everything under it was already
/// built and had been for a long time: the map sets, the settings file stores only what differs
/// from the defaults, and <see cref="IShortcutActions"/> says in its own remarks that a settings
/// page builds itself from it. Nobody had built the page, and the help said where it was, which
/// is worth naming as a shape: help text is the one place in an application where a feature can
/// be described into existence, since nothing compiles it and nothing runs it.
///
/// Two lists rather than one, because they are two different things. The system's four are what
/// the application does and every page answers them for itself; the rest are somewhere to go,
/// they ship on no key at all, and they are the whole of what anybody sets here.
///
/// Every row is rebuilt from the map after any change, rather than the one row that was touched
/// being written to. One key does one job, so putting a key on a row takes it off whatever else
/// had it, and the row that lost it has to say so without anybody remembering to tell it.
///
/// **And it follows the map rather than only writing to it**, which is not the same thing and is
/// what was missing. This is built while the window is, before the settings file has been read
/// into the map, so a page that only read once showed every row as not set on a machine where
/// eight keys were saved, while the tab strip beside it drew all eight underlines. From a chair
/// that is a settings page that has lost your work. The map is what knows and the map says when
/// it moves.
/// </remarks>
public sealed partial class ShortcutsViewModel : ObservableObject
{
    /// <summary>What each shortcut is called and whether it is the system's.</summary>
    private readonly IShortcutActions _actions;

    /// <summary>Which key each is on.</summary>
    private readonly IShortcutMap _map;

    /// <summary>What a keystroke means while a row is listening.</summary>
    private readonly IShortcutCatch _catch;

    /// <summary>Every key that is not yours to change.</summary>
    private readonly ISystemKeys _system;

    /// <summary>What to do once something has moved, which is to write the settings down.</summary>
    private readonly System.Action<List<ShortcutBinding>> _keep;

    /// <summary>
    /// Every key the application answers that is not yours to change, to read.
    /// </summary>
    /// <remarks>
    /// Not rows like the pages below, because they are not the same thing: a page row is a key
    /// somebody sets and this is a fact being reported. The four the map holds are in it beside
    /// the four written into doors of their own, since from a chair they are one list, and the
    /// card showed only the map's for about an hour.
    /// </remarks>
    public IReadOnlyList<SystemKey> System => _system.All;

    /// <summary>The pages along the top, each with a key of your own or none.</summary>
    public ObservableCollection<ShortcutRowViewModel> Menu { get; } = new();

    /// <summary>The row waiting for a keystroke, or nothing while none is.</summary>
    private ShortcutRowViewModel? _waiting;

    /// <summary>Builds the page over a map, and says what to do when it moves.</summary>
    /// <param name="keep">Called with what to store whenever something has changed.</param>
    /// <param name="actions">What each shortcut is called, or nothing for the ordinary list.</param>
    /// <param name="map">Which key each is on, or nothing for the application's own.</param>
    /// <param name="caught">What a keystroke means while listening, or nothing for the rule.</param>
    /// <param name="system">The keys nobody may change, or nothing for the ordinary list.</param>
    public ShortcutsViewModel(
        System.Action<List<ShortcutBinding>>? keep = null,
        IShortcutActions? actions = null,
        IShortcutMap? map = null,
        IShortcutCatch? caught = null,
        ISystemKeys? system = null)
    {
        _actions = actions ?? new ShortcutActions();
        _map = map ?? ShortcutKeys.Map;
        _catch = caught ?? new ShortcutCatcher();
        _keep = keep ?? (_ => { });
        _system = system ?? new SystemKeys(_actions, _map);

        foreach (var (action, name, _) in _actions.Everything)
            if (!_actions.Fixed(action))
                Menu.Add(new ShortcutRowViewModel(action, name, locked: false));

        _map.Changed += (_, _) => Show();

        Show();
    }

    /// <summary>Whether anybody has put a key on a page, which is what Clear all is for.</summary>
    public bool AnySet
    {
        get
        {
            foreach (var row in Menu)
                if (row.Keys.Length > 0) return true;

            return false;
        }
    }

    /// <summary>
    /// Starts listening on a row, and stops listening on whichever was.
    /// </summary>
    /// <remarks>
    /// One at a time, since a keystroke has to land somewhere in particular. Asking again on the
    /// row that is already listening stops it, so the button is a way out of the mode as well as
    /// into it.
    /// </remarks>
    /// <param name="row">The row that was clicked.</param>
    [RelayCommand]
    private void Listen(ShortcutRowViewModel? row)
    {
        if (row is null || row.Locked) return;

        bool again = ReferenceEquals(_waiting, row);

        Stop();

        if (again) return;

        _waiting = row;
        row.Listening = true;

        LearningKeys.On = true;
    }

    /// <summary>
    /// A keystroke arrived while a row was listening. Answers whether it was wanted.
    /// </summary>
    /// <remarks>
    /// What the keystroke means is <see cref="IShortcutCatch"/> and has no window in it. What is
    /// here is only what to do about each answer.
    ///
    /// It answers false when nothing was listening, so the key carries on to whatever else might
    /// want it rather than being swallowed by a page that was not in the mode.
    /// </remarks>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifiers">What was held with it.</param>
    public bool Took(Key key, KeyModifiers modifiers)
    {
        if (_waiting is not { } row) return false;

        switch (_catch.Means(key, modifiers))
        {
            case ShortcutCatch.Waiting:
                return true;

            case ShortcutCatch.Refused:
                return true;

            case ShortcutCatch.Cancel:
                Stop();
                return true;

            case ShortcutCatch.Clear:
                _map.Set(row.Action, null);
                break;

            default:
                _map.Set(row.Action, _catch.Gesture(key, modifiers));
                break;
        }

        Stop();
        Show();
        _keep(_map.Given());

        return true;
    }

    /// <summary>Takes the key off one row.</summary>
    /// <param name="row">The row to clear.</param>
    [RelayCommand]
    private void Clear(ShortcutRowViewModel? row)
    {
        if (row is null || row.Locked || row.Keys.Length == 0) return;

        Stop();

        _map.Set(row.Action, null);

        Show();
        _keep(_map.Given());
    }

    /// <summary>Takes the keys off every page at once.</summary>
    [RelayCommand]
    private void ClearAll()
    {
        Stop();

        foreach (var row in Menu) _map.Set(row.Action, null);

        Show();
        _keep(_map.Given());
    }

    /// <summary>
    /// Stops whichever row was listening, and lets every key door hear again.
    /// </summary>
    /// <remarks>
    /// Called from every way out, the page losing the keyboard included: a row left waiting
    /// would take whatever was pressed on the way back to it, and the gate left set would leave
    /// the application deaf to the space bar for the rest of the session.
    /// </remarks>
    public void Stop()
    {
        if (_waiting is { } row) row.Listening = false;

        _waiting = null;

        LearningKeys.On = false;
    }

    /// <summary>Reads every row off the map, which is the only thing that knows.</summary>
    private void Show()
    {
        foreach (var row in Menu) row.Keys = _map.Said(row.Action);

        OnPropertyChanged(nameof(AnySet));
        OnPropertyChanged(nameof(System));
    }
}
