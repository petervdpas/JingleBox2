using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// One plugin on the list in SETTINGS, and whether it is offered.
/// </summary>
/// <remarks>
/// The list is what the last scan found and it runs to hundreds on a machine anybody works on.
/// What it could not say before is that you have one you never reach for: the same plugin ships
/// as a CLAP and a VST3 and both are listed, a bundle holds an instrument and its effect twin,
/// and a picker offering all of them is one nobody can read.
///
/// Off, the plugin stays here with its path and its maker and is left out of every picker. **A
/// song that already names it still loads it**, which is the rule the crash guard keeps for a
/// plugin that took the application down, and for the same reason: what somebody has already
/// written must not quietly lose a voice because of a tick on a settings page.
/// </remarks>
public sealed partial class PluginRowViewModel : ObservableObject
{
    /// <summary>What is switched off, which is where the answer is kept.</summary>
    private readonly IPluginSwitches _switches;

    /// <summary>Told when the tick moves, so the pickers can be told to read again.</summary>
    private readonly Action? _turned;

    /// <param name="plugin">The plugin this row is about.</param>
    /// <param name="switches">What is switched off.</param>
    /// <param name="turned">Told when this row is turned over.</param>
    public PluginRowViewModel(PluginInfo plugin, IPluginSwitches switches, Action? turned = null)
    {
        Plugin = plugin;
        _switches = switches;
        _turned = turned;

        offered = switches.Wanted(plugin);
    }

    /// <summary>The plugin itself, which is what the row draws.</summary>
    public PluginInfo Plugin { get; }

    /// <summary>Whether it is offered anywhere a plugin is chosen.</summary>
    [ObservableProperty] private bool offered = true;

    /// <summary>Writes the tick down and says so.</summary>
    partial void OnOfferedChanged(bool value)
    {
        if (!_switches.Turn(Plugin, value)) return;

        _turned?.Invoke();
    }
}
