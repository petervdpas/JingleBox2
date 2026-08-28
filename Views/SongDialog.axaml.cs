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
    /// <summary>Builds the window. Its list and its search box are the tracker's own.</summary>
    public SongDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the list over the app's window. True when a song was picked to open, false when
    /// it was cancelled or there is no window to sit over.
    /// </summary>
    /// <remarks>
    /// The search is cleared on the way in, whatever was typed the last time it was open,
    /// because a dialog that opens filtered looks like a dialog that has lost your songs. The
    /// box takes the keyboard, since typing is the first thing you do on a long list and costs
    /// nothing on a short one; Enter still opens what is picked, because the Open button is the
    /// default.
    /// </remarks>
    public static Task<bool> PickAsync(ViewModels.TrackerViewModel tracker)
    {
        tracker.SongSearch = "";

        var dialog = new SongDialog { DataContext = tracker };

        dialog.Opened += (_, _) => dialog.FindControl<TextBox>("SearchBox")?.Focus();

        return Dialog.ShowAsync(dialog, false);
    }

    /// <summary>
    /// A double click on a song opens it, which is what anybody who has used a file dialog
    /// expects of a list.
    /// </summary>
    private void Songs_DoubleTapped(object? sender, RoutedEventArgs e) => Open_Click(sender, e);

    /// <summary>
    /// Deletes the song on that row, whichever row the button was on.
    /// </summary>
    /// <remarks>
    /// From the button's own row rather than from what is picked in the list, because a press
    /// on a row's button is about that row. Picking it first and then deleting would be one
    /// gesture too many, and picking it is also how you open it.
    ///
    /// The press is marked handled so it is not also the list's. Without that, deleting picks
    /// the row on the way past, and a press meant to remove a song leaves it selected and ready
    /// to be opened by Enter. With nothing left in the list the dialog has nothing to do, so it
    /// closes.
    /// </remarks>
    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.TrackerViewModel tracker) return;
        if (sender is not Control button || button.DataContext is not Tracker.SongFile file) return;

        e.Handled = true;

        await tracker.DeleteSongFile(file);

        if (tracker.ShownSongs.Count == 0) Close(false);
    }

    /// <summary>
    /// Closes with a yes, and the tracker opens whatever its list has picked.
    /// </summary>
    /// <remarks>
    /// Nothing picked means nothing to open, and the window stays put: the double click landed
    /// on the empty part of the list.
    /// </remarks>
    private void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("Songs")?.SelectedItem == null) return;

        Close(true);
    }

    /// <summary>Closes with a no, leaving the song that is open open.</summary>
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
