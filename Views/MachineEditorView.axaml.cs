using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>
/// Where a machine is built.
/// </summary>
/// <remarks>
/// Empty for now, and deliberately: it opens a machine project, and a project is a thing on
/// disc that is made, saved, installed and eventually sold. The rack is the other side of
/// that, what is installed and ready for a song to take an instrument off, and it stays in
/// the tracker where a song is written.
/// </remarks>
public partial class MachineEditorView : UserControl
{
    public MachineEditorView()
    {
        InitializeComponent();
    }
}
