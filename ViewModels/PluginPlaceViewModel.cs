using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// One row of the list of folders a scan walks: where it is, which standard it is walked for,
/// and whether it is walked at all.
/// </summary>
/// <remarks>
/// The folders are almost all the two standards' own, worked out from the operating system, so
/// the list is mostly not somebody's choice and could not be edited before: the only thing that
/// could be done to it was adding one of your own, and the only thing that could be done to that
/// was taking it away again. Switching one off is the missing half, and it is the useful one,
/// since a scan opens every plugin it finds and a folder of something that hangs costs every scan
/// from now on.
///
/// Said aloud rather than inferred: a folder of yours is looked in by both scanners, so it is two
/// rows here, one per standard, and each can be turned off on its own.
/// </remarks>
public sealed partial class PluginPlaceViewModel : ObservableObject
{
    /// <summary>What this row is about.</summary>
    private readonly PluginPlace _place;

    /// <summary>What is switched off, which is where the answer is kept.</summary>
    private readonly IPluginSwitches _switches;

    /// <summary>Told when the tick moves, so the page can scan again.</summary>
    private readonly Action? _turned;

    /// <param name="place">The folder and the standard it is walked for.</param>
    /// <param name="switches">What is switched off.</param>
    /// <param name="turned">
    /// Told when this row is turned over, so whoever holds the list can act on it. Left out,
    /// nothing happens beyond the answer being written down.
    /// </param>
    public PluginPlaceViewModel(PluginPlace place, IPluginSwitches switches, Action? turned = null)
    {
        _place = place;
        _switches = switches;
        _turned = turned;

        lookedIn = switches.Wanted(place);
    }

    /// <summary>The folder itself.</summary>
    public string Path => _place.Path;

    /// <summary>Which standard it is walked for, in the word a person reads.</summary>
    public string Format => _place.Format == PluginFormat.Clap ? "CLAP" : "VST3";

    /// <summary>Whether a scan walks it.</summary>
    /// <remarks>
    /// Off, the folder is left exactly where it is and is listed exactly as it was. Nothing about
    /// a plugin already found in it is forgotten either: what changes is the next scan.
    /// </remarks>
    [ObservableProperty] private bool lookedIn = true;

    /// <summary>Writes the tick down and says so.</summary>
    partial void OnLookedInChanged(bool value)
    {
        if (!_switches.Turn(_place, value)) return;

        _turned?.Invoke();
    }
}
