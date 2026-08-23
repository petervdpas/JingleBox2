using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace JingleBox2.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows a modal yes/no prompt. Answers no when it is cancelled, closed, or there is no
    /// window to open it over.
    /// </summary>
    public static Task<bool> AskAsync(string title, string message, string confirmText)
    {
        var dialog = new ConfirmDialog { Title = title };

        Fill(dialog, message, confirmText);

        return Dialog.ShowAsync(dialog, false);
    }

    /// <summary>
    /// The same window with nothing to decide: one button, and no red on it. Used where the app
    /// has already made the decision and is only saying so.
    /// </summary>
    public static Task NoteAsync(string title, string message)
    {
        var dialog = new ConfirmDialog { Title = title };

        Fill(dialog, message, "OK");

        var cancel = dialog.FindControl<Button>("CancelButton");
        if (cancel != null) cancel.IsVisible = false;

        var confirm = dialog.FindControl<Button>("ConfirmButton");
        confirm?.Classes.Remove("danger");

        return Dialog.ShowAsync(dialog, false);
    }

    private static void Fill(ConfirmDialog dialog, string message, string confirmText)
    {
        var text = dialog.FindControl<TextBlock>("MessageText");
        if (text != null) text.Text = message;

        var confirm = dialog.FindControl<Button>("ConfirmButton");
        if (confirm != null) confirm.Content = confirmText;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
