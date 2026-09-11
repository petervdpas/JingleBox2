using CommunityToolkit.Mvvm.ComponentModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// One plugin in a window of its own: what is inside it and what it is called.
/// </summary>
/// <remarks>
/// The frame and nothing else. Everything the host has to say about a plugin is said where the
/// plugin sits rather than here: switching it off is the power button on its block in the chain,
/// and taking it out is the cross beside that. A second control over either of those would be two
/// things over one flag with nothing but a binding keeping them in step.
/// </remarks>
public sealed class PluginWindowViewModel : ObservableObject
{
    /// <summary>
    /// Makes the window's contents around a panel that is already built.
    /// </summary>
    /// <param name="panel">The controls already built for the plugin, which the window only frames.</param>
    /// <param name="name">
    /// What the title bar says. Passed in rather than read off the plugin, because a track's
    /// instrument is named by the person who put it there and the plugin's own name is only
    /// part of that.
    /// </param>
    public PluginWindowViewModel(PluginControlsViewModel panel, string name)
    {
        Panel = panel;
        Name = name;
    }

    /// <summary>The plugin's controls: its own interface if it has one, our knobs if not.</summary>
    public PluginControlsViewModel Panel { get; }

    /// <summary>What the title bar says.</summary>
    public string Name { get; }
}
