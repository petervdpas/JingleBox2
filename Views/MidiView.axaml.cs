using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>
/// Which MIDI message triggers which pad, shown by the MidiMappingWindow dialog.
/// </summary>
/// <remarks>
/// A page rather than a window so it can be hosted by that dialog, which is the only thing that
/// shows it. Learning and clearing are on the view model, so nothing is answered here.
/// </remarks>
public partial class MidiView : UserControl
{
    /// <summary>Builds the page. Its rows come from the MIDI view model.</summary>
    public MidiView()
    {
        InitializeComponent();
    }
}
