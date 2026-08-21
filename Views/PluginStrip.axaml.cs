using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>
/// The plugin slot control. All of its behaviour is in PluginSlotViewModel, so the same strip
/// works for a pad, a tracker track, or anything else that grows one later.
/// </summary>
public partial class PluginStrip : UserControl
{
    public PluginStrip()
    {
        InitializeComponent();
    }
}
