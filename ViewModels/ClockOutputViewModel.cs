using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// One MIDI output, and whether this machine's clock is sent to it.
/// </summary>
/// <remarks>
/// A row rather than a bare string, because the list is ticked: what is stored is the names that
/// are on, and what is drawn is every output the machine has with the stored ones marked.
///
/// **Its own type rather than the port rows above it**, which look similar and are not the same
/// thing. Those are inputs, and the four jobs they can be given are things a port does to us; an
/// output has exactly one job and it is the other direction. Folding the two into one list with a
/// direction on each row would put a tick box for Pads beside an output that cannot send us
/// anything.
/// </remarks>
public sealed partial class ClockOutputViewModel : ObservableObject
{
    /// <summary>Told when the tick moves, so the settings can be written.</summary>
    private readonly Action<ClockOutputViewModel>? _changed;

    /// <summary>Set once the row is built, so filling it in does not count as a change.</summary>
    private readonly bool _loaded;

    /// <summary>Takes the output's name, whether it is on, and who to tell.</summary>
    /// <param name="name">The output, as the machine names it.</param>
    /// <param name="driven">Whether the clock is being sent to it.</param>
    /// <param name="changed">Told when the tick moves.</param>
    public ClockOutputViewModel(string name, bool driven, Action<ClockOutputViewModel>? changed = null)
    {
        Name = name;
        _changed = changed;
        isDriven = driven;
        _loaded = true;
    }

    /// <summary>The output, as the machine names it.</summary>
    public string Name { get; }

    /// <summary>Whether the clock is sent to it.</summary>
    /// <remarks>
    /// Off unless somebody says so. Clock arriving at a device nobody pointed it at is a device
    /// that starts running when its owner did not ask.
    /// </remarks>
    [ObservableProperty] private bool isDriven;

    /// <summary>Says the tick moved, once the row is built.</summary>
    partial void OnIsDrivenChanged(bool value)
    {
        if (_loaded) _changed?.Invoke(this);
    }
}
