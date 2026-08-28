using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.Audio.Records;
using JingleBox2.ViewModels;
using System.Threading.Tasks;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// The shelf, to pick one take off it.
/// </summary>
/// <remarks>
/// A dialog rather than a picker on the panel, because finding a take is a hunt and a hunt
/// needs room: a category to narrow by, a name to search for, and the takes themselves listed
/// with what they are filed under. All of that costs one button on the panel, which is what a
/// machine has room for.
///
/// It works on the panel's own filter, so the category you were last hunting in is the one it
/// opens in. The search is cleared each time, since a search is about one take and the next
/// hunt is a different one.
/// </remarks>
public partial class TakeDialog : Window
{
    /// <summary>Finding the window a modal sits over. Holds nothing, so one serves them all.</summary>
    private static readonly IDialogs Modal = new Dialogs();

    /// <summary>Builds the window. Its list, its categories and its search are all bound.</summary>
    public TakeDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the shelf over the app's window and gives back the take that was picked, or null
    /// when it was cancelled or there is no window to sit over.
    /// </summary>
    public static Task<Recording?> PickAsync(TakeFilter takes)
    {
        takes.Search = "";

        var dialog = new TakeDialog { DataContext = takes };

        return Modal.ShowAsync<Recording?>(dialog, null);
    }

    /// <summary>
    /// A double click on a take is the same as picking it and pressing the button, which is
    /// what anybody who has used a file dialog expects of a list.
    /// </summary>
    private void Takes_DoubleTapped(object? sender, RoutedEventArgs e) => Pick_Click(sender, e);

    /// <summary>
    /// Hands the picked take back and closes.
    /// </summary>
    /// <remarks>
    /// Nothing picked means nothing to hand back, and the window stays open: the double click
    /// landed on the empty part of the list, or Enter was pressed before anything was chosen.
    /// </remarks>
    private void Pick_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("Takes")?.SelectedItem is not Recording take) return;

        Close(take);
    }

    /// <summary>Closes with nothing picked, which the caller reads as the hunt being abandoned.</summary>
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
