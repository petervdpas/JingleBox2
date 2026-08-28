using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// The one place that knows both plugin standards, so nothing above here has to.
/// </summary>
/// <remarks>
/// CLAP and VST3 are different enough underneath to be worth keeping apart, and similar enough
/// above to be worth hiding. A picker, a chain and a saved song all deal in
/// <see cref="PluginInfo"/> and <see cref="IPluginEffect"/>; only this chooses which loader to
/// call.
///
/// Nothing here is quick. Loading a plugin starts a process and waits for it to say hello, and
/// scanning a folder starts one per plugin; both are seconds rather than milliseconds and
/// neither belongs on a thread anybody is drawing with.
/// </remarks>
public interface IPluginHost
{
    /// <summary>
    /// True when plugins are given a process of their own, which is Linux and macOS. Windows
    /// loads them into this one.
    /// </summary>
    /// <remarks>
    /// A plugin in its own process cannot take the application down, so everything the crash
    /// guard was written for stops applying: nothing needs blocking, because nothing that goes
    /// wrong in a plugin is fatal any more. See <see cref="PluginCrashGuard"/>, which stands
    /// down while this is true.
    ///
    /// Windows is the exception because of how a plugin's window is handed over there: the
    /// embedding used here only works within one process, so a VST3 plugin has to be loaded into
    /// this one for its own interface to answer a mouse at all.
    ///
    /// <c>JB_PLUGINS_INPROCESS=1</c> turns it off everywhere, which is how a plugin is debugged
    /// with the application's own debugger attached.
    /// </remarks>
    bool Isolated { get; }

    /// <summary>Opens a plugin as an effect, whichever standard it speaks.</summary>
    /// <remarks>
    /// The block size is a promise: a plugin allocates against it and may not be handed a bigger
    /// one afterwards.
    /// </remarks>
    IPluginEffect? Load(PluginInfo plugin, int sampleRate, int maxFrames);

    /// <summary>
    /// Opens a plugin as an instrument: something that takes notes and gives audio back.
    /// </summary>
    /// <remarks>
    /// Only VST3 for now. CLAP carries notes just as well and the plumbing here is the same
    /// shape, but nothing has been written for it yet, so a CLAP instrument is refused rather
    /// than loaded and then found to be silent. <see cref="CanPlay"/> is what a picker should
    /// ask so nobody is offered one.
    /// </remarks>
    IPluginInstrument? LoadInstrument(PluginInfo plugin, int sampleRate, int maxFrames);

    /// <summary>True when this host can play notes into a plugin of this kind.</summary>
    bool CanPlay(PluginInfo plugin);

    /// <summary>Every directory either standard keeps plugins in, plus the user's own.</summary>
    IReadOnlyList<string> SearchPaths(IEnumerable<string>? extra = null);

    /// <summary>
    /// True when a plugin is still where it was found. A CLAP plugin is a file and a VST3
    /// plugin is usually a folder, so both have to be asked about.
    /// </summary>
    bool Exists(PluginInfo plugin);

    /// <summary>
    /// Looks in every standard place, opens what it finds, and asks each bundle what is in it.
    /// A bundle that will not open is skipped rather than stopping the scan: one bad plugin is
    /// not a machine with no plugins.
    /// </summary>
    /// <remarks>
    /// In a process of its own on every platform, Windows included, where plugins are otherwise
    /// loaded into this one. Scanning is the one thing that needs nothing from the window
    /// embedding that keeps Windows in-process: the child opens each bundle, asks what is in it,
    /// writes a list and goes away. And it is the worst place to be running somebody else's code
    /// unprotected, because a plugin that dies while being asked what it is would take the
    /// application down every time it started, before anybody had chosen to use it.
    /// </remarks>
    List<PluginInfo> Scan(IReadOnlyList<string> folders);
}
