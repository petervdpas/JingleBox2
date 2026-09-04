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
    /// True when plugins are given a process of their own, which is every platform.
    /// </summary>
    /// <remarks>
    /// A plugin in its own process cannot take the application down, so everything the crash
    /// guard was written for stops applying: nothing needs blocking, because nothing that goes
    /// wrong in a plugin is fatal any more. See <see cref="PluginCrashGuard"/>, which stands
    /// down while this is true.
    ///
    /// **Windows was the exception and should never have been.** The reason written here was
    /// that the embedding used only works within one process, and that is not true of Windows: a
    /// window whose parent belongs to another program draws, resizes and answers a mouse exactly
    /// as one in the same process does, which is how every host that bridges plugins does it.
    /// The only thing cross-process costs there is the keyboard, because Windows keeps focus per
    /// thread, and that is two calls: see <see cref="NativeWindow.ShareInput"/>.
    ///
    /// What the exception really cost was a plugin split across two threads. A plugin loaded
    /// into this process is loaded off the drawing thread, rightly, since loading one is seconds;
    /// but its window is then created and handed over on the drawing thread. VST3 asks for a
    /// view and the controller behind it to live on one thread, and a plugin whose toolkit binds
    /// its own message thread where it was built, which is most of them, blocks for ever when
    /// <c>attached</c> arrives on a different one. That is what a grey plugin window on Windows
    /// was: not a window that would not draw, but a call that never came back.
    ///
    /// A plugin in its own process cannot have that fault, because there is one thread and it
    /// does everything: the process loads the plugin, makes its view and hands it the window in
    /// turn, on the thread that then goes on pumping for it.
    ///
    /// <c>JB_PLUGINS_INPROCESS=1</c> turns it off everywhere, which is how a plugin is debugged
    /// with the application's own debugger attached. It is the one way back to the old behaviour
    /// and it brings the old fault with it.
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
    /// In a process of its own, like everything else here now: the child opens each bundle, asks
    /// what is in it, writes a list and goes away. It used to be the one thing that was isolated
    /// on Windows as well, back when a plugin being used was not, which is worth remembering only
    /// because it was the standing proof that a child process worked on Windows at all.
    ///
    /// It is also the worst place to be running somebody else's code unprotected, because a
    /// plugin that dies while being asked what it is would take the application down every time
    /// it started, before anybody had chosen to use it.
    /// </remarks>
    List<PluginInfo> Scan(IReadOnlyList<string> folders);
}
