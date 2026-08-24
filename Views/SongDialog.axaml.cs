using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace JingleBox2.Views;

/// <summary>
/// The songs there are, to pick one to open.
/// </summary>
/// <remarks>
/// A list of files is a list of files: it belongs in a dialog you open when you want it, not
/// standing along the foot of the page taking room from the pattern for the whole of the time
/// you are not opening anything.
///
/// It works on the tracker itself rather than on a copy of the list, so deleting a song from
/// here is the same delete with the same question asked, and the list is right again the moment
/// it answers.
/// </remarks>
public partial class SongDialog : Window
{
    public SongDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the list over the app's window. True when a song was picked to open, false when
    /// it was cancelled or there is no window to sit over.
    /// </summary>
    public static Task<bool> PickAsync(object tracker)
    {
        var dialog = new SongDialog { DataContext = tracker };

        return Dialog.ShowAsync(dialog, false);
    }

    private void Songs_DoubleTapped(object? sender, RoutedEventArgs e) => Open_Click(sender, e);

    private void Open_Click(object? sender, RoutedEventArgs e)
    {
        // Nothing picked, nothing to open: the double click landed on the empty part of the list.
        if (this.FindControl<ListBox>("Songs")?.SelectedItem == null) return;

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
