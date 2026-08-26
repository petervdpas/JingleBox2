using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Diagnostics;
using System;

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
    private readonly Action<LogAreaViewModel> _changed;
    private readonly bool _loaded;

    public LogAreaViewModel(LogArea area, string name, bool on, Action<LogAreaViewModel> changed)
    {
        Area = area;
        Name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        _changed = changed;

        writes = on;

        _loaded = true;
    }

    public LogArea Area { get; }

    /// <summary>What it is called, as the log's own lines are stamped.</summary>
    public string Name { get; }

    [ObservableProperty] private bool writes;

    partial void OnWritesChanged(bool value)
    {
        if (_loaded) _changed(this);
    }
}
