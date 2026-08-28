using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>
/// The FIRE page: the pads as they are played, with nothing on them that can be edited.
/// </summary>
/// <remarks>
/// Everything it does is bindings onto the pad view models, so there is no behaviour here.
/// </remarks>
public partial class UseView : UserControl
{
    /// <summary>Builds the page. It holds no state of its own.</summary>
    public UseView()
    {
        InitializeComponent();
    }
}
