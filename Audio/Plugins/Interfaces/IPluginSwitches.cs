using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// What this installation has switched off: folders that are not looked in, and plugins that
/// are not offered.
/// </summary>
/// <remarks>
/// **Off is not gone.** A folder switched off is still listed, still says which standard it is
/// for, and is one tick away from being looked in again; a plugin switched off is still on the
/// list in SETTINGS with its path and its maker, and is simply not offered anywhere a plugin is
/// chosen. Nothing is deleted and nothing is forgotten, which is what makes it safe to switch
/// something off to find out whether it was the thing causing trouble.
///
/// The two are different acts for different reasons and that is why there are two. A folder is
/// switched off to stop a scan walking it: scanning is the one place a plugin runs code before
/// anybody has chosen to use it, so a folder full of something that hangs or crashes costs every
/// scan from now on, and a folder of CLAPs walked for VST3 bundles is time spent finding nothing.
/// A plugin is switched off because it is installed and you do not want it in the picker: two
/// hundred plugins is a list nobody can scan, and the same plugin shipping as a CLAP and a VST3
/// is two entries where one will do.
///
/// **A song that already names a switched-off plugin still loads it.** Switching one off says
/// what may be offered, not what may be played, and a song opening silently without the plugin it
/// was written on would be the worse answer by a long way. It is the rule the crash guard already
/// keeps for a plugin that took the application down: kept out of the pickers, named where it is
/// not there, and never quietly dropped from somebody's work.
/// </remarks>
public interface IPluginSwitches
{
    /// <summary>Whether that folder is looked in for that standard.</summary>
    /// <param name="place">The folder and the format it would be walked for.</param>
    bool Wanted(PluginPlace? place);

    /// <summary>Whether that plugin may be offered.</summary>
    /// <remarks>
    /// By the id the scanner gave it rather than by its path, which is the rule the rest of this
    /// half keeps: a bundle lives somewhere else on another machine, and two classes in one
    /// bundle share a path and are two plugins.
    /// </remarks>
    /// <param name="plugin">The plugin to ask about.</param>
    bool Wanted(PluginInfo? plugin);

    /// <summary>Turns a folder on or off, and says whether anything really moved.</summary>
    /// <param name="place">The folder and the format.</param>
    /// <param name="on">Whether it should be looked in.</param>
    bool Turn(PluginPlace? place, bool on);

    /// <summary>Turns a plugin on or off, and says whether anything really moved.</summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="on">Whether it should be offered.</param>
    bool Turn(PluginInfo? plugin, bool on);

    /// <summary>
    /// The folders switched off, as they are written down.
    /// </summary>
    /// <remarks>
    /// Handed to a scan so the walk can skip them, which is the whole reason a folder is switched
    /// off, and the scan happens in a process of its own, so this has to be something that can be
    /// written on a command line rather than a question that can be asked.
    /// </remarks>
    IReadOnlyList<PluginPlace> Off { get; }
}
