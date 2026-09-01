using Avalonia.Controls;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The knobs for one plugin, as a panel to read and to turn.
/// </summary>
/// <remarks>
/// Nothing of its own: every knob is bound to a <see cref="PluginParameterViewModel"/> and the
/// panel is the list of them.
///
/// A hardware control cannot be pointed at any of these, and that is a decision rather than a
/// gap. A plugin is somebody else's program and brings its own MIDI learn, so pointing at it
/// here meant a second mapping beside the one the plugin already keeps, and the two would
/// disagree about what a knob does. Remote control is for machines, our own effects and the
/// mixer, which are the things this application is the only owner of.
/// </remarks>
public partial class PluginParameters : UserControl
{
    /// <summary>Builds the panel. Everything on it comes from the plugin through bindings.</summary>
    public PluginParameters()
    {
        InitializeComponent();
    }
}
