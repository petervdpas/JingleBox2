using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>
/// A track's MIDI block: the port and channel it plays from, and the port and channel it sends to.
/// </summary>
/// <remarks>
/// Drawn over a <see cref="ViewModels.TrackMidiViewModel"/> and nothing else, so it can stand in
/// front of a track's chain or anywhere else a track's MIDI is shown. Everything it does is a
/// binding; nothing here decides anything.
/// </remarks>
public partial class TrackMidiBlock : UserControl
{
    /// <summary>Builds the block from its layout.</summary>
    public TrackMidiBlock()
    {
        InitializeComponent();
    }
}
