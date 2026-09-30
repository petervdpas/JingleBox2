
namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>A plugin that may have an interface of its own.</summary>
/// <remarks>
/// Kept apart from the plugin itself because having a window is not a fact about being a plugin.
/// A compressor with no picture is an ordinary plugin and gets the host's knobs; asking every
/// plugin for an editor and taking null half the time would say the opposite.
/// </remarks>
public interface IPluginWindowSource
{
    /// <summary>
    /// Opens the plugin's own interface, or null when it has none. Some plugins are all
    /// parameters and no picture, and those still get the host's knobs.
    /// </summary>
    IPluginEditor? OpenEditor();

    /// <summary>
    /// Whether <see cref="OpenEditor"/> may be called from a thread other than the one that draws.
    /// </summary>
    /// <remarks>
    /// Opening an interface is the plugin building its whole picture, which was measured at 0.6 to
    /// 4.7 s, and a drawing thread waiting for that is every page of the application standing
    /// still. A plugin in a process of its own is only being asked a question over a socket, so
    /// the wait can happen anywhere, and the window is put up once the answer is in, at the size
    /// the plugin gave. A plugin loaded into this process is different: both standards want its
    /// view made on the thread its window lives on, so no is the answer unless a plugin says
    /// otherwise.
    /// </remarks>
    bool OpensOffTheDrawingThread => false;
}
