using Avalonia.Controls;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The PADS page: where a pad is given its name, its source, its level and its fades.
/// </summary>
/// <remarks>
/// FIRE plays the same pads and cannot change any of that, which is the whole reason there are
/// two pages over one set of view models.
/// </remarks>
public partial class PadsView : UserControl
{
    /// <summary>Builds the page. Every pad's settings are bound; only the take picker is not.</summary>
    public PadsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The take that was picked, onto the pad the button belongs to.
    /// </summary>
    /// <remarks>
    /// Read off the button rather than off the page, since the pad being edited is the
    /// button's own data context and there is no second place for it to disagree with.
    /// </remarks>
    private void PadTake_Picked(object? sender, TakePickedEventArgs e)
    {
        if (sender is Control control && control.DataContext is PadViewModel pad)
            pad.FilePath = e.Take.FilePath;
    }
}
