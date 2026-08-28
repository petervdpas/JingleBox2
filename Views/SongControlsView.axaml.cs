using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>What this song has its controller pointed at. No behaviour of its own.</summary>
/// <remarks>
/// The song's half of the two link layers, so it lists what is stored in the .jibx rather than
/// what is on the desk. SETTINGS shows the other half.
/// </remarks>
public partial class SongControlsView : UserControl
{
    /// <summary>Builds the list. Everything on it comes from the song through bindings.</summary>
    public SongControlsView()
    {
        InitializeComponent();
    }
}
