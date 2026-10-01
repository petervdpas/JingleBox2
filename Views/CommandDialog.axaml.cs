using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// The command popup: a pattern command picked by name and set with a ruler, a chord or a slider,
/// given back as the command to write, nothing to clear it, or null when it is cancelled.
/// </summary>
/// <remarks>
/// What it decides is all in <see cref="CommandEditorViewModel"/>; this is the window and its
/// three answers.
/// </remarks>
public partial class CommandDialog : Window
{
    /// <summary>Finding the window a modal sits over. Holds nothing, so one serves them all.</summary>
    private static readonly IDialogs Modal = new Dialogs();

    /// <summary>Builds the window. What it edits is handed in by <see cref="AskAsync"/>.</summary>
    public CommandDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Asks for a command, opening on the one given. Gives back the command to write,
    /// <see cref="TrackerCommand.None"/> to clear it, or null when it is cancelled or there is
    /// no window to open it over.
    /// </summary>
    /// <param name="current">The command the cell already holds.</param>
    /// <param name="where">What it will be written into, for the title.</param>
    public static Task<TrackerCommand?> AskAsync(TrackerCommand current, string where)
    {
        var dialog = new CommandDialog
        {
            Title = "Command on " + where,
            DataContext = new CommandEditorViewModel(current)
        };

        return Modal.ShowAsync<TrackerCommand?>(dialog, null);
    }

    /// <summary>OK: the command as the controls stand.</summary>
    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommandEditorViewModel editor) Close(editor.Command);
        else Close(null);
    }

    /// <summary>Clear: no command at all.</summary>
    private void Clear_Click(object? sender, RoutedEventArgs e) => Close(TrackerCommand.None);

    /// <summary>Cancel: nothing changes.</summary>
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
