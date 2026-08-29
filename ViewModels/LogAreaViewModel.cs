using CommunityToolkit.Mvvm.ComponentModel;
using System;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.ViewModels;

/// <summary>
/// One part of the app, and whether it is writing anything down.
/// </summary>
/// <remarks>
/// The areas are not alike, which is the whole reason they can be had one at a time. Most of
/// what is written happens once or only when something has gone wrong; a few lines are written
/// per message, per block or once a second, for ever. Switching everything on to look for one
/// thing is how the one thing gets buried, and a queue that overflows drops lines, so the log
/// can be loud enough to lose what you turned it on for.
/// </remarks>
public sealed partial class LogAreaViewModel : ObservableObject
{
    /// <summary>Told when this area was switched on or off, so the settings can be written.</summary>
    private readonly Action<LogAreaViewModel> _changed;

    /// <summary>
    /// False until the constructor has finished, so the starting state is not reported as a change.
    /// </summary>
    /// <remarks>
    /// The initial value is assigned to the backing field, which still raises the generated
    /// property's changed callback. Without this guard, building the SETTINGS page would save the
    /// settings once per area on the way past and each save would say only what was already there.
    /// </remarks>
    private readonly bool _loaded;

    /// <summary>
    /// Builds the row for one area, starting where the settings say it stands.
    /// </summary>
    /// <param name="area">The area this row switches.</param>
    /// <param name="name">Its name as the log stamps it, shown with the first letter raised.</param>
    /// <param name="on">Whether it is currently writing.</param>
    /// <param name="changed">Called when somebody moves the tick box, never on the way in.</param>
    public LogAreaViewModel(LogArea area, string name, bool on, Action<LogAreaViewModel> changed)
    {
        Area = area;
        Name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        _changed = changed;

        writes = on;

        _loaded = true;
    }

    /// <summary>Which part of the app this row is about.</summary>
    public LogArea Area { get; }

    /// <summary>What it is called, as the log's own lines are stamped.</summary>
    public string Name { get; }

    /// <summary>Whether this area is writing anything down, as the tick box shows it.</summary>
    [ObservableProperty] private bool writes;

    /// <summary>Passes a change of the tick box on to whoever stores the settings.</summary>
    partial void OnWritesChanged(bool value)
    {
        if (_loaded) _changed(this);
    }
}
