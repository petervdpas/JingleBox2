namespace JingleBox2.Audio.Plugins;

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
}
